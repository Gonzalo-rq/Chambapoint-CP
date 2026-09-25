using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ChambaPoint.Api.Data;
using ChambaPoint.Api.Models;
using ChambaPoint.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChambaPoint.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly ICacheService _cache;
    private readonly IPasswordHasher<User> _hasher;

    public AuthController(
        AppDbContext db,
        ITokenService tokenService,
        ICacheService cache,
        IPasswordHasher<User> hasher)
    {
        _db = db;
        _tokenService = tokenService;
        _cache = cache;
        _hasher = hasher;
    }

    public record RegisterRequest(string Name, string Email, string Password, string? Role, string? AvatarUrl);

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Name, Email y Password son obligatorios." });
        }

        if (request.Password.Length < 6)
        {
            return BadRequest(new { message = "La contraseña debe tener al menos 6 caracteres." });
        }

        var role = string.Equals(request.Role, Roles.Worker, StringComparison.OrdinalIgnoreCase)
            ? Roles.Worker
            : Roles.Customer;

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(u => u.Email == normalizedEmail, ct);
        if (exists)
        {
            return Conflict(new { message = "Ya existe una cuenta con ese email." });
        }

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = normalizedEmail,
            Role = role,
            AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);

        _db.Users.Add(user);

        if (role == Roles.Worker)
        {
            _db.Workers.Add(new Worker
            {
                User = user,
                Profession = RequestCategories.Plomeria
            });
        }

        await _db.SaveChangesAsync(ct);

        await CacheUserAsync(user, ct);

        var token = _tokenService.GenerateToken(user);
        return Created($"/api/users/{user.Id}", new
        {
            token,
            user = BuildUserDto(user)
        });
    }

    public record LoginRequest(string Email, string Password);

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Email y Password son obligatorios." });
        }

        var user = await _db.Users
            .Include(u => u.WorkerProfile)
            .FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLowerInvariant(), ct);

        if (user is null)
        {
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Credenciales inválidas." });
        }

        await CacheUserAsync(user, ct);

        var token = _tokenService.GenerateToken(user);
        return Ok(new
        {
            token,
            user = BuildUserDto(user)
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var cached = await _cache.GetAsync<User>("user:" + userId, ct);
        if (cached is not null)
        {
            return Ok(new { user = BuildUserDto(cached) });
        }

        var user = await _db.Users
            .Include(u => u.WorkerProfile)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user is null)
        {
            return NotFound(new { message = "Usuario no encontrado." });
        }

        await CacheUserAsync(user, ct);
        return Ok(new { user = BuildUserDto(user) });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is not null)
        {
            await _cache.RemoveAsync("user:" + userId, ct);
        }
        return Ok(new { message = "Sesión cerrada." });
    }

    private async Task CacheUserAsync(User user, CancellationToken ct)
    {
        await _cache.SetAsync("user:" + user.Id, user, TimeSpan.FromMinutes(30), ct);
    }

    private int? GetUserId()
    {
        var sub = User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(sub, out var id) ? id : null;
    }

    private static object BuildUserDto(User user)
    {
        return new
        {
            user.Id,
            user.Name,
            user.Email,
            user.Role,
            user.AvatarUrl,
            user.CreatedAt,
            hasWorkerProfile = user.WorkerProfile is not null
        };
    }
}