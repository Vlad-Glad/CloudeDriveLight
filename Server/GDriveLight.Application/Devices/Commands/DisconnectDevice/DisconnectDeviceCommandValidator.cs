using FluentValidation;

namespace GDriveLight.Application.Devices.Commands.DisconnectDevice;

public sealed class DisconnectDeviceCommandValidator : AbstractValidator<DisconnectDeviceCommand>
{
    public DisconnectDeviceCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.DeviceId).NotEmpty();
    }
}
