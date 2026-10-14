using FluentValidation;

namespace GDriveLight.Application.Devices.Commands.RegisterDevice;

public class RegisterDeviceCommandValidator : AbstractValidator<RegisterDeviceCommand>
{
    public RegisterDeviceCommandValidator()
    {
        RuleFor(x => x.DeviceId)
            .NotEmpty().WithMessage("Device ID cannot be empty.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID cannot be empty.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Device name cannot be empty.")
            .MaximumLength(100).WithMessage("Device name must not exceed 100 characters.");
    }
}
