using System.Net;
using GDriveLight.Desktop.Models;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.ViewModels;
using Moq;

namespace GDriveLight.Desktop.Tests.ViewModels;

public sealed class FileOperationsTests
{
    [Fact]
    public async Task SearchUsesCloudResultsAndCanBeCleared()
    {
        var api = ReadableApi();
        api.Setup(service => service.SearchFoldersAsync("NOTES", It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FolderResponse>());
        api.Setup(service => service.SearchFilesAsync("NOTES", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new FileResponse(Guid.NewGuid(), "notes.txt", 2, "hash", 1024L, null, Guid.NewGuid(), DateTime.UtcNow, null, "path") });
        using var model = CreateModel(api);
        await model.InitializeAsync();
        model.SearchText = "NOTES";
        await model.RefreshAsync();
        Assert.Equal("notes.txt", Assert.Single(model.VisibleFiles).Name);
        api.Verify(service => service.SearchFilesAsync("NOTES", It.IsAny<CancellationToken>()), Times.Once);
        model.SearchText = string.Empty;
        await model.RefreshAsync();
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UploadContinuesAfterOneFailureAndReportsThePartialResult(bool dropped)
    {
        var api = ReadableApi();
        api.Setup(service => service.UploadFileAsync("one.txt", null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DriveApiException(HttpStatusCode.BadRequest, "Already exists."));
        api.Setup(service => service.UploadFileAsync("two.txt", null, It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        var files = new Mock<IFileDialogService>(MockBehavior.Strict);
        files.Setup(service => service.SelectFilesToUpload()).Returns(new[] { "one.txt", "two.txt" });
        using var model = CreateModel(api, files);
        await model.InitializeAsync();

        if (dropped)
            await model.UploadFilesCommand.ExecuteAsync(new FileUploadRequest(new[] { "one.txt", "two.txt" }, new FolderDestination(null, "Home")));
        else
            await model.UploadCommand.ExecuteAsync();

        api.Verify(service => service.UploadFileAsync("two.txt", null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(model.IsNotificationError);
        Assert.Contains("Uploaded 1 of 2", model.NotificationMessage);
        Assert.Contains("Already exists", model.NotificationMessage);
        api.Verify(service => service.GetFilesAsync(null, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BusyOperationsDisableOtherWritesAndCanBeCancelled(bool dropped)
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

        var request = new FileUploadRequest(new[] { "one.txt", "two.txt" }, new FolderDestination(null, "Home"));
        var upload = dropped ? model.UploadFilesCommand.ExecuteAsync(request) : model.UploadCommand.ExecuteAsync();
        Assert.True(model.IsOperating);
        Assert.False(model.CreateFolderCommand.CanExecute(null));
        Assert.False(model.CanNavigate);
        Assert.False(model.UploadFilesCommand.CanExecute(request));
        model.CancelOperationCommand.Execute(null);
        await upload;

        Assert.True(captured.IsCancellationRequested);
        Assert.False(model.IsOperating);
        Assert.Contains("cancelled", model.NotificationMessage);
        api.Verify(service => service.UploadFileAsync("two.txt", It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DroppedFilesUseTheChosenDestinationWithoutOpeningTheFileDialog(bool nestedFolder)
    {
        var api = ReadableApi();
        using var model = CreateModel(api);
        await model.InitializeAsync();
        var destinationId = nestedFolder ? model.Files.Single(item => item.IsFolder).Id : (Guid?)null;
        api.Setup(service => service.UploadFileAsync("one.txt", destinationId, It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());

        await model.UploadFilesCommand.ExecuteAsync(new FileUploadRequest(new[] { "one.txt" }, new FolderDestination(destinationId, "Target")));

        api.Verify(service => service.UploadFileAsync("one.txt", destinationId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Null(model.Breadcrumbs.Last().Id);
        Assert.Contains("Uploaded 1", model.NotificationMessage);
        Assert.False(model.UploadFilesCommand.CanExecute(new FileUploadRequest(Array.Empty<string>(), new FolderDestination(null, "Home"))));
    }

    [Fact]
    public async Task AFoldersDownloadCommandIsUnavailable()
    {
        var api = ReadableApi();
        using var model = CreateModel(api);
        await model.InitializeAsync();
        Assert.False(model.DownloadCommand.CanExecute(model.Files.First(item => item.IsFolder)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MovingAnItemUsesTheChosenFolderAndRefreshesTheSource(bool isFolder)
    {
        var api = ReadableApi();
        var targetId = Guid.NewGuid();
        var dialogs = new Mock<IUserDialogService>(MockBehavior.Strict);
        using var model = CreateModel(api, dialogs: dialogs);
        await model.InitializeAsync();
        var item = model.Files.Single(file => file.IsFolder == isFolder);
        dialogs.Setup(service => service.SelectDestination(null, isFolder ? item.Id : null))
            .Returns(new FolderDestination(targetId, "Target"));
        api.Setup(service => service.MoveAsync(item.Id, isFolder, targetId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await model.MoveCommand.ExecuteAsync(item);

        api.Verify(service => service.MoveAsync(item.Id, isFolder, targetId, It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(service => service.GetFilesAsync(null, It.IsAny<CancellationToken>()), Times.Exactly(2));
        Assert.Contains("Target", model.NotificationMessage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancelledMoveAndCurrentLocationDoNotSendAnApiRequest(bool cancelled)
    {
        var api = ReadableApi();
        var dialogs = new Mock<IUserDialogService>(MockBehavior.Strict);
        dialogs.Setup(service => service.SelectDestination(null, null))
            .Returns(cancelled ? null : new FolderDestination(null, "Home"));
        using var model = CreateModel(api, dialogs: dialogs);
        await model.InitializeAsync();

        await model.MoveCommand.ExecuteAsync(model.Files.Single(item => !item.IsFolder));

        api.Verify(service => service.MoveAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(model.HasNotification);
    }

    [Fact]
    public async Task MovingToHomeFromANestedFolderSendsANullDestination()
    {
        var api = ReadableApi();
        var dialogs = new Mock<IUserDialogService>(MockBehavior.Strict);
        using var model = CreateModel(api, dialogs: dialogs);
        await model.InitializeAsync();
        var parent = model.Files.Single(item => item.IsFolder);
        var file = new FileResponse(Guid.NewGuid(), "nested.txt", 2, "hash", 1024L, parent.Id, Guid.NewGuid(), DateTime.UtcNow, null, "path");
        api.Setup(service => service.GetFolderContentAsync(parent.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FolderResponse>());
        api.Setup(service => service.GetFilesAsync(parent.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { file });
        api.Setup(service => service.MoveAsync(file.Id, false, null, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        dialogs.Setup(service => service.SelectDestination(parent.Id, null)).Returns(new FolderDestination(null, "Home"));
        await model.OpenFolderCommand.ExecuteAsync(parent);

        await model.MoveCommand.ExecuteAsync(model.Files.Single());

        api.Verify(service => service.MoveAsync(file.Id, false, null, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(parent.Id, model.Breadcrumbs.Last().Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RenameUsesTheNewNameAndRefreshesForFilesAndFolders(bool isFolder)
    {
        var api = ReadableApi();
        var dialogs = new Mock<IUserDialogService>(MockBehavior.Strict);
        using var model = CreateModel(api, dialogs: dialogs);
        await model.InitializeAsync();
        var item = model.Files.Single(file => file.IsFolder == isFolder);
        dialogs.Setup(service => service.RequestName("Rename", "New name", item.IsFolder ? item.Name : System.IO.Path.GetFileNameWithoutExtension(item.Name))).Returns("Renamed.txt");
        api.Setup(service => service.RenameAsync(item.Id, isFolder, "Renamed.txt", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await model.RenameCommand.ExecuteAsync(item);

        api.Verify(service => service.RenameAsync(item.Id, isFolder, "Renamed.txt", It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(service => service.GetFilesAsync(null, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancelledRenameAndUnchangedNameDoNotSendAnApiRequest(bool cancelled)
    {
        var api = ReadableApi();
        var dialogs = new Mock<IUserDialogService>(MockBehavior.Strict);
        dialogs.Setup(service => service.RequestName("Rename", "New name", "notes")).Returns(cancelled ? null : "notes.txt");
        using var model = CreateModel(api, dialogs: dialogs);
        await model.InitializeAsync();

        await model.RenameCommand.ExecuteAsync(model.Files.Single(item => !item.IsFolder));

        api.Verify(service => service.RenameAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("move")]
    [InlineData("rename")]
    [InlineData("delete")]
    public async Task ServerRejectionIsDisplayedWithoutRemovingTheItem(string operation)
    {
        var api = ReadableApi();
        var dialogs = new Mock<IUserDialogService>(MockBehavior.Strict);
        using var model = CreateModel(api, dialogs: dialogs);
        await model.InitializeAsync();
        var item = model.Files.Single(file => !file.IsFolder);
        var rejection = new DriveApiException(HttpStatusCode.BadRequest, "Operation rejected.");
        var targetId = Guid.NewGuid();
        dialogs.Setup(service => service.SelectDestination(null, null)).Returns(new FolderDestination(targetId, "Target"));
        dialogs.Setup(service => service.RequestName("Rename", "New name", item.IsFolder ? item.Name : System.IO.Path.GetFileNameWithoutExtension(item.Name))).Returns("Other.txt");
        dialogs.Setup(service => service.ConfirmDelete(item.Name, false)).Returns(true);
        api.Setup(service => service.MoveAsync(item.Id, false, targetId, It.IsAny<CancellationToken>())).ThrowsAsync(rejection);
        api.Setup(service => service.RenameAsync(item.Id, false, "Other.txt", It.IsAny<CancellationToken>())).ThrowsAsync(rejection);
        api.Setup(service => service.DeleteAsync(item.Id, false, It.IsAny<CancellationToken>())).ThrowsAsync(rejection);

        var command = operation switch { "move" => model.MoveCommand, "rename" => model.RenameCommand, _ => model.DeleteCommand };
        await command.ExecuteAsync(item);

        Assert.Contains(item, model.Files);
        Assert.True(model.IsNotificationError);
        Assert.Equal("Operation rejected.", model.NotificationMessage);
        Assert.False(model.IsOperating);
    }

    [Fact]
    public async Task FolderDeletionUsesTheFolderEndpointAfterConfirmation()
    {
        var api = ReadableApi();
        var dialogs = new Mock<IUserDialogService>(MockBehavior.Strict);
        using var model = CreateModel(api, dialogs: dialogs);
        await model.InitializeAsync();
        var folder = model.Files.Single(item => item.IsFolder);
        dialogs.Setup(service => service.ConfirmDelete(folder.Name, true)).Returns(true);
        api.Setup(service => service.DeleteAsync(folder.Id, true, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await model.DeleteCommand.ExecuteAsync(folder);

        api.Verify(service => service.DeleteAsync(folder.Id, true, It.IsAny<CancellationToken>()), Times.Once);
        api.Verify(service => service.GetFolderContentAsync(null, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static MainWindowViewModel CreateModel(Mock<IDriveApiService> api,
        Mock<IFileDialogService>? files = null, Mock<IUserDialogService>? dialogs = null) =>
        new(api.Object, (files ?? new Mock<IFileDialogService>(MockBehavior.Strict)).Object,
            (dialogs ?? new Mock<IUserDialogService>(MockBehavior.Strict)).Object);

    private static Mock<IDriveApiService> ReadableApi()
    {
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.Setup(service => service.EnsureDeviceRegisteredAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        api.Setup(service => service.GetFolderContentAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new FolderResponse(Guid.NewGuid(), "Documents", null, Guid.NewGuid()) });
        api.Setup(service => service.GetFilesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new FileResponse(Guid.NewGuid(), "notes.txt", 2, "hash", 1024L, null, Guid.NewGuid(),
                    DateTime.UtcNow, null, "internal-path")
            });
        return api;
    }
}
