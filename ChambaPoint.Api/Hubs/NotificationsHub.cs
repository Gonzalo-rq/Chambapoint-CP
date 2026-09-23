using Microsoft.AspNetCore.SignalR;

namespace ChambaPoint.Api.Hubs;

public class NotificationsHub : Hub
{
    public async Task JoinUserGroup(int userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
    }

    public async Task LeaveUserGroup(int userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
    }
}
