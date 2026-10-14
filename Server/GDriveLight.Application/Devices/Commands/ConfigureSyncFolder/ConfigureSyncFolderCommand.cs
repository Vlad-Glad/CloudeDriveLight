using GDriveLight.Application.Common.Models;
using GDriveLight.Application.Devices.Queries;
using MediatR;

namespace GDriveLight.Application.Devices.Commands.ConfigureSyncFolder;

public record ConfigureSyncFolderCommand(Guid UserId, Guid DeviceId, string LocalPath) : IRequest<Result<DeviceInfo>>;
