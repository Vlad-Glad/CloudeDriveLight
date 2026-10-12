using FluentValidation;

namespace GDriveLight.Application.Files.Commands;

public class UpdateFileContentCommandValidator : AbstractValidator<UpdateFileContentCommand>
{
    public UpdateFileContentCommandValidator()
    {
        RuleFor(x => x.FileId)
            .NotEmpty().WithMessage("File ID is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.FileStream)
            .NotNull().WithMessage("File stream cannot be null.");
    }
}
