using FluentValidation;

namespace GDriveLight.Application.Folders.Commands;

public class MoveFolderCommandValidator : AbstractValidator<MoveFolderCommand>
{
    public MoveFolderCommandValidator()
    {
        RuleFor(x => x.FolderId)
            .NotEmpty().WithMessage("Folder ID cannot be empty.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID cannot be empty.");
            
        RuleFor(x => x.NewParentFolderId)
            .Must((cmd, parentId) => parentId != cmd.FolderId)
            .WithMessage("A folder cannot be moved into itself.");
    }
}
