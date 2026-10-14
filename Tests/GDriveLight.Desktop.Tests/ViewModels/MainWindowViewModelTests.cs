using System.Net;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.ViewModels;
using Moq;
using Wpf.Ui.Controls;

namespace GDriveLight.Desktop.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task LateResponseCannotPopulateTheSyncedFoldersScreen()
    {
        var folders = new TaskCompletionSource<IEnumerable<FolderResponse>>();
        var files = new TaskCompletionSource<IEnumerable<FileResponse>>();
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.Setup(service => service.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        CancellationToken loadToken = default;
        api.Setup(service => service.GetFolderContentAsync(null, It.IsAny<CancellationToken>()))
            .Callback<Guid?, CancellationToken>((_, token) => loadToken = token).Returns(folders.Task);
        api.Setup(service => service.GetFilesAsync(null, It.IsAny<CancellationToken>())).Returns(files.Task);
        using var viewModel = new MainWindowViewModel(api.Object,
            new Mock<IFileDialogService>().Object, new Mock<IUserDialogService>().Object);

        var load = viewModel.InitializeAsync();
        api.Setup(service => service.GetDevicesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<DeviceResponse>());
        api.SetupGet(service => service.DeviceId).Returns(Guid.NewGuid());
        viewModel.SelectedSidebarIndex = 1;
        folders.SetResult(new[] { new FolderResponse(Guid.NewGuid(), "Old folder", null, Guid.NewGuid()) });
        files.SetResult(Array.Empty<FileResponse>());
        await load;

        Assert.True(loadToken.IsCancellationRequested);
        Assert.Empty(viewModel.Files);
        Assert.True(viewModel.IsSyncedFoldersView);
        Assert.False(viewModel.IsLoading);
        Assert.True(viewModel.IsEmpty);
    }

    [Fact]
    public async Task TheLatestRefreshWinsEvenWhenTheServerIgnoresCancellation()
    {
        var oldFolders = new TaskCompletionSource<IEnumerable<FolderResponse>>();
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.Setup(service => service.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        api.SetupSequence(service => service.GetFolderContentAsync(null, It.IsAny<CancellationToken>()))
            .Returns(oldFolders.Task)
            .ReturnsAsync(new[] { new FolderResponse(Guid.NewGuid(), "New folder", null, Guid.NewGuid()) });
        api.Setup(service => service.GetFilesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FileResponse>());
        using var viewModel = new MainWindowViewModel(api.Object,
            new Mock<IFileDialogService>().Object, new Mock<IUserDialogService>().Object);

        var oldLoad = viewModel.InitializeAsync();
        await viewModel.RefreshAsync();
        oldFolders.SetResult(new[] { new FolderResponse(Guid.NewGuid(), "Old folder", null, Guid.NewGuid()) });
        await oldLoad;

        Assert.Equal("New folder", Assert.Single(viewModel.Files).Name);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task RetryKeepsTheCurrentFolderAndDoesNotReturnToRoot()
    {
        var folder = new FolderResponse(Guid.NewGuid(), "Documents", null, Guid.NewGuid());
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.Setup(service => service.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        api.Setup(service => service.GetFolderContentAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { folder });
        api.Setup(service => service.GetFilesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FileResponse>());
        api.SetupSequence(service => service.GetFolderContentAsync(folder.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DriveApiException(HttpStatusCode.InternalServerError, "Try again."))
            .ReturnsAsync(Array.Empty<FolderResponse>());
        using var viewModel = new MainWindowViewModel(api.Object,
            new Mock<IFileDialogService>().Object, new Mock<IUserDialogService>().Object);
        await viewModel.InitializeAsync();

        await viewModel.OpenFolderCommand.ExecuteAsync(Assert.Single(viewModel.Files));
        Assert.True(viewModel.IsError);
        await viewModel.RetryCommand.ExecuteAsync();

        Assert.Equal(folder.Id, viewModel.Breadcrumbs.Last().Id);
        Assert.False(viewModel.IsError);
        Assert.True(viewModel.IsEmpty);
        api.Verify(service => service.GetFolderContentAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(service => service.GetFolderContentAsync(folder.Id, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ExpiredSessionRaisesAnEventForTheApplicationToHandle()
    {
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.Setup(service => service.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        api.Setup(service => service.GetFolderContentAsync(null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DriveApiException(HttpStatusCode.Unauthorized, "Sign in."));
        api.Setup(service => service.GetFilesAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FileResponse>());
        api.Setup(service => service.Logout());
        using var viewModel = new MainWindowViewModel(api.Object,
            new Mock<IFileDialogService>().Object, new Mock<IUserDialogService>().Object);
        var sessionEnded = false;
        viewModel.SessionEnded += (_, _) => sessionEnded = true;

        await viewModel.InitializeAsync();

        Assert.True(sessionEnded);
        api.Verify(service => service.Logout(), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FileAppearanceUsesTheExtensionAndLocalCreationAndModificationTimes(bool wasEdited)
    {
        var edited = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        var file = new FileResponse(Guid.NewGuid(), "photo.png", 12, "hash", 1024L, null, Guid.NewGuid(),
            edited.AddDays(-1), wasEdited ? edited : null, "server-path");
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.Setup(service => service.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        api.Setup(service => service.GetFolderContentAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FolderResponse>());
        api.Setup(service => service.GetFilesAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { file });
        using var viewModel = new MainWindowViewModel(api.Object,
            new Mock<IFileDialogService>().Object, new Mock<IUserDialogService>().Object);

        await viewModel.InitializeAsync();

        var item = Assert.Single(viewModel.Files);
        Assert.Equal(SymbolRegular.Image24, item.Icon);
        var created = file.UploadedAtUtc.ToLocalTime();
        var modified = (file.EditedAtUtc ?? file.UploadedAtUtc).ToLocalTime();
        Assert.Equal(created.ToString("d"), item.DateCreated);
        Assert.Equal(created.ToString("HH:mm"), item.TimeCreated);
        Assert.Equal(created.ToString("F"), item.CreatedTimestamp);
        Assert.Equal(modified.ToString("d"), item.DateModified);
        Assert.Equal(modified.ToString("HH:mm"), item.TimeModified);
        Assert.Equal(modified.ToString("F"), item.ModifiedTimestamp);
    }
}
