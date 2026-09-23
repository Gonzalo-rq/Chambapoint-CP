using System.Text.Json;
using System.Text.Json.Serialization;
using ChambaPoint.Api.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace ChambaPoint.Api.Services;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<bool> PingAsync(CancellationToken ct = default);
}

public class CacheService : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        ReferenceHandler = ReferenceHandler.Preserve
    };

    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheService> _logger;
    private readonly string _backend;

    public CacheService(IDistributedCache cache, IConfiguration config, ILogger<CacheService> logger)
    {
        _cache = cache;
        _logger = logger;

        var redisConnection = config.GetConnectionString("Redis");
        _backend = string.IsNullOrWhiteSpace(redisConnection) ? "InMemory" : "Redis";
    }

    public string Backend => _backend;

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        string? raw;
        try
        {
            raw = await _cache.GetStringAsync(key, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache get fallo para {Key}", key);
            return null;
        }

        if (raw is null) return null;

        try
        {
            return JsonSerializer.Deserialize<T>(raw, SerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "No se pudo deserializar cache key {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) where T : class
    {
        var options = new DistributedCacheEntryOptions();
        if (ttl is not null)
        {
            options.SetAbsoluteExpiration(ttl.Value);
        }
        else
        {
            options.SetSlidingExpiration(TimeSpan.FromMinutes(15));
        }

        try
        {
            await _cache.SetStringAsync(key, JsonSerializer.Serialize(value, SerializerOptions), options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache set fallo para {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _cache.RemoveAsync(key, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache remove fallo para {Key}", key);
        }
    }

    public Task<bool> PingAsync(CancellationToken ct = default)
    {
        return Task.FromResult(_backend == "Redis");
    }
}