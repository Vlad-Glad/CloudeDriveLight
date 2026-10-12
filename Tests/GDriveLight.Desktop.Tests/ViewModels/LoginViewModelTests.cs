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
}
