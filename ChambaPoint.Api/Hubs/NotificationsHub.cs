using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChambaPoint.Api.Hubs;

[Authorize]
public class NotificationsHub : Hub
{
    public async Task JoinUserGroup(int userId)
    {
        EnsureSelf(userId);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
    }

    public async Task LeaveUserGroup(int userId)
    {
        EnsureSelf(userId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
    }

    private void EnsureSelf(int userId)
    {
        var sub = Context.User?.FindFirstValue("sub")
            ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

        if (!int.TryParse(sub, out var currentUserId) || currentUserId != userId)
        {
            throw new HubException("No autorizado para ese grupo.");
        }
    }
}
