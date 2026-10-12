using GDriveLight.Api.Contracts.Sync;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Sync.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GDriveLight.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/sync")]
public class SyncController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public SyncController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    [HttpGet("changes")]
    public async Task<IActionResult> GetChanges(
        [FromQuery] long cursor = 0,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService.UserId;

        var query = new GetChangesQuery(userId, cursor, limit);
        var result = await _mediator.Send(query, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        var response = new SyncChangesResponse(
            result.Value.Changes.Select(c => new ChangeDto(
                c.Id,
                c.ResourceId,
                c.ResourceType,
                c.EventType,
                c.OriginDeviceId,
                c.ParentFolderId,
                c.Name,
                c.ContentHash,
                c.SizeBytes,
                c.TimestampUtc)).ToList(),
            result.Value.NextCursor,
            result.Value.HasMore);

        return Ok(response);
    }
}
