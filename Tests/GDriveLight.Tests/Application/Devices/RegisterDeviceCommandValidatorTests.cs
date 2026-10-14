using FluentValidation.TestHelper;
using GDriveLight.Application.Devices.Commands.RegisterDevice;

namespace GDriveLight.Tests.Application.Devices;

public class RegisterDeviceCommandValidatorTests
{
    private readonly RegisterDeviceCommandValidator _validator = new();

    [Fact]
    public void Validator_WhenCommandIsValid_PassesValidation()
    {
        var command = new RegisterDeviceCommand(Guid.NewGuid(), Guid.NewGuid(), "MyLaptop");

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validator_WhenDeviceIdIsEmpty_HasValidationError()
    {
        var command = new RegisterDeviceCommand(Guid.Empty, Guid.NewGuid(), "MyLaptop");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DeviceId);
    }

    [Fact]
    public void Validator_WhenUserIdIsEmpty_HasValidationError()
    {
        var command = new RegisterDeviceCommand(Guid.NewGuid(), Guid.Empty, "MyLaptop");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Validator_WhenNameIsEmpty_HasValidationError()
    {
        var command = new RegisterDeviceCommand(Guid.NewGuid(), Guid.NewGuid(), "");

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validator_WhenNameExceeds100Chars_HasValidationError()
    {
        var command = new RegisterDeviceCommand(Guid.NewGuid(), Guid.NewGuid(), new string('a', 101));

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}
