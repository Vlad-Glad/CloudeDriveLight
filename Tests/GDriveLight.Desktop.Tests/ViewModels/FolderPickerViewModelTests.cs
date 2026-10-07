using System.Net;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.ViewModels;
using Moq;

namespace GDriveLight.Desktop.Tests.ViewModels;

public sealed class FolderPickerViewModelTests
{
    [Fact]
    public async Task SourceFolderIsExcludedAndCurrentLocationCannotBeConfirmed()
    {
        var source = Folder("Source");
        var target = Folder("Target");
        var api = Api(null, source, target);
        api.Setup(service => service.GetFolderContentAsync(target.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<FolderResponse>());
        using var model = new FolderPickerViewModel(api.Object, null, source.Id);

        Assert.False(model.ConfirmCommand.CanExecute(null));
        await model.InitializeAsync();
        Assert.Equal(target, Assert.Single(model.Folders));
        Assert.False(model.OpenFolderCommand.CanExecute(source));
        Assert.False(model.CanConfirm);
        await model.OpenFolderCommand.ExecuteAsync(target);

        Assert.True(model.CanConfirm);
        Assert.True(model.IsEmpty);
        model.ConfirmCommand.Execute(null);
        Assert.Equal(target.Id, model.Destination!.Id);
    }

    [Fact]
    public async Task ChoosingHomeReturnsADestinationWithNullIdInsteadOfACancelledResult()
    {
        var api = Api(null);
        using var model = new FolderPickerViewModel(api.Object, Guid.NewGuid(), null);
        await model.InitializeAsync();
        var closed = false;
        model.CloseRequested += (_, _) => closed = true;

        model.ConfirmCommand.Execute(null);

        Assert.True(closed);
        Assert.NotNull(model.Destination);
        Assert.Null(model.Destination.Id);
        Assert.Equal("Home", model.Destination.Name);
    }

    [Fact]
    public async Task CancelReturnsNoDestination()
    {
        using var model = new FolderPickerViewModel(Api(null).Object, Guid.NewGuid(), null);
        await model.InitializeAsync();
        var closed = false;
        model.CloseRequested += (_, _) => closed = true;

        model.CancelCommand.Execute(null);

        Assert.True(closed);
        Assert.Null(model.Destination);
    }

    [Fact]
    public async Task FailedFolderCannotBeChosenAndUserCanReturnToHome()
    {
        var target = Folder("Target");
        var api = Api(null, target);
        api.Setup(service => service.GetFolderContentAsync(target.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DriveApiException(HttpStatusCode.NotFound, "Folder was deleted."));
        using var model = new FolderPickerViewModel(api.Object, Guid.NewGuid(), null);
        await model.InitializeAsync();
        await model.OpenFolderCommand.ExecuteAsync(target);

        Assert.True(model.HasError);
        Assert.False(model.CanConfirm);
        Assert.True(model.NavigateBackCommand.CanExecute(model.Breadcrumbs.First()));
        await model.NavigateBackCommand.ExecuteAsync(model.Breadcrumbs.First());

        Assert.False(model.HasError);
        Assert.Equal("Home", model.CurrentFolderName);
        Assert.True(model.CanConfirm);
    }

    [Fact]
    public async Task RetryPreservesTheFolderAndMakesItSelectableOnlyAfterLoading()
    {
        var target = Folder("Target");
        var api = Api(null, target);
        api.SetupSequence(service => service.GetFolderContentAsync(target.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DriveApiException(HttpStatusCode.BadRequest, "Try again."))
            .ReturnsAsync(Array.Empty<FolderResponse>());
        using var model = new FolderPickerViewModel(api.Object, Guid.NewGuid(), null);
        await model.InitializeAsync();
        await model.OpenFolderCommand.ExecuteAsync(target);

        await model.RetryCommand.ExecuteAsync();

        Assert.Equal(target.Id, model.Breadcrumbs.Last().Id);
        Assert.True(model.CanConfirm);
    }

    [Fact]
    public async Task ExpiredSessionClosesThePickerAndReturnsTheErrorToTheOwner()
    {
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.Setup(service => service.GetFolderContentAsync(null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DriveApiException(HttpStatusCode.Unauthorized, "Expired."));
        using var model = new FolderPickerViewModel(api.Object, Guid.NewGuid(), null);
        var closed = false;
        model.CloseRequested += (_, _) => closed = true;

        await model.InitializeAsync();

        Assert.True(closed);
        Assert.Equal(HttpStatusCode.Unauthorized, model.SessionError!.StatusCode);
        Assert.Null(model.Destination);
        Assert.False(model.CanConfirm);
    }

    [Fact]
    public async Task ClosingThePickerCancelsLoadingAndDiscardsALateResponse()
    {
        var response = new TaskCompletionSource<IEnumerable<FolderResponse>>();
        CancellationToken captured = default;
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.Setup(service => service.GetFolderContentAsync(null, It.IsAny<CancellationToken>()))
            .Returns<Guid?, CancellationToken>((_, token) => { captured = token; return response.Task; });
        using var model = new FolderPickerViewModel(api.Object, Guid.NewGuid(), null);
        var loading = model.InitializeAsync();
        Assert.True(model.IsLoading);
        Assert.False(model.CanConfirm);

        model.Dispose();
        response.SetResult(new[] { Folder("Late folder") });
        await loading;

        Assert.True(captured.IsCancellationRequested);
        Assert.Empty(model.Folders);
        Assert.False(model.CanConfirm);
    }

    private static FolderResponse Folder(string name) => new(Guid.NewGuid(), name, null, Guid.NewGuid());

    private static Mock<IDriveApiService> Api(Guid? parentId, params FolderResponse[] folders)
    {
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.Setup(service => service.GetFolderContentAsync(parentId, It.IsAny<CancellationToken>())).ReturnsAsync(folders);
        return api;
    }
}
