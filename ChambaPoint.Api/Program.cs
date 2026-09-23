using System.Text;
using ChambaPoint.Api.Data;
using ChambaPoint.Api.Hubs;
using ChambaPoint.Api.Models;
using ChambaPoint.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var redisConnection = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "ChambaPoint:";
    });
    Console.WriteLine("Cache: Redis Cloud");
}
else
{
    builder.Services.AddDistributedMemoryCache();
    Console.WriteLine("Cache: InMemory (fallback). Configura Redis:ConnectionString para usar Redis Cloud.");
}

builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IWorkerService, WorkerService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddSingleton<IRequestNotifier, RabbitMqRequestNotifier>();
builder.Services.AddSignalR();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("Frontend");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationsHub>("/hubs/notifications");

app.Run();