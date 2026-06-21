using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SignalRChat.Api.Contracts;
using SignalRChat.Api.Data;
using SignalRChat.Api.Entities;
using SignalRChat.Api.Hubs;

namespace SignalRChat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController(ChatDbContext dbContext, ActiveUserTracker activeUserTracker) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ChatUserResponse>> CreateUser(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.Email == request.Email, cancellationToken);

        if (existingUser != null)
        {
            return Ok(new ChatUserResponse
            (
                existingUser.Id,
                existingUser.Username,
                existingUser.Email
            ));
        }

        var user = new ChatUser
        {
            Username = request.Username,
            Email = request.Email,
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ChatUserResponse
        (
            user.Id,
            user.Username,
            user.Email
        ));
    }

    [HttpGet]
    public async Task<ActionResult<List<ChatUserResponse>>> GetUsers(
        CancellationToken cancellationToken)
    {
        var users = await dbContext.Users
            .OrderBy(u => u.Username)
            .Select(u => new ChatUserResponse(
                u.Id,
                u.Username,
                u.Email
        )).ToListAsync(cancellationToken);

        return Ok(users);
    }

    [HttpGet("active")]
    public IActionResult GetActiveUsers()
    {
        var activeUserIds = activeUserTracker.GetActiveUserIds().ToList();

        var activeUsers = dbContext.Users
            .Where(u => activeUserIds.Contains(u.Id))
            .Select(u => new ChatUserResponse(
                u.Id,
                u.Username,
                u.Email
        )).ToList();

        return Ok(new
        {
            activeUserCount = activeUserTracker.GetActiveUserCount(),
            users = activeUsers
        });
    }
}