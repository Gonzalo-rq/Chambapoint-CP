using System.Security.Claims;
using ChambaPoint.Api.Hubs;
using ChambaPoint.Api.Models;
using ChambaPoint.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace ChambaPoint.Api.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly IHubContext<NotificationsHub> _hub;
    private readonly IRequestNotifier _notifier;

    public MessagesController(
        IMessageService messageService,
        IHubContext<NotificationsHub> hub,
        IRequestNotifier notifier)
    {
        _messageService = messageService;
        _hub = hub;
        _notifier = notifier;
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromBody] SendMessageInput input, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var (message, statusCode, error) = await _messageService.SendAsync(userId.Value, input, ct);

        if (statusCode == 400) return BadRequest(new { message = error });
        if (statusCode == 404) return NotFound(new { message = error });
        if (message == null) return StatusCode(500, new { message = "Error interno enviando el mensaje." });

        var senderName = User.FindFirstValue("name") ?? User.Identity?.Name ?? message.Sender?.Name;

        // Enviar en vivo por SignalR al grupo user:{receiverId} con evento newMessage
        await _hub.Clients.Group($"user:{message.ReceiverId}").SendAsync("newMessage", new
        {
            messageId = message.Id,
            senderId = message.SenderId,
            senderName,
            receiverId = message.ReceiverId,
            requestId = message.RequestId,
            text = message.Text,
            sentAt = message.SentAt
        }, ct);

        // Notificar por RabbitMQ
        await _notifier.PublishAsync("message.sent", new
        {
            messageId = message.Id,
            senderId = message.SenderId,
            receiverId = message.ReceiverId,
            requestId = message.RequestId,
            sentAt = message.SentAt
        }, ct);

        return Created($"/api/messages/{message.Id}", MapToDto(message));
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int? withUserId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        // Si no se especifica withUserId, retornar lista de conversaciones del usuario
        if (!withUserId.HasValue)
        {
            var conversations = await _messageService.GetConversationsAsync(userId.Value, ct);
            return Ok(new { conversations });
        }

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 50;

        var (items, total, markedCount) = await _messageService.GetHistoryAsync(userId.Value, withUserId.Value, page, pageSize, ct);

        // Si se marcaron mensajes como leídos, notificar al remitente
        if (markedCount > 0)
        {
            await _hub.Clients.Group($"user:{withUserId.Value}").SendAsync("messagesRead", new
            {
                readerId = userId.Value,
                markedCount
            }, ct);
        }

        return Ok(new
        {
            withUserId = withUserId.Value,
            items = items.Select(MapToDto),
            page,
            pageSize,
            total,
            markedAsRead = markedCount
        });
    }

    [HttpPost("read")]
    public async Task<IActionResult> MarkRead([FromBody] MarkReadInput input, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var count = await _messageService.MarkAsReadAsync(userId.Value, input.WithUserId, ct);

        if (count > 0)
        {
            await _hub.Clients.Group($"user:{input.WithUserId}").SendAsync("messagesRead", new
            {
                readerId = userId.Value,
                markedCount = count
            }, ct);
        }

        return Ok(new { markedCount = count });
    }

    public record MarkReadInput(int WithUserId);

    private static object MapToDto(Message m) => new
    {
        m.Id,
        m.SenderId,
        sender = m.Sender != null ? new
        {
            m.Sender.Id,
            m.Sender.Name,
            m.Sender.AvatarUrl
        } : null,
        m.ReceiverId,
        receiver = m.Receiver != null ? new
        {
            m.Receiver.Id,
            m.Receiver.Name,
            m.Receiver.AvatarUrl
        } : null,
        m.RequestId,
        m.Text,
        m.SentAt,
        m.IsRead
    };

    private int? GetUserId()
    {
        var sub = User.FindFirstValue("sub")
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        return int.TryParse(sub, out var id) ? id : null;
    }
}
