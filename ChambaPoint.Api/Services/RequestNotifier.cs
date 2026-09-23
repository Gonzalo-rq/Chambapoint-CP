using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace ChambaPoint.Api.Services;

public interface IRequestNotifier
{
    Task PublishAsync(string type, object payload, CancellationToken ct = default);
    bool IsEnabled { get; }
}

public class RabbitMqRequestNotifier : IRequestNotifier, IDisposable, IAsyncDisposable
{
    private readonly string? _uri;
    private readonly ILogger<RabbitMqRequestNotifier> _logger;
    private IConnection? _connection;
    private IChannel? _channel;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_uri);

    public RabbitMqRequestNotifier(IConfiguration config, ILogger<RabbitMqRequestNotifier> logger)
    {
        _uri = config["RabbitMQ:Uri"];
        _logger = logger;

        if (!IsEnabled)
        {
            _logger.LogInformation("RabbitMQ no configurado. Las notificaciones se descartan (fallback log).");
        }
    }

    public async Task PublishAsync(string type, object payload, CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            _logger.LogInformation("[Notificacion fallback] {Type}: {Payload}", type, JsonSerializer.Serialize(payload));
            return;
        }

        await _lock.WaitAsync(ct);
        try
        {
            _channel ??= await EnsureChannelAsync(ct);

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type, payload, createdAt = DateTime.UtcNow }));
            await _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: "chambapoint.notifications",
                body: body,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publicando en RabbitMQ");
            _channel = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IChannel> EnsureChannelAsync(CancellationToken ct)
    {
        _connection ??= await ConnectAsync(ct);
        return await _connection.CreateChannelAsync(cancellationToken: ct);
    }

    private async Task<IConnection> ConnectAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory { Uri = new Uri(_uri!) };
        return await factory.CreateConnectionAsync(cancellationToken: ct);
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        _lock.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
        _lock.Dispose();
    }
}
