using GDriveLight.Api.Contracts.Folders;
using GDriveLight.Application.Folders.Commands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GDriveLight.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FoldersController : ControllerBase
{
    private readonly IMediator _mediator;

    public FoldersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateFolder([FromBody] CreateFolderRequest request)
    {
        //заглушка користувача   
        var stubUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var command = new CreateFolderCommand(request.Name, stubUserId, request.ParentFolderId);

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
        //заглушка користувача   
        var stubUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var command = new RenameFolderCommand(id, request.NewName, stubUserId);

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
        //заглушка користувача   
        var stubUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var command = new DeleteFolderCommand(id, stubUserId);

        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }
}