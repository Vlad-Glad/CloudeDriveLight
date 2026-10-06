using GDriveLight.Api.Contracts.Files;
using GDriveLight.Application.Files.Commands;
using GDriveLight.Application.Files.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace GDriveLight.Api.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private readonly IMediator _mediator;

    public FilesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadFile(
        IFormFile file,
        [FromForm] Guid? folderId)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "File is empty or not provided." });
        }

        // Stub user id
        var stubUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        using var stream = file.OpenReadStream();
        var command = new UploadFileCommand(file.FileName, stream, stubUserId, folderId);
        
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return CreatedAtAction(nameof(GetFile), new { id = result.Value }, new { id = result.Value });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetFile(Guid id)
    {
        var stubUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        
        var query = new GetFileByIdQuery(id, stubUserId);
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error });
        }

        var dto = result.Value;
        var response = new FileResponse(
            dto.Id, dto.Name, dto.FileTypeId, dto.ContentHash, 
            dto.FolderId, dto.OwnerId, dto.UploadedAtUtc, dto.EditedAtUtc, dto.FileUrl);

        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetFiles([FromQuery] Guid? folderId)
    {
        var stubUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var query = new GetFilesByFolderQuery(folderId, stubUserId);
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        var response = result.Value.Select(dto => new FileResponse(
            dto.Id, dto.Name, dto.FileTypeId, dto.ContentHash, 
            dto.FolderId, dto.OwnerId, dto.UploadedAtUtc, dto.EditedAtUtc, dto.FileUrl));

        return Ok(response);
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadFile(Guid id)
    {
        var stubUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var query = new DownloadFileQuery(id, stubUserId);
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        var fileResult = result.Value;
        return File(fileResult.FileStream, fileResult.MimeType, fileResult.FileName);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFile(Guid id)
    {
        var stubUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var command = new DeleteFileCommand(id, stubUserId);
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    [HttpPut("{id}/rename")]
    public async Task<IActionResult> RenameFile(Guid id, [FromBody] RenameFileRequest request)
    {
        var stubUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var command = new RenameFileCommand(id, request.NewName, stubUserId);
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    [HttpPut("{id}/move")]
    public async Task<IActionResult> MoveFile(Guid id, [FromBody] MoveFileRequest request)
    {
        var stubUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var command = new MoveFileCommand(id, request.NewFolderId, stubUserId);
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchFiles([FromQuery] string name)
    {
        var stubUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var query = new SearchFilesQuery(name, stubUserId);
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        var response = result.Value.Select(dto => new FileResponse(
            dto.Id, dto.Name, dto.FileTypeId, dto.ContentHash, 
            dto.FolderId, dto.OwnerId, dto.UploadedAtUtc, dto.EditedAtUtc, dto.FileUrl));

        return Ok(response);
    }
}
