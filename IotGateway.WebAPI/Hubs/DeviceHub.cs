using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace IotGateway.WebAPI.Hubs;

[Authorize]
public sealed class DeviceHub : Hub
{
}
