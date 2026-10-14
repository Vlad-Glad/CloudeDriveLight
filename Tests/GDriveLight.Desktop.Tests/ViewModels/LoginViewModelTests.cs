using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.ViewModels;
using Moq;

namespace GDriveLight.Desktop.Tests.ViewModels;

public sealed class LoginViewModelTests
{
    [Fact]
    public async Task ALoginResponseAfterClosingTheWindowCannotRestoreTheSession()
    {
        var response = new TaskCompletionSource<string?>();
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.Setup(service => service.LoginAsync("a@b.com", "password", It.IsAny<CancellationToken>())).Returns(response.Task);
        using var viewModel = new LoginViewModel(api.Object) { Email = "a@b.com", Password = "password" };
        var succeeded = false;
        viewModel.LoginSucceeded += (_, _) => succeeded = true;

        var login = viewModel.SubmitCommand.ExecuteAsync();
        viewModel.Dispose();
        response.SetResult("late-token");
        await login;

        Assert.False(succeeded);
        Assert.Empty(viewModel.Password);
        api.Verify(service => service.SetAuthToken(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterValidationIsPresentedWithoutAuthenticating()
    {
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.Setup(service => service.RegisterAsync("a@b.com", "short", "Name", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DriveApiException(System.Net.HttpStatusCode.BadRequest, "Password is too short."));
        using var viewModel = new LoginViewModel(api.Object)
        {
            Email = "a@b.com", Password = "short", FirstName = "Name", IsRegisterMode = true
        };

        await viewModel.SubmitCommand.ExecuteAsync();

        Assert.Equal("Password is too short.", viewModel.ErrorMessage);
        Assert.False(viewModel.IsLoading);
        Assert.True(viewModel.SubmitCommand.CanExecute(null));
    }

    [Fact]
    public async Task Login_WhenCredentialsAreValid_RegistersDeviceAndRaisesLoginSucceeded()
    {
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.Setup(service => service.LoginAsync("a@b.com", "password", It.IsAny<CancellationToken>()))
            .ReturnsAsync("valid-jwt-token");
        api.Setup(service => service.SetAuthToken("valid-jwt-token"));
        api.Setup(service => service.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        using var viewModel = new LoginViewModel(api.Object) { Email = "a@b.com", Password = "password" };
        var succeeded = false;
        viewModel.LoginSucceeded += (_, _) => succeeded = true;

        await viewModel.SubmitCommand.ExecuteAsync();

        Assert.True(succeeded);
        Assert.Empty(viewModel.Password);
        Assert.False(viewModel.IsLoading);
        api.Verify(service => service.SetAuthToken("valid-jwt-token"), Times.Once);
        api.Verify(service => service.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Login_WhenDeviceRegistrationFails_LogsOutAndDisplaysError()
    {
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.Setup(service => service.LoginAsync("a@b.com", "password", It.IsAny<CancellationToken>()))
            .ReturnsAsync("valid-jwt-token");
        api.Setup(service => service.SetAuthToken("valid-jwt-token"));
        api.Setup(service => service.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DriveApiException(System.Net.HttpStatusCode.BadRequest, "This device is already registered to another user."));
        api.Setup(service => service.Logout());

        using var viewModel = new LoginViewModel(api.Object) { Email = "a@b.com", Password = "password" };
        var succeeded = false;
        viewModel.LoginSucceeded += (_, _) => succeeded = true;

        await viewModel.SubmitCommand.ExecuteAsync();

        Assert.False(succeeded);
        Assert.Equal("This device is already registered to another user.", viewModel.ErrorMessage);
        Assert.False(viewModel.IsLoading);
        api.Verify(service => service.Logout(), Times.Once);
    }
}
