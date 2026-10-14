using System.Net;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.ViewModels;
using Moq;

namespace GDriveLight.Desktop.Tests.ViewModels;

public sealed class FileTypeFilterTests
{
    [Fact]
    public async Task AnyCatalogTypeCanFilterFilesAndRemainAppliedAfterRefreshAndSearch()
    {
        var type = new FileTypeResponse(99, "Custom", ".custom");
        var folder = new FolderResponse(Guid.NewGuid(), "Documents", null, Guid.NewGuid());
        var matching = CreateFile("example.CUSTOM", type.Id);
        var other = CreateFile("notes.txt", 2);
        FileResponse[] files = [matching, other];
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.Setup(service => service.GetFileTypesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { type });
        api.Setup(service => service.GetFolderContentAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { folder });
        api.Setup(service => service.GetFilesAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(() => files);
        api.Setup(service => service.SearchFoldersAsync("example", It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FolderResponse>());
        api.Setup(service => service.SearchFilesAsync("example", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { matching, other });
        using var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();
        viewModel.SelectedFile = viewModel.Files.Single(file => file.Id == other.Id);

        viewModel.SelectedFileType = Assert.Single((await viewModel.LoadFileTypesAsync())!);

        Assert.Equal(new[] { folder.Id, matching.Id }, viewModel.VisibleFiles.Select(file => file.Id));
        Assert.Null(viewModel.SelectedFile);
        Assert.Contains(".custom", viewModel.FileTypeFilterLabel);
        var uploaded = CreateFile("new.custom", type.Id);
        files = [matching, other, uploaded];
        await viewModel.RefreshAsync();
        Assert.Equal(3, viewModel.VisibleFiles.Count);
        Assert.DoesNotContain(viewModel.VisibleFiles, file => file.Id == other.Id);
        Assert.Equal(type, viewModel.SelectedFileType);

        viewModel.SearchText = "example";
        await viewModel.RefreshAsync();
        Assert.Equal(matching.Id, Assert.Single(viewModel.VisibleFiles).Id);
        viewModel.SelectedFileType = null;
        Assert.Equal(2, viewModel.VisibleFiles.Count);
        Assert.Contains("All types", viewModel.FileTypeFilterLabel);
    }

    [Fact]
    public async Task EmptyFilterResultsExplainHowToRestoreAllFiles()
    {
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.Setup(service => service.GetFolderContentAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<FolderResponse>());
        api.Setup(service => service.GetFilesAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { CreateFile("notes.txt", 2) });
        using var viewModel = CreateViewModel(api);
        await viewModel.RefreshAsync();

        viewModel.SelectedFileType = new FileTypeResponse(12, "PNG", ".png");

        Assert.True(viewModel.IsEmpty);
        Assert.Equal("No matching files", viewModel.EmptyTitle);
        Assert.Contains("All types", viewModel.EmptyDescription);
        viewModel.SelectedFileType = null;
        Assert.False(viewModel.IsEmpty);
        Assert.Single(viewModel.VisibleFiles);
    }

    [Fact]
    public async Task CatalogFailureLeavesTheListingUsableAndCanBeRetried()
    {
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.SetupSequence(service => service.GetFileTypesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DriveApiException(HttpStatusCode.InternalServerError, "Please retry."))
            .ReturnsAsync(new[] { new FileTypeResponse(99, "Custom", ".custom") });
        using var viewModel = CreateViewModel(api);

        Assert.Null(await viewModel.LoadFileTypesAsync());
        Assert.True(viewModel.IsNotificationError);
        Assert.False(viewModel.IsError);
        Assert.True(viewModel.CanManageFiles);
        Assert.Equal(99, Assert.Single((await viewModel.LoadFileTypesAsync())!).Id);
    }

    private static MainWindowViewModel CreateViewModel(Mock<IDriveApiService> api) =>
        new(api.Object, new Mock<IFileDialogService>().Object, new Mock<IUserDialogService>().Object);

    private static FileResponse CreateFile(string name, int typeId) =>
        new(Guid.NewGuid(), name, typeId, "hash", 10, null, Guid.NewGuid(), DateTime.UtcNow, null, "server-path");
}
