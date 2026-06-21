using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SignalRChat.Api.Data;
using SignalRChat.Api.Contracts;

namespace SignalRChat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MessagesController : ControllerBase
{
    private readonly ChatDbContext _dbContext;

    public MessagesController(ChatDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<ChatMessageResponse>>> GetMessages(
        CancellationToken cancellationToken)
    {
        var messages = await _dbContext.Messages
            .Include(m => m.Sender)
            .OrderBy(m => m.SentAtUtc)
            .Select(m => new ChatMessageResponse(
                m.Id,
                m.Sender.Id,
                m.Sender.Username,
                m.Text,
                m.SentAtUtc
            )).ToListAsync(cancellationToken);

        return Ok(messages);
    }
}