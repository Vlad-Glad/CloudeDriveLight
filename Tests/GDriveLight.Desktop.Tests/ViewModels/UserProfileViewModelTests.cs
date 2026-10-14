using System.Windows.Media;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.ViewModels;
using Moq;

namespace GDriveLight.Desktop.Tests.ViewModels;

public sealed class UserProfileViewModelTests
{
    [Theory]
    [InlineData(" Олена ", "Олена", "О")]
    [InlineData("e\u0301mile", "e\u0301mile", "E\u0301")]
    [InlineData("", "Your account", "?")]
    public void ProfileUsesOnlyFirstNameAndOneCompleteInitial(string firstName, string expectedName, string expectedInitial)
    {
        var api = new Mock<IDriveApiService>();
        api.SetupGet(service => service.CurrentUser).Returns(new UserProfile(Guid.NewGuid(), firstName, "user@example.com"));
        using var profile = new UserProfileViewModel(api.Object, new Mock<IAvatarService>().Object);

        Assert.Equal(expectedName, profile.FirstName);
        Assert.Equal(expectedInitial, profile.Initial);
        Assert.Equal("user@example.com", profile.Email);
        Assert.Null(profile.Avatar);
    }

    [Fact]
    public async Task NoProfileDoesNotRequestAnAvatar()
    {
        var avatars = new Mock<IAvatarService>(MockBehavior.Strict);
        using var profile = new UserProfileViewModel(new Mock<IDriveApiService>().Object, avatars.Object);
        await profile.LoadAvatarAsync();
        Assert.Equal("Your account", profile.FirstName);
        Assert.Equal("?", profile.Initial);
        avatars.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AnAvatarIsLoadedOnlyOnceAndNotifiesTheView()
    {
        var image = new DrawingImage();
        image.Freeze();
        var avatars = new Mock<IAvatarService>(MockBehavior.Strict);
        avatars.Setup(service => service.GetAvatarAsync("user@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(image);
        using var profile = CreateProfile(avatars.Object);
        var notified = false;
        profile.PropertyChanged += (_, e) => notified |= e.PropertyName == nameof(profile.Avatar);

        await profile.LoadAvatarAsync();
        await profile.LoadAvatarAsync();

        Assert.Same(image, profile.Avatar);
        Assert.True(notified);
        avatars.Verify(service => service.GetAvatarAsync("user@example.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EndingTheSessionCancelsTheRequestAndIgnoresALateImage()
    {
        var response = new TaskCompletionSource<ImageSource?>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default;
        var avatars = new Mock<IAvatarService>();
        avatars.Setup(service => service.GetAvatarAsync("user@example.com", It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((_, cancellation) => token = cancellation).Returns(response.Task);
        using var profile = CreateProfile(avatars.Object);
        var load = profile.LoadAvatarAsync();

        profile.Dispose();
        var image = new DrawingImage();
        image.Freeze();
        response.SetResult(image);
        await load;

        Assert.True(token.IsCancellationRequested);
        Assert.Null(profile.Avatar);
    }

    [Fact]
    public async Task EndingTheSessionHandlesACanceledDownload()
    {
        var avatars = new Mock<IAvatarService>();
        avatars.Setup(service => service.GetAvatarAsync("user@example.com", It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return null; });
        using var profile = CreateProfile(avatars.Object);
        var load = profile.LoadAvatarAsync();
        profile.Dispose();
        await load;
        Assert.Null(profile.Avatar);
    }

    private static UserProfileViewModel CreateProfile(IAvatarService avatars)
    {
        var api = new Mock<IDriveApiService>();
        api.SetupGet(service => service.CurrentUser).Returns(new UserProfile(Guid.NewGuid(), "Олена", "user@example.com"));
        return new UserProfileViewModel(api.Object, avatars);
    }
}
