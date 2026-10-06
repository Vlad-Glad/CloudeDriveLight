using GDriveLight.Application.Folders.Commands.CreateFolder;
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
        // Створюємо заглушку користувача (ніби він зараз залогінений)
        var stubUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var command = new CreateFolderCommand(request.Name, stubUserId, request.ParentFolderId);
        
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            // Це бізнес-помилка з Result Pattern (наприклад "Файл вже існує")
            return BadRequest(new { error = result.Error });
        }

        return Ok(new { folderId = result.Value });
    }
}

public class CreateFolderRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid? ParentFolderId { get; set; }
}
