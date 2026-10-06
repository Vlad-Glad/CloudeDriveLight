using FluentValidation;

namespace GDriveLight.Application.Files.Commands;

public class RenameFileCommandValidator : AbstractValidator<RenameFileCommand>
{
    public RenameFileCommandValidator()
    {
        RuleFor(x => x.NewName)
            .NotEmpty().WithMessage("New file name cannot be empty.")
            .MaximumLength(255).WithMessage("New file name cannot exceed 255 characters.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");
    }
}
