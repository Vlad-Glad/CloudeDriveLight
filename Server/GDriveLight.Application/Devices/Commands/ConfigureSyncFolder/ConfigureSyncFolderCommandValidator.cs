using FluentValidation;

namespace GDriveLight.Application.Devices.Commands.ConfigureSyncFolder;

public sealed class ConfigureSyncFolderCommandValidator : AbstractValidator<ConfigureSyncFolderCommand>
{
    public ConfigureSyncFolderCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.DeviceId).NotEmpty();
        RuleFor(x => x.LocalPath).NotEmpty().MaximumLength(4096);
    }
}
