using System.Net;
using System.Net.Http;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.ViewModels;
using Moq;

namespace GDriveLight.Desktop.Tests.ViewModels;

public sealed class StorageUsageViewModelTests
{
    private const long GiB = 1024L * 1024 * 1024;

    [Fact]
    public async Task InitialLoadDoesNotDisplayFictitiousZeroUsage()
    {
        var api = CreateApi();
        var response = new TaskCompletionSource<StorageUsageResponse>();
        api.Setup(service => service.GetStorageUsageAsync(It.IsAny<CancellationToken>())).Returns(response.Task);
        using var model = new StorageUsageViewModel(api.Object);
        var loading = model.RefreshAsync();

        Assert.True(model.ShowLoading);
        Assert.False(model.HasUsage);
        Assert.Empty(model.UsageText);
        Assert.False(model.RetryCommand.CanExecute(null));

        response.SetResult(new StorageUsageResponse(4 * GiB, GiB, 3 * GiB, false));
        await loading;
        Assert.False(model.IsLoading);
        Assert.True(model.HasUsage);
        Assert.Equal(25, model.UsedPercentage);
        Assert.Equal("1 GiB of 4 GiB used", model.UsageText);
        Assert.Equal("3 GiB available", model.AvailableText);
        Assert.False(model.HasWarning);
    }

    [Theory]
    [InlineData(0, 100, false, 0, false, false)]
    [InlineData(79, 100, false, 79, false, false)]
    [InlineData(80, 100, false, 80, true, false)]
    [InlineData(100, 100, false, 100, false, true)]
    [InlineData(140, 100, true, 100, false, true)]
    [InlineData(0, 0, false, 100, false, true)]
    public async Task ProgressIsClampedAndWarnsAtQuotaThresholds(long used, long quota, bool overQuota,
        double percentage, bool nearLimit, bool full)
    {
        var api = CreateApi();
        api.Setup(service => service.GetStorageUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageUsageResponse(quota, used, Math.Max(0, quota - used), overQuota));
        using var model = new StorageUsageViewModel(api.Object);
        await model.RefreshAsync();

        Assert.Equal(percentage, model.UsedPercentage);
        Assert.Equal(nearLimit, model.IsNearLimit);
        Assert.Equal(full, model.IsFull);
        Assert.Equal(nearLimit || full, model.HasWarning);
    }

    [Fact]
    public async Task FailedRefreshRetainsLastKnownUsageAndRetryRecovers()
    {
        var api = CreateApi();
        api.SetupSequence(service => service.GetStorageUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageUsageResponse(2 * GiB, GiB, GiB, false))
            .ThrowsAsync(new HttpRequestException("offline"))
            .ReturnsAsync(new StorageUsageResponse(2 * GiB, 0, 2 * GiB, false));
        using var model = new StorageUsageViewModel(api.Object);
        await model.RefreshAsync();
        await model.RefreshAsync();

        Assert.True(model.HasError);
        Assert.True(model.HasUsage);
        Assert.Contains("last known", model.ErrorMessage);
        Assert.Equal(50, model.UsedPercentage);

        await model.RetryCommand.ExecuteAsync();
        Assert.False(model.HasError);
        Assert.Equal(0, model.UsedPercentage);
    }

    [Fact]
    public async Task InitialFailureAndInvalidUsageNeverBecomeEmptyStorage()
    {
        var api = CreateApi();
        api.SetupSequence(service => service.GetStorageUsageAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("offline"))
            .ReturnsAsync(new StorageUsageResponse(2 * GiB, -1, GiB, false));
        using var model = new StorageUsageViewModel(api.Object);
        await model.RefreshAsync();
        Assert.True(model.HasError);
        Assert.False(model.HasUsage);
        await model.RetryCommand.ExecuteAsync();
        Assert.True(model.HasError);
        Assert.False(model.HasUsage);
    }

    [Fact]
    public async Task UnauthorizedResponseClearsUsageAndExpiresSession()
    {
        var api = CreateApi();
        api.SetupSequence(service => service.GetStorageUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageUsageResponse(2 * GiB, GiB, GiB, false))
            .ThrowsAsync(new DriveApiException(HttpStatusCode.Unauthorized, "expired"));
        using var model = new StorageUsageViewModel(api.Object);
        var expired = 0;
        model.SessionExpired += (_, _) => expired++;
        await model.RefreshAsync();
        await model.RefreshAsync();

        Assert.Equal(1, expired);
        Assert.False(model.HasUsage);
        Assert.False(model.HasError);
        Assert.False(model.IsLoading);
    }

    [Fact]
    public async Task AccountChangeClearsPreviousUsageAndIgnoresLateResponse()
    {
        var api = CreateApi();
        var oldRequest = new TaskCompletionSource<StorageUsageResponse>();
        var newRequest = new TaskCompletionSource<StorageUsageResponse>();
        api.SetupSequence(service => service.GetStorageUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageUsageResponse(2 * GiB, GiB, GiB, false))
            .Returns(oldRequest.Task)
            .Returns(newRequest.Task);
        using var model = new StorageUsageViewModel(api.Object);
        await model.RefreshAsync();
        var oldRefresh = model.RefreshAsync();
        api.SetupGet(service => service.CurrentUserId).Returns(Guid.NewGuid());
        var newRefresh = model.RefreshAsync();
        Assert.False(model.HasUsage);
        Assert.True(model.ShowLoading);

        oldRequest.SetResult(new StorageUsageResponse(2 * GiB, 2 * GiB, 0, true));
        await oldRefresh;
        Assert.False(model.HasUsage);
        Assert.True(model.ShowLoading);
        newRequest.SetResult(new StorageUsageResponse(2 * GiB, 0, 2 * GiB, false));
        await newRefresh;
        Assert.True(model.HasUsage);
        Assert.Equal(0, model.UsedPercentage);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClearOrDisposeCancelsRequestAndRejectsLateResponse(bool dispose)
    {
        var api = CreateApi();
        var response = new TaskCompletionSource<StorageUsageResponse>();
        CancellationToken requestToken = default;
        api.Setup(service => service.GetStorageUsageAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken token) => { requestToken = token; return response.Task; });
        using var model = new StorageUsageViewModel(api.Object);
        var refresh = model.RefreshAsync();
        if (dispose) model.Dispose(); else model.Clear();
        Assert.True(requestToken.IsCancellationRequested);
        response.SetResult(new StorageUsageResponse(2 * GiB, GiB, GiB, false));
        await refresh;
        Assert.False(model.HasUsage);
        Assert.False(model.IsLoading);
        if (dispose) Assert.False(model.RetryCommand.CanExecute(null));
    }

    [Fact]
    public async Task SignedOutSessionDoesNotRequestOrRetainUsage()
    {
        var api = CreateApi();
        api.Setup(service => service.GetStorageUsageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageUsageResponse(2 * GiB, GiB, GiB, false));
        using var model = new StorageUsageViewModel(api.Object);
        await model.RefreshAsync();
        api.SetupGet(service => service.CurrentUserId).Returns((Guid?)null);
        await model.RefreshAsync();
        Assert.False(model.HasUsage);
        Assert.False(model.HasError);
        api.Verify(service => service.GetStorageUsageAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IDriveApiService> CreateApi()
    {
        var api = new Mock<IDriveApiService>(MockBehavior.Strict);
        api.SetupGet(service => service.CurrentUserId).Returns(Guid.NewGuid());
        return api;
    }
}
