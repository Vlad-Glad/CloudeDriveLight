using GDriveLight.Api.Contracts.Folders;
using GDriveLight.Application.Folders.Commands;
using GDriveLight.Application.Folders.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

using GDriveLight.Application.Abstractions.Services;
using Microsoft.AspNetCore.Authorization;

namespace GDriveLight.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FoldersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public FoldersController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateFolder([FromBody] CreateFolderRequest request)
    {
        var userId = _currentUserService.UserId;

        var command = new CreateFolderCommand(request.Name, userId, request.ParentFolderId);

        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new { folderId = result.Value });
    }

    [HttpPut("{id}/rename")]
    public async Task<IActionResult> RenameFolder(Guid id, [FromBody] RenameFolderRequest request)
    {
        var userId = _currentUserService.UserId;

        var command = new RenameFolderCommand(id, request.NewName, userId);

        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok();
    }

    [HttpPut("{id}/move")]
    public async Task<IActionResult> MoveFolder(Guid id, [FromBody] MoveFolderRequest request)
    {
        var userId = _currentUserService.UserId;

        var command = new MoveFolderCommand(id, request.NewParentFolderId, userId);

        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFolder(Guid id)
    {
        var userId = _currentUserService.UserId;

        var command = new DeleteFolderCommand(id, userId);

        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetFolder(Guid id)
    {
        var userId = _currentUserService.UserId;

        var query = new GetFolderByIdQuery(id, userId);
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error });
        }

        var response = new FolderResponse(result.Value.Id, result.Value.Name, result.Value.ParentFolderId, result.Value.OwnerId);
        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetFolderContent([FromQuery] Guid? parentFolderId)
    {
        var userId = _currentUserService.UserId;

        var query = new GetFolderContentQuery(parentFolderId, userId);
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        var response = result.Value.Select(f => new FolderResponse(f.Id, f.Name, f.ParentFolderId, f.OwnerId));
        return Ok(response);
    }

    [HttpGet("{id}/path")]
    public async Task<IActionResult> GetFolderPath(Guid id)
    {
        var userId = _currentUserService.UserId;

        var query = new GetFolderPathQuery(id, userId);
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error });
        }

        var response = result.Value.Select(f => new FolderResponse(f.Id, f.Name, f.ParentFolderId, f.OwnerId));
        return Ok(response);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchFolders([FromQuery] string name)
    {
        var userId = _currentUserService.UserId;

        var query = new GetFoldersByNameQuery(name, userId);
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        var response = result.Value.Select(f => new FolderResponse(f.Id, f.Name, f.ParentFolderId, f.OwnerId));
        return Ok(response);
    }
}