using GDriveLight.Api.Contracts.Files;
using GDriveLight.Application.Files.Commands;
using GDriveLight.Application.Files.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

using GDriveLight.Application.Abstractions.Services;
using Microsoft.AspNetCore.Authorization;

namespace GDriveLight.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public FilesController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadFile(
        IFormFile file,
        [FromForm] Guid? folderId,
        CancellationToken cancellationToken)
    {
        if (file == null)
        {
            return BadRequest(new { error = "File was not provided." });
        }

        var userId = _currentUserService.UserId;

        using var stream = file.OpenReadStream();
        var command = new UploadFileCommand(file.FileName, stream, userId, folderId, _currentUserService.DeviceId);
        
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return CreatedAtAction(nameof(GetFile), new { id = result.Value }, new { id = result.Value });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetFile(Guid id)
    {
        var userId = _currentUserService.UserId;
        
        var query = new GetFileByIdQuery(id, userId);
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error });
        }

        var dto = result.Value;
        var response = new FileResponse(
            dto.Id, dto.Name, dto.FileTypeId, dto.ContentHash, dto.SizeBytes,
            dto.FolderId, dto.OwnerId, dto.UploadedAtUtc, dto.EditedAtUtc, dto.FileUrl);

        return Ok(response);
    }

    [HttpGet("types")]
    public async Task<IActionResult> GetFileTypes(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetFileTypesQuery(), cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value.Select(type => new FileTypeResponse(type.Id, type.Name, type.Extension, type.MimeType, type.PreviewKind)));
    }

    [HttpGet]
    public async Task<IActionResult> GetFiles([FromQuery] Guid? folderId)
    {
        var userId = _currentUserService.UserId;

        var query = new GetFilesByFolderQuery(folderId, userId);
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        var response = result.Value.Select(dto => new FileResponse(
            dto.Id, dto.Name, dto.FileTypeId, dto.ContentHash, dto.SizeBytes,
            dto.FolderId, dto.OwnerId, dto.UploadedAtUtc, dto.EditedAtUtc, dto.FileUrl));

        return Ok(response);
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadFile(Guid id)
    {
        var userId = _currentUserService.UserId;

        var query = new DownloadFileQuery(id, userId);
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        var fileResult = result.Value;
        return File(fileResult.FileStream, fileResult.MimeType, fileResult.FileName);
    }

    [HttpGet("{id}/preview")]
    public async Task<IActionResult> PreviewFile(Guid id, CancellationToken cancellationToken)
    {
        // Share the download query's ownership check and storage access, not its attachment response.
        var result = await _mediator.Send(new DownloadFileQuery(id, _currentUserService.UserId), cancellationToken);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        var content = result.Value;
        if (FilePreviewPolicy.GetKind(content.MimeType) == "none")
        {
            await content.FileStream.DisposeAsync();
            return StatusCode(StatusCodes.Status415UnsupportedMediaType,
                new { error = "Preview is not supported for this file type." });
        }

        Response.Headers.ContentDisposition = "inline";
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.ContentSecurityPolicy = "sandbox; default-src 'none'";
        return File(content.FileStream, FilePreviewPolicy.GetContentType(content.MimeType));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteFile(Guid id)
    {
        var userId = _currentUserService.UserId;

        var command = new DeleteFileCommand(id, userId, _currentUserService.DeviceId);
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    [HttpPut("{id}/content")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateContent(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null)
        {
            return BadRequest(new { error = "File was not provided." });
        }

        var userId = _currentUserService.UserId;

        using var stream = file.OpenReadStream();
        var command = new UpdateFileContentCommand(id, stream, userId, _currentUserService.DeviceId);

        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }

    [HttpPut("{id}/rename")]
    public async Task<IActionResult> RenameFile(Guid id, [FromBody] RenameFileRequest request)
    {
        var userId = _currentUserService.UserId;

        var command = new RenameFileCommand(id, request.NewName, userId, _currentUserService.DeviceId);
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
        var userId = _currentUserService.UserId;

        var command = new MoveFileCommand(id, request.NewFolderId, userId, _currentUserService.DeviceId);
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
        var userId = _currentUserService.UserId;

        var query = new SearchFilesQuery(name, userId);
        var result = await _mediator.Send(query);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        var response = result.Value.Select(dto => new FileResponse(
            dto.Id, dto.Name, dto.FileTypeId, dto.ContentHash, dto.SizeBytes,
            dto.FolderId, dto.OwnerId, dto.UploadedAtUtc, dto.EditedAtUtc, dto.FileUrl));

        return Ok(response);
    }
}
