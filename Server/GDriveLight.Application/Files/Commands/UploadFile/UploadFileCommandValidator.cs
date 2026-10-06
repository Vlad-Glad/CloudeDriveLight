using FluentValidation;

namespace GDriveLight.Application.Files.Commands;

public class UploadFileCommandValidator : AbstractValidator<UploadFileCommand>
{
    public UploadFileCommandValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("File name cannot be empty.")
            .MaximumLength(255).WithMessage("File name cannot exceed 255 characters.");

        RuleFor(x => x.FileStream)
            .NotNull().WithMessage("File stream cannot be null.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");
    }
}
