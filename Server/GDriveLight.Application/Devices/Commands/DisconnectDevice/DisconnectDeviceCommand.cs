using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Devices.Commands.DisconnectDevice;

public sealed record DisconnectDeviceCommand(Guid UserId, Guid DeviceId) : IRequest<Result>;
