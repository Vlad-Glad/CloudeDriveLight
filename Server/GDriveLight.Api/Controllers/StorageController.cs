using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Storage;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GDriveLight.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/storage")]
public sealed class StorageController(IMediator mediator, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("usage")]
    public async Task<ActionResult<StorageUsage>> GetUsage(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await mediator.Send(new GetStorageUsageQuery(currentUser.UserId), cancellationToken));
    }
}
