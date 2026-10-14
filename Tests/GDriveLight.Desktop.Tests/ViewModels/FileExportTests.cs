using System.Net;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.Services.Shell;
using GDriveLight.Desktop.ViewModels;
using Moq;

namespace GDriveLight.Desktop.Tests.ViewModels;

public sealed class FileExportTests
{
    [Fact]
    public async Task InternalOrCanceledDragDoesNotDownloadOrShowSuccess()
    {
        var api = CreateApi();
        var transfer = new Mock<IVirtualFileDragOperation>(MockBehavior.Strict);
        transfer.SetupGet(value => value.IsExternalTransfer).Returns(false);
        transfer.Setup(value => value.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var drag = new Mock<IVirtualFileDragService>(MockBehavior.Strict);
        drag.Setup(value => value.BeginDrag(It.IsAny<IReadOnlyList<VirtualFile>>(), "internal", It.IsAny<CancellationToken>())).Returns(transfer.Object);
        using var viewModel = CreateViewModel(api, drag);
        await viewModel.RefreshAsync();

        await viewModel.DragFileAsync(viewModel.Files.Single(), "internal");

        Assert.False(viewModel.IsOperating);
        Assert.False(viewModel.HasNotification);
        api.Verify(value => value.DownloadFileAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        transfer.Verify(value => value.DisposeAsync(), Times.Once);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    public async Task ExternalTransferUsesExistingDownloadAndSupportsCancellation(bool cancel, int count)
    {
        var api = CreateApi(count);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transfer = new Mock<IVirtualFileDragOperation>(MockBehavior.Strict);
        transfer.SetupGet(value => value.IsExternalTransfer).Returns(true);
        transfer.Setup(value => value.DisposeAsync()).Returns(ValueTask.CompletedTask);
        transfer.Setup(value => value.CompleteAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(token => pending.Task.WaitAsync(token));
        IReadOnlyList<VirtualFile>? exported = null;
        CancellationToken exportToken = default;
        var drag = new Mock<IVirtualFileDragService>(MockBehavior.Strict);
        drag.Setup(value => value.BeginDrag(It.IsAny<IReadOnlyList<VirtualFile>>(), "internal", It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<VirtualFile>, string, CancellationToken>((files, _, token) => { exported = files; exportToken = token; })
            .Returns(transfer.Object);
        using var viewModel = CreateViewModel(api, drag);
        await viewModel.RefreshAsync();
        var items = viewModel.Files.ToArray();
        api.Setup(value => value.DownloadFileAsync(It.IsAny<Guid>(), "cache", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var operation = viewModel.DragFilesAsync(items, "internal");
        Assert.True(viewModel.IsOperating);
        Assert.False(viewModel.CanManageFiles);
        Assert.Equal(count, exported!.Count);
        Assert.Equal(items.Select(item => item.Id), exported.Select(file => file.Id));
        Assert.All(exported, file => Assert.Equal(6, file.SizeBytes));
        foreach (var file in exported) await file.DownloadAsync("cache", exportToken);
        if (cancel) viewModel.CancelOperationCommand.Execute(null);
        else pending.SetResult();
        await operation;

        Assert.False(viewModel.IsOperating);
        Assert.Equal(cancel, exportToken.IsCancellationRequested);
        Assert.Contains(cancel ? "cancelled" : "Copied", viewModel.NotificationMessage);
        transfer.Verify(value => value.DisposeAsync(), Times.Once);
        api.Verify(value => value.DeleteAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task FailedExportDisplaysAnErrorWithoutRemovingTheCloudFile()
    {
        var api = CreateApi();
        var transfer = new Mock<IVirtualFileDragOperation>();
        transfer.SetupGet(value => value.IsExternalTransfer).Returns(true);
        transfer.Setup(value => value.CompleteAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DriveApiException(HttpStatusCode.InternalServerError, "Download failed."));
        var drag = new Mock<IVirtualFileDragService>();
        drag.Setup(value => value.BeginDrag(It.IsAny<IReadOnlyList<VirtualFile>>(), "internal", It.IsAny<CancellationToken>())).Returns(transfer.Object);
        using var viewModel = CreateViewModel(api, drag);
        await viewModel.RefreshAsync();

        await viewModel.DragFileAsync(viewModel.Files.Single(), "internal");

        Assert.Single(viewModel.Files);
        Assert.True(viewModel.IsNotificationError);
        Assert.Equal("Download failed.", viewModel.NotificationMessage);
        Assert.False(viewModel.IsOperating);
    }

    private static MainWindowViewModel CreateViewModel(Mock<IDriveApiService> api, Mock<IVirtualFileDragService> drag) =>
        new(api.Object, new Mock<IFileDialogService>().Object, new Mock<IUserDialogService>().Object, virtualFileDrag: drag.Object);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FolderOnlyAndMixedSelectionsCannotStartADrag(bool mixed)
    {
        var api = CreateApi();
        var drag = new Mock<IVirtualFileDragService>(MockBehavior.Strict);
        using var viewModel = CreateViewModel(api, drag);
        await viewModel.RefreshAsync();
        var folder = new FileItemViewModel { Id = Guid.NewGuid(), Name = "Documents", IsFolder = true };
        viewModel.Files.Add(folder);
        var items = mixed ? viewModel.Files.ToArray() : new[] { folder };

        await viewModel.DragFilesAsync(items, "internal");

        drag.Verify(value => value.BeginDrag(It.IsAny<IReadOnlyList<VirtualFile>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(viewModel.IsOperating);
        Assert.False(viewModel.HasNotification);
    }

    private static Mock<IDriveApiService> CreateApi(int count = 1)
    {
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.Setup(value => value.GetFolderContentAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FolderResponse>());
        api.Setup(value => value.GetFilesAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(Enumerable.Range(0, count).Select(index =>
            new FileResponse(Guid.NewGuid(), $"notes{index}.txt", 2, "hash", 6, null, Guid.NewGuid(), DateTime.UtcNow, null, "server-path")).ToArray());
        return api;
    }
}
