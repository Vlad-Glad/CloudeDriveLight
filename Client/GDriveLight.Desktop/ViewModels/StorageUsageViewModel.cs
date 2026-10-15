using System.Globalization;
using System.Net;
using System.Text.Json;
using GDriveLight.Desktop.Commands;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Services.Api;

namespace GDriveLight.Desktop.ViewModels;

public sealed class StorageUsageViewModel : ViewModelBase, IDisposable
{
    private readonly IDriveApiService _apiService;
    private CancellationTokenSource? _activeRequest;
    private StorageUsageResponse? _usage;
    private Guid? _accountId;
    private bool _disposed;
    private bool _isLoading;
    private string _errorMessage = string.Empty;

    public StorageUsageViewModel(IDriveApiService apiService)
    {
        _apiService = apiService;
        RetryCommand = new AsyncRelayCommand(RefreshAsync, _ => { }, () => !_disposed && !IsLoading);
    }

    public AsyncRelayCommand RetryCommand { get; }
    public event EventHandler? SessionExpired;
    public bool IsLoading => _isLoading;
    public bool ShowLoading => IsLoading && !HasUsage;
    public bool HasUsage => _usage is not null;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public string ErrorMessage => _errorMessage;
    public double UsedPercentage => _usage is { QuotaBytes: > 0 } usage
        ? Math.Clamp(100.0 * usage.UsedBytes / usage.QuotaBytes, 0, 100)
        : IsFull ? 100 : 0;
    public bool IsFull => _usage is { } usage && (usage.IsOverQuota || usage.UsedBytes >= usage.QuotaBytes);
    public bool IsNearLimit => HasUsage && !IsFull && UsedPercentage >= 80;
    public string UsageText => _usage is { } usage
        ? $"{FormatBytes(usage.UsedBytes)} of {FormatBytes(usage.QuotaBytes)} used" : string.Empty;
    public string AvailableText => _usage is { } usage ? $"{FormatBytes(usage.AvailableBytes)} available" : string.Empty;
    public string WarningText => IsFull ? "Storage full. Delete files to free up space."
        : IsNearLimit ? "Storage is almost full." : string.Empty;
    public bool HasWarning => IsFull || IsNearLimit;

    public async Task RefreshAsync()
    {
        if (_disposed) return;
        _activeRequest?.Cancel();
        using var request = new CancellationTokenSource();
        _activeRequest = request;
        _isLoading = true;
        _errorMessage = string.Empty;
        try
        {
            var accountId = _apiService.CurrentUserId;
            if (_accountId != accountId)
            {
                _usage = null;
                _accountId = accountId;
            }

            NotifyState();
            if (accountId is null) return;
            var usage = await _apiService.GetStorageUsageAsync(request.Token);
            if (!IsCurrent(request) || _apiService.CurrentUserId != accountId) return;
            if (usage.QuotaBytes < 0 || usage.UsedBytes < 0 || usage.AvailableBytes < 0)
                throw new JsonException("Storage usage cannot be negative.");
            _usage = usage;
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!IsCurrent(request)) return;
            if (exception is DriveApiException { StatusCode: HttpStatusCode.Unauthorized })
            {
                Clear();
                SessionExpired?.Invoke(this, EventArgs.Empty);
                return;
            }

            _errorMessage = HasUsage
                ? "Could not refresh storage usage. Showing the last known values."
                : "Could not load storage usage.";
        }
        finally
        {
            if (ReferenceEquals(_activeRequest, request))
            {
                _activeRequest = null;
                _isLoading = false;
                NotifyState();
            }
        }
    }

    private bool IsCurrent(CancellationTokenSource request) =>
        !_disposed && !request.IsCancellationRequested && ReferenceEquals(_activeRequest, request);

    public void Clear()
    {
        _activeRequest?.Cancel();
        _activeRequest = null;
        _accountId = null;
        _usage = null;
        _isLoading = false;
        _errorMessage = string.Empty;
        NotifyState();
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(ShowLoading));
        OnPropertyChanged(nameof(HasUsage));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(UsedPercentage));
        OnPropertyChanged(nameof(IsFull));
        OnPropertyChanged(nameof(IsNearLimit));
        OnPropertyChanged(nameof(UsageText));
        OnPropertyChanged(nameof(AvailableText));
        OnPropertyChanged(nameof(WarningText));
        OnPropertyChanged(nameof(HasWarning));
        RetryCommand.NotifyCanExecuteChanged();
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value.ToString("0.#", CultureInfo.CurrentCulture)} {units[unit]}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Clear();
    }
}
