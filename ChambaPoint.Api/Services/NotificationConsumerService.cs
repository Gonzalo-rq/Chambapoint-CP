using System.Text;
using System.Text.Json;
using ChambaPoint.Api.Data;
using ChambaPoint.Api.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ChambaPoint.Api.Services;

public class NotificationConsumerService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationConsumerService> _logger;

    public NotificationConsumerService(
        IConfiguration config,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationConsumerService> logger)
    {
        _config = config;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var uri = _config["RabbitMQ:Uri"];
        if (string.IsNullOrWhiteSpace(uri))
        {
            _logger.LogInformation("RabbitMQ URI no configurada. Consumidor en segundo plano desactivado (modo fallback).");
            return;
        }

        var queueName = _config["RabbitMQ:QueueName"] ?? "chambapoint.notifications";

        while (!stoppingToken.IsCancellationRequested)
        {
            IConnection? connection = null;
            IChannel? channel = null;

            try
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(uri),
                    AutomaticRecoveryEnabled = true,
                    ClientProvidedName = "ChambaPoint-NotificationConsumer"
                };

                connection = await factory.CreateConnectionAsync("ChambaPoint-NotificationConsumer", stoppingToken);
                channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                await channel.QueueDeclareAsync(
                    queue: queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null,
                    cancellationToken: stoppingToken);

                await channel.BasicQosAsync(0, 1, false, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += (_, ea) => ProcesarMensajeAsync(channel, ea, stoppingToken);

                await channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

                _logger.LogInformation("Consumidor RabbitMQ conectado y escuchando en la cola '{Queue}'...", queueName);

                var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ConnectionShutdownAsync += (_, _) =>
                {
                    tcs.TrySetResult();
                    return Task.CompletedTask;
                };

                try
                {
                    await tcs.Task.WaitAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error en el consumidor de RabbitMQ ({Queue}). Reintentando en 10s...", queueName);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            finally
            {
                if (channel is not null)
                {
                    try { await channel.CloseAsync(CancellationToken.None); } catch { }
                    channel.Dispose();
                }

                if (connection is not null)
                {
                    try { await connection.CloseAsync(CancellationToken.None); } catch { }
                    connection.Dispose();
                }
            }
        }
    }

    private async Task ProcesarMensajeAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        var raw = Encoding.UTF8.GetString(ea.Body.ToArray());
        _logger.LogInformation("RabbitMQ [Evento Recibido]: {Raw}", raw);

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var eventType = "notification";
            if (doc.RootElement.TryGetProperty("type", out var typeProp))
            {
                eventType = typeProp.GetString() ?? "notification";
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var audit = new NotificationAudit
            {
                EventType = eventType,
                Payload = raw,
                ProcessedAt = DateTime.UtcNow
            };

            db.NotificationAudits.Add(audit);
            await db.SaveChangesAsync(stoppingToken);

            _logger.LogInformation("Auditoría de notificación #{Id} guardada en BD para evento '{EventType}'", audit.Id, eventType);
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando mensaje de RabbitMQ.");
            // Reencola o descarta según sea necesario; en este caso descartamos para evitar bucles infinitos
            await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
        }
    }
}
