using Example.QueueSystem.Api.Dtos;
using Example.QueueSystem.Application;
using Microsoft.AspNetCore.Mvc;

namespace Example.QueueSystem.Api.Controllers;

[ApiController]
[Route("api/queue")]
public class QueueController : ControllerBase
{
    private readonly IQueueService _queueService;

    public QueueController(IQueueService queueService)
    {
        _queueService = queueService;
    }

    [HttpPost("tickets")]
    public async Task<ActionResult<TicketResponseDto>> TakeTicket(CancellationToken ct)
    {
        var result = await _queueService.TakeTicketAsync(ct);

        if (!result.Success)
        {
            return Conflict(new
            {
                message = "Queue exhausted: all tickets from A0 to Z9 have been issued. Clear the queue to continue.",
            });
        }

        return Ok(new TicketResponseDto(result.TicketNumber!, result.IssuedAt));
    }

    [HttpPost("clear")]
    public async Task<ActionResult<TicketResponseDto>> Clear(CancellationToken ct)
    {
        await _queueService.ClearAsync(ct);
        return Ok(new TicketResponseDto("00", null));
    }

    [HttpGet("current")]
    public async Task<ActionResult<TicketResponseDto>> GetCurrent(CancellationToken ct)
    {
        var state = await _queueService.GetCurrentAsync(ct);
        return Ok(new TicketResponseDto(state.TicketNumber, state.IssuedAt));
    }
}
