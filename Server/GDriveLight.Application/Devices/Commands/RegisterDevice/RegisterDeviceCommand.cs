using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Devices.Commands.RegisterDevice;

public record RegisterDeviceCommand(Guid DeviceId, Guid UserId, string Name) : IRequest<Result<Guid>>;
