using ChambaPoint.Api.Data;
using ChambaPoint.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ChambaPoint.Api.Services;

public record SendMessageInput(
    int ReceiverId,
    int? RequestId,
    string Text
);

public interface IMessageService
{
    Task<(Message? Message, int StatusCode, string? Error)> SendAsync(int senderId, SendMessageInput input, CancellationToken ct = default);
    Task<(List<Message> Items, int Total, int MarkedAsReadCount)> GetHistoryAsync(int currentUserId, int withUserId, int page, int pageSize, CancellationToken ct = default);
    Task<List<object>> GetConversationsAsync(int currentUserId, CancellationToken ct = default);
    Task<int> MarkAsReadAsync(int currentUserId, int withUserId, CancellationToken ct = default);
}

public class MessageService : IMessageService
{
    private readonly AppDbContext _db;

    public MessageService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(Message? Message, int StatusCode, string? Error)> SendAsync(int senderId, SendMessageInput input, CancellationToken ct = default)
    {
        if (input.ReceiverId <= 0)
        {
            return (null, 400, "ReceiverId es requerido y debe ser mayor a 0.");
        }

        if (senderId == input.ReceiverId)
        {
            return (null, 400, "No puedes enviarte un mensaje a ti mismo.");
        }

        if (string.IsNullOrWhiteSpace(input.Text))
        {
            return (null, 400, "El texto del mensaje no puede estar vacío.");
        }

        var receiverExists = await _db.Users.AnyAsync(u => u.Id == input.ReceiverId, ct);
        if (!receiverExists)
        {
            return (null, 404, "El usuario destinatario no existe.");
        }

        if (input.RequestId.HasValue)
        {
            var requestExists = await _db.Requests.AnyAsync(r => r.Id == input.RequestId.Value, ct);
            if (!requestExists)
            {
                return (null, 400, "La solicitud referenciada no existe.");
            }
        }

        var message = new Message
        {
            SenderId = senderId,
            ReceiverId = input.ReceiverId,
            RequestId = input.RequestId,
            Text = input.Text.Trim(),
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        _db.Messages.Add(message);
        await _db.SaveChangesAsync(ct);

        await _db.Entry(message).Reference(m => m.Sender).LoadAsync(ct);
        await _db.Entry(message).Reference(m => m.Receiver).LoadAsync(ct);

        return (message, 201, null);
    }

    public async Task<(List<Message> Items, int Total, int MarkedAsReadCount)> GetHistoryAsync(int currentUserId, int withUserId, int page, int pageSize, CancellationToken ct = default)
    {
        var unread = await _db.Messages
            .Where(m => m.SenderId == withUserId && m.ReceiverId == currentUserId && !m.IsRead)
            .ToListAsync(ct);

        var markedCount = unread.Count;
        if (markedCount > 0)
        {
            foreach (var m in unread)
            {
                m.IsRead = true;
            }
            await _db.SaveChangesAsync(ct);
        }

        var query = _db.Messages
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .Where(m => (m.SenderId == currentUserId && m.ReceiverId == withUserId)
                     || (m.SenderId == withUserId && m.ReceiverId == currentUserId));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(m => m.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total, markedCount);
    }

    public async Task<int> MarkAsReadAsync(int currentUserId, int withUserId, CancellationToken ct = default)
    {
        var unread = await _db.Messages
            .Where(m => m.SenderId == withUserId && m.ReceiverId == currentUserId && !m.IsRead)
            .ToListAsync(ct);

        if (unread.Count == 0) return 0;

        foreach (var m in unread)
        {
            m.IsRead = true;
        }

        await _db.SaveChangesAsync(ct);
        return unread.Count;
    }

    public async Task<List<object>> GetConversationsAsync(int currentUserId, CancellationToken ct = default)
    {
        // Encontrar todos los usuarios con los que se han intercambiado mensajes
        var partnerIds = await _db.Messages
            .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
            .Select(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
            .Distinct()
            .ToListAsync(ct);

        var result = new List<object>();

        foreach (var partnerId in partnerIds)
        {
            var partner = await _db.Users.Include(u => u.WorkerProfile).FirstOrDefaultAsync(u => u.Id == partnerId, ct);
            if (partner == null) continue;

            var lastMessage = await _db.Messages
                .Where(m => (m.SenderId == currentUserId && m.ReceiverId == partnerId)
                         || (m.SenderId == partnerId && m.ReceiverId == currentUserId))
                .OrderByDescending(m => m.SentAt)
                .FirstOrDefaultAsync(ct);

            var unreadCount = await _db.Messages
                .CountAsync(m => m.SenderId == partnerId && m.ReceiverId == currentUserId && !m.IsRead, ct);

            result.Add(new
            {
                partnerId = partner.Id,
                partnerName = partner.Name,
                partnerRole = partner.Role,
                partnerAvatar = partner.AvatarUrl,
                profession = partner.WorkerProfile?.Profession,
                lastMessage = lastMessage != null ? new
                {
                    lastMessage.Id,
                    lastMessage.Text,
                    lastMessage.SentAt,
                    isFromMe = lastMessage.SenderId == currentUserId,
                    lastMessage.IsRead
                } : null,
                unreadCount
            });
        }

        return result;
    }
}
