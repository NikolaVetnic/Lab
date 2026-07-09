using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace OperationsCenter.Api.Hubs;

[Authorize]
public sealed class OperationsHub : Hub
{
}
