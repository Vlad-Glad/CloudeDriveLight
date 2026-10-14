using GDriveLight.Api.Contracts.Devices;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Devices.Commands.RegisterDevice;
using GDriveLight.Application.Devices.Commands.DisconnectDevice;
using MediatR;
using GDriveLight.Application.Devices.Queries;
using GDriveLight.Application.Devices.Commands.ConfigureSyncFolder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GDriveLight.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public DevicesController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DisconnectDevice(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DisconnectDeviceCommand(_currentUserService.UserId, id), cancellationToken);
        return result.IsFailure ? BadRequest(new { error = result.Error }) : NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> GetDevices(CancellationToken cancellationToken)
    {
        var devices = await _mediator.Send(new GetDevicesQuery(_currentUserService.UserId), cancellationToken);
        return Ok(devices.Select(ToResponse));
    }

    [HttpPut("{id}/sync-folder")]
    public async Task<IActionResult> ConfigureSyncFolder(Guid id, ConfigureSyncFolderRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ConfigureSyncFolderCommand(_currentUserService.UserId, id, request.LocalPath), cancellationToken);
        return result.IsFailure ? BadRequest(new { error = result.Error }) : Ok(ToResponse(result.Value));
    }

    private static DeviceResponse ToResponse(DeviceInfo device) =>
        new(device.Id, device.Name, device.SyncRootFolderId, device.LocalPath);

    [HttpPost]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        var command = new RegisterDeviceCommand(request.DeviceId, userId, request.Name);
        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(new { deviceId = result.Value });
    }
}
