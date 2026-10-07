using System.Net;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.ViewModels;
using Moq;

namespace GDriveLight.Desktop.Tests.ViewModels;

public sealed class FileOperationsTests
{
    [Fact]
    public async Task SearchFiltersFilesAndFoldersWithoutMakingAnotherServerRequest()
    {
        var api = ReadableApi();
        using var model = CreateModel(api);
        await model.InitializeAsync();

        model.SearchText = "NOTES";

        Assert.Equal("notes.txt", Assert.Single(model.VisibleFiles).Name);
        Assert.Equal(2, model.Files.Count);
        api.Verify(service => service.GetFilesAsync(null, It.IsAny<CancellationToken>()), Times.Once);
        model.SearchText = "missing";
        Assert.True(model.IsEmpty);
        Assert.Equal("No matching files", model.EmptyTitle);
        model.SearchText = string.Empty;
        Assert.Equal(2, model.VisibleFiles.Count);
    }

    [Fact]
    public async Task CancellingTheNameDialogDoesNotCreateAFolder()
    {
        var api = ReadableApi();
        var dialogs = new Mock<IUserDialogService>(MockBehavior.Strict);
        dialogs.Setup(service => service.RequestName("New folder", "Folder name", "")).Returns((string?)null);
        using var model = CreateModel(api, dialogs: dialogs);
        await model.InitializeAsync();

        await model.CreateFolderCommand.ExecuteAsync();

        api.Verify(service => service.CreateFolderAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(model.IsOperating);
    }

    [Fact]
    public async Task CreatingAFolderUsesTheCurrentParentAndRefreshesTheList()
    {
        var api = ReadableApi();
        var folder = new FolderResponse(Guid.NewGuid(), "Documents", null, Guid.NewGuid());
        api.Setup(service => service.GetFolderContentAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { folder });
        api.Setup(service => service.GetFolderContentAsync(folder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FolderResponse>());
        api.Setup(service => service.GetFilesAsync(folder.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FileResponse>());
        api.Setup(service => service.CreateFolderAsync("New", folder.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        var dialogs = new Mock<IUserDialogService>(MockBehavior.Strict);
        dialogs.Setup(service => service.RequestName("New folder", "Folder name", "")).Returns("New");
        using var model = CreateModel(api, dialogs: dialogs);
        await model.InitializeAsync();
        await model.OpenFolderCommand.ExecuteAsync(model.Files.First(item => item.IsFolder));

        await model.CreateFolderCommand.ExecuteAsync();

        api.Verify(service => service.CreateFolderAsync("New", folder.Id, It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(service => service.GetFolderContentAsync(folder.Id, It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.Contains("Created", model.NotificationMessage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletionRequiresConfirmation(bool confirmed)
    {
        var api = ReadableApi();
        api.Setup(service => service.DeleteAsync(It.IsAny<Guid>(), false, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var dialogs = new Mock<IUserDialogService>(MockBehavior.Strict);
        dialogs.Setup(service => service.ConfirmDelete("notes.txt", false)).Returns(confirmed);
        using var model = CreateModel(api, dialogs: dialogs);
        await model.InitializeAsync();
        var item = model.Files.Single(file => !file.IsFolder);

        await model.DeleteCommand.ExecuteAsync(item);

        api.Verify(service => service.DeleteAsync(item.Id, false, It.IsAny<CancellationToken>()),
            confirmed ? Times.Once() : Times.Never());
        api.Verify(service => service.GetFilesAsync(null, It.IsAny<CancellationToken>()),
            confirmed ? Times.Exactly(2) : Times.Once());
    }

    [Fact]
    public async Task UploadContinuesAfterOneFailureAndReportsThePartialResult()
    {
        var api = ReadableApi();
        api.Setup(service => service.UploadFileAsync("one.txt", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DriveApiException(HttpStatusCode.BadRequest, "Already exists."));
        api.Setup(service => service.UploadFileAsync("two.txt", null, It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        var files = new Mock<IFileDialogService>(MockBehavior.Strict);
        files.Setup(service => service.SelectFilesToUpload()).Returns(new[] { "one.txt", "two.txt" });
        using var model = CreateModel(api, files);
        await model.InitializeAsync();

        await model.UploadCommand.ExecuteAsync();

        api.Verify(service => service.UploadFileAsync("two.txt", null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(model.IsNotificationError);
        Assert.Contains("Uploaded 1 of 2", model.NotificationMessage);
        Assert.Contains("Already exists", model.NotificationMessage);
        api.Verify(service => service.GetFilesAsync(null, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task BusyOperationsDisableOtherWritesAndCanBeCancelled()
    {
        var api = ReadableApi();
        CancellationToken captured = default;
        api.Setup(service => service.UploadFileAsync("one.txt", null, It.IsAny<CancellationToken>()))
            .Returns<string, Guid?, CancellationToken>(async (_, _, token) =>
            {
                captured = token;
                await Task.Delay(Timeout.Infinite, token);
                return Guid.NewGuid();
            });
        var files = new Mock<IFileDialogService>(MockBehavior.Strict);
        files.Setup(service => service.SelectFilesToUpload()).Returns(new[] { "one.txt" });
        using var model = CreateModel(api, files);
        await model.InitializeAsync();

        var upload = model.UploadCommand.ExecuteAsync();
        Assert.True(model.IsOperating);
        Assert.False(model.CreateFolderCommand.CanExecute(null));
        Assert.False(model.CanNavigate);
        model.CancelOperationCommand.Execute(null);
        await upload;

        Assert.True(captured.IsCancellationRequested);
        Assert.False(model.IsOperating);
        Assert.Contains("cancelled", model.NotificationMessage);
    }

    [Fact]
    public async Task AFoldersDownloadCommandIsUnavailable()
    {
        var api = ReadableApi();
        using var model = CreateModel(api);
        await model.InitializeAsync();
        Assert.False(model.DownloadCommand.CanExecute(model.Files.First(item => item.IsFolder)));
    }

    private static MainWindowViewModel CreateModel(Mock<IDriveApiService> api,
        Mock<IFileDialogService>? files = null, Mock<IUserDialogService>? dialogs = null) =>
        new(api.Object, (files ?? new Mock<IFileDialogService>(MockBehavior.Strict)).Object,
            (dialogs ?? new Mock<IUserDialogService>(MockBehavior.Strict)).Object);

    private static Mock<IDriveApiService> ReadableApi()
    {
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.Setup(service => service.GetFolderContentAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new FolderResponse(Guid.NewGuid(), "Documents", null, Guid.NewGuid()) });
        api.Setup(service => service.GetFilesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new FileResponse(Guid.NewGuid(), "notes.txt", 2, "hash", null, Guid.NewGuid(),
                    DateTime.UtcNow, null, "internal-path")
            });
        return api;
    }
}
