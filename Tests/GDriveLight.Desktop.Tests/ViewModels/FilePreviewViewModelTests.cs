using System.Net;
using System.Net.Http;
using System.Text;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.Services.Preview;
using GDriveLight.Desktop.Tests.Services;
using GDriveLight.Desktop.ViewModels;
using Moq;

namespace GDriveLight.Desktop.Tests.ViewModels;

public sealed class FilePreviewViewModelTests
{
    [Theory]
    [InlineData("Example.cs", false)]
    [InlineData("photo.jpeg", true)]
    public async Task PriorityFormatsHaveNativePreviews(string name, bool image)
    {
        var file = File(name);
        var api = CreateApi();
        api.Setup(service => service.GetFilePreviewContentAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(image ? PreviewContentDecoderTests.CreateImage(true) : Encoding.UTF8.GetBytes("class Example {}"));
        using var model = CreateModel(api, [file]);
        await model.LoadAsync();

        Assert.False(model.IsLoading);
        Assert.Equal(image, model.HasImage);
        Assert.Equal(!image, model.HasText);
        Assert.False(model.ShowMessage);
        if (!image) Assert.Equal("class Example {}", model.Text);
        else
        {
            Assert.True(model.Image!.IsFrozen);
            model.ZoomInCommand.Execute(null);
            Assert.True(model.Zoom > 1);
            model.FitCommand.Execute(null);
            Assert.Equal(1, model.Zoom);
        }
    }

    [Theory]
    [InlineData("document.pdf", 1)]
    [InlineData("photo.jpg", FilePreviewPolicy.MaxPreviewBytes + 1L)]
    public async Task UnsupportedAndOversizedFilesOfferDownloadWithoutReadingTheBody(string name, long size)
    {
        var file = File(name, size);
        var api = CreateApi();
        using var model = CreateModel(api, [file]);
        await model.LoadAsync();

        Assert.True(model.ShowMessage);
        Assert.False(model.CanRetry);
        Assert.Contains("Download", model.Message);
        Assert.True(model.DownloadCommand.CanExecute(null));
        api.Verify(service => service.GetFilePreviewContentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NetworkFailureCanBeRetriedWithoutClosingThePreview()
    {
        var file = File("code.cs");
        var api = CreateApi();
        api.SetupSequence(service => service.GetFilePreviewContentAsync(file.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("offline"))
            .ReturnsAsync(Encoding.UTF8.GetBytes("// recovered"));
        using var model = CreateModel(api, [file]);
        await model.LoadAsync();
        Assert.True(model.CanRetry);
        Assert.True(model.ShowMessage);
        await model.RetryCommand.ExecuteAsync();
        Assert.True(model.HasText);
        Assert.Equal("// recovered", model.Text);
    }

    [Fact]
    public async Task SwitchingFilesCancelsTheOldRequestAndIgnoresItsLateResponse()
    {
        var first = File("first.cs");
        var second = File("second.cs");
        var api = CreateApi();
        var oldResponse = new TaskCompletionSource<byte[]>();
        CancellationToken oldToken = default;
        api.Setup(service => service.GetFilePreviewContentAsync(first.Id, It.IsAny<CancellationToken>()))
            .Callback<Guid, CancellationToken>((_, token) => oldToken = token).Returns(oldResponse.Task);
        api.Setup(service => service.GetFilePreviewContentAsync(second.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("// second"));
        using var model = CreateModel(api, [first, second]);
        var oldLoad = model.LoadAsync();
        var ready = new TaskCompletionSource();
        model.PropertyChanged += (_, _) => { if (model.HasText) ready.TrySetResult(); };
        model.NextCommand.Execute(null);
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(oldToken.IsCancellationRequested);
        oldResponse.SetResult(Encoding.UTF8.GetBytes("// stale"));
        await oldLoad;
        Assert.Equal(second, model.CurrentFile);
        Assert.Equal("// second", model.Text);
        Assert.Equal("2 / 2", model.PositionText);
        Assert.False(model.NextCommand.CanExecute(null));
        Assert.True(model.PreviousCommand.CanExecute(null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosingOrChangingAccountPreventsLateContentFromBeingDisplayed(bool dispose)
    {
        var file = File("code.cs");
        var api = CreateApi();
        var response = new TaskCompletionSource<byte[]>();
        api.Setup(service => service.GetFilePreviewContentAsync(file.Id, It.IsAny<CancellationToken>())).Returns(response.Task);
        using var model = CreateModel(api, [file]);
        var loading = model.LoadAsync();
        if (dispose) model.Dispose();
        else api.SetupGet(service => service.CurrentUserId).Returns(Guid.NewGuid());
        response.SetResult(Encoding.UTF8.GetBytes("// private content"));
        await loading;
        Assert.False(model.HasText);
        Assert.False(model.HasImage);
        if (dispose) Assert.False(model.DownloadCommand.CanExecute(null));
    }

    [Fact]
    public async Task ExpiredSessionClosesContentAndSignalsTheMainWindow()
    {
        var file = File("code.cs");
        var api = CreateApi();
        api.Setup(service => service.GetFilePreviewContentAsync(file.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DriveApiException(HttpStatusCode.Unauthorized, "expired"));
        using var model = CreateModel(api, [file]);
        var expired = false;
        model.SessionExpired += (_, _) => expired = true;
        await model.LoadAsync();
        Assert.True(expired);
        Assert.False(model.DownloadCommand.CanExecute(null));
        Assert.Empty(model.Text);
    }

    [Fact]
    public async Task UnsupportedFilesCanBeDownloadedToTheSelectedDestination()
    {
        var file = File("document.pdf");
        var api = CreateApi();
        var dialogs = new Mock<IFileDialogService>(MockBehavior.Strict);
        dialogs.Setup(service => service.SelectDownloadDestination(file.Name)).Returns("selected-file.pdf");
        api.Setup(service => service.DownloadFileAsync(file.Id, "selected-file.pdf", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        using var model = new FilePreviewViewModel(api.Object, dialogs.Object, [file], file.Id);
        await model.LoadAsync();
        await model.DownloadCommand.ExecuteAsync();
        api.Verify(service => service.DownloadFileAsync(file.Id, "selected-file.pdf", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains("Downloaded", model.DownloadStatus);
    }

    private static FileItemViewModel File(string name, long size = 100) => new() { Id = Guid.NewGuid(), Name = name, SizeBytes = size };
    private static Mock<IDriveApiService> CreateApi()
    {
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.SetupGet(service => service.CurrentUserId).Returns(Guid.NewGuid());
        return api;
    }
    private static FilePreviewViewModel CreateModel(Mock<IDriveApiService> api, FileItemViewModel[] files) =>
        new(api.Object, new Mock<IFileDialogService>().Object, files, files[0].Id);
}
