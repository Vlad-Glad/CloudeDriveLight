using FluentValidation.TestHelper;
using GDriveLight.Application.Files.Commands;

namespace GDriveLight.Tests.Application.Files;

public class UpdateFileContentCommandValidatorTests
{
    private readonly UpdateFileContentCommandValidator _validator = new();

    [Fact]
    public void Validator_WhenCommandIsValid_PassesValidation()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = new UpdateFileContentCommand(Guid.NewGuid(), stream, Guid.NewGuid());

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validator_WhenFileIdIsEmpty_HasValidationError()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = new UpdateFileContentCommand(Guid.Empty, stream, Guid.NewGuid());

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.FileId);
    }

    [Fact]
    public void Validator_WhenUserIdIsEmpty_HasValidationError()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var command = new UpdateFileContentCommand(Guid.NewGuid(), stream, Guid.Empty);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Validator_WhenFileStreamIsNull_HasValidationError()
    {
        var command = new UpdateFileContentCommand(Guid.NewGuid(), null!, Guid.NewGuid());

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.FileStream);
    }
}
