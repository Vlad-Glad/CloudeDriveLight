using FluentValidation;

namespace GDriveLight.Application.Folders.Commands;

public class RenameFolderCommandValidator : AbstractValidator<RenameFolderCommand>
{
    public RenameFolderCommandValidator()
    {
        RuleFor(x => x.FolderId)
            .NotEmpty().WithMessage("Folder ID is required.");

        RuleFor(x => x.NewName)
            .NotEmpty().WithMessage("New folder name cannot be empty.")
            .MaximumLength(255).WithMessage("Folder name must not exceed 255 characters.");

        RuleFor(x => x.OwnerId)
            .NotEmpty().WithMessage("Owner ID is required.");
    }
}
