using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Windows.Media.Imaging;
using GDriveLight.Desktop.Commands;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.Services.Preview;

namespace GDriveLight.Desktop.ViewModels;

public sealed class FilePreviewViewModel : ViewModelBase, IDisposable
{
    private readonly IDriveApiService _apiService;
    private readonly IFileDialogService _fileDialogs;
    private readonly FileItemViewModel[] _files;
    private readonly Guid? _accountId;
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _activeLoad;
    private int _index;
    private bool _disposed;
    private bool _isLoading;
    private bool _canRetry;
    private BitmapSource? _image;
    private string? _text;
    private string _message = string.Empty;
    private string _downloadStatus = string.Empty;
    private double _zoom = 1;

    public FilePreviewViewModel(IDriveApiService apiService, IFileDialogService fileDialogs,
        IEnumerable<FileItemViewModel> files, Guid initialId)
    {
        _apiService = apiService;
        _fileDialogs = fileDialogs;
        _accountId = apiService.CurrentUserId;
        _files = files.Where(file => !file.IsFolder).ToArray();
        _index = Array.FindIndex(_files, file => file.Id == initialId);
        if (_index < 0) throw new ArgumentException("The preview file must be in the file list.", nameof(initialId));
        PreviousCommand = new RelayCommand(() => Navigate(-1), () => !_disposed && _index > 0);
        NextCommand = new RelayCommand(() => Navigate(1), () => !_disposed && _index < _files.Length - 1);
        RetryCommand = new AsyncRelayCommand(LoadAsync, _ => { }, () => !_disposed && !_isLoading && _canRetry);
        DownloadCommand = new AsyncRelayCommand(DownloadAsync, _ => { }, () => !_disposed);
        ZoomInCommand = new RelayCommand(() => SetZoom(_zoom * 1.25), () => !_disposed && HasImage && _zoom < 8);
        ZoomOutCommand = new RelayCommand(() => SetZoom(_zoom / 1.25), () => !_disposed && HasImage && _zoom > 0.25);
        FitCommand = new RelayCommand(() => SetZoom(1), () => !_disposed && HasImage);
    }

    public event EventHandler? SessionExpired;
    public FileItemViewModel CurrentFile => _files[_index];
    public string Title => $"{CurrentFile.Name} — Preview";
    public string PositionText => $"{_index + 1} / {_files.Length}";
    public bool IsLoading => _isLoading;
    public BitmapSource? Image => _image;
    public string Text => _text ?? string.Empty;
    public bool HasImage => _image is not null;
    public bool HasText => _text is not null;
    public bool ShowMessage => !_isLoading && !HasImage && !HasText;
    public string Message => _message;
    public bool CanRetry => !_isLoading && _canRetry;
    public string DownloadStatus => _downloadStatus;
    public double Zoom => _zoom;
    public string ZoomText => Math.Abs(_zoom - 1) < 0.001 ? "Fit" : $"{_zoom:P0} of fit";
    public RelayCommand PreviousCommand { get; }
    public RelayCommand NextCommand { get; }
    public AsyncRelayCommand RetryCommand { get; }
    public AsyncRelayCommand DownloadCommand { get; }
    public RelayCommand ZoomInCommand { get; }
    public RelayCommand ZoomOutCommand { get; }
    public RelayCommand FitCommand { get; }

    private void Navigate(int offset)
    {
        _index += offset;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        if (_disposed) return;
        _activeLoad?.Cancel();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _activeLoad = request;
        var file = CurrentFile;
        _isLoading = true;
        _canRetry = false;
        _image = null;
        _text = null;
        _zoom = 1;
        _message = string.Empty;
        NotifyState();
        try
        {
            if (_accountId is null || _apiService.CurrentUserId != _accountId)
            {
                _message = "Your session has changed. Close this preview and sign in again.";
                return;
            }

            var kind = FilePreviewPolicy.GetKind(file.Name);
            if (kind == FilePreviewKind.Unsupported)
            {
                _message = "Preview is not available for this file type yet. Download it to open it in another application.";
                return;
            }
            if (file.SizeBytes > FilePreviewPolicy.MaxPreviewBytes)
                throw new PreviewUnavailableException("Preview is limited to 10 MiB. Download this file to view it.");

            var bytes = await _apiService.GetFilePreviewContentAsync(file.Id, request.Token);
            if (!IsCurrent(request)) return;
            if (bytes.Length > FilePreviewPolicy.MaxPreviewBytes)
                throw new PreviewUnavailableException("Preview is limited to 10 MiB. Download this file to view it.");
            var content = await Task.Run(() => kind == FilePreviewKind.Image
                ? (Image: PreviewContentDecoder.DecodeImage(bytes), Text: (string?)null)
                : (Image: (BitmapSource?)null, Text: PreviewContentDecoder.DecodeText(bytes)), request.Token);
            if (!IsCurrent(request)) return;
            if (_apiService.CurrentUserId != _accountId)
            {
                _message = "Your session has changed. Close this preview and sign in again.";
                return;
            }
            _image = content.Image;
            _text = content.Text;
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (!IsCurrent(request)) return;
            if (_apiService.CurrentUserId != _accountId)
            {
                _message = "Your session has changed. Close this preview and sign in again.";
                return;
            }
            if (exception is DriveApiException { StatusCode: HttpStatusCode.Unauthorized })
            {
                Dispose();
                SessionExpired?.Invoke(this, EventArgs.Empty);
                return;
            }
            _canRetry = exception is not PreviewUnavailableException;
            _message = exception switch
            {
                PreviewUnavailableException => exception.Message,
                DriveApiException { StatusCode: HttpStatusCode.Forbidden } => "You do not have access to this file.",
                DriveApiException { StatusCode: HttpStatusCode.NotFound or HttpStatusCode.BadRequest } => "This file is no longer available.",
                HttpRequestException => "Could not load the preview. Check your connection and try again.",
                DecoderFallbackException => "This text encoding is not supported. Use UTF-8 or Unicode with a byte order mark, or download the file.",
                _ => "Could not preview this file. It may be damaged or use an unsupported format. You can still download it."
            };
        }
        finally
        {
            if (ReferenceEquals(_activeLoad, request))
            {
                _activeLoad = null;
                _isLoading = false;
                NotifyState();
            }
        }
    }

    private bool IsCurrent(CancellationTokenSource request) =>
        !_disposed && !request.IsCancellationRequested && ReferenceEquals(_activeLoad, request);

    private async Task DownloadAsync()
    {
        if (_accountId is null || _apiService.CurrentUserId != _accountId)
        {
            _downloadStatus = "Your session has changed. Close this preview and sign in again.";
            OnPropertyChanged(nameof(DownloadStatus));
            return;
        }
        var file = CurrentFile;
        var destination = _fileDialogs.SelectDownloadDestination(file.Name);
        if (destination is null) return;
        _downloadStatus = $"Downloading {file.Name}…";
        OnPropertyChanged(nameof(DownloadStatus));
        try
        {
            await _apiService.DownloadFileAsync(file.Id, destination, _lifetime.Token);
            if (!_disposed) _downloadStatus = $"Downloaded {file.Name}.";
        }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception)
        {
            if (_disposed) return;
            if (_apiService.CurrentUserId == _accountId && exception is DriveApiException { StatusCode: HttpStatusCode.Unauthorized })
            {
                Dispose();
                SessionExpired?.Invoke(this, EventArgs.Empty);
                return;
            }
            _downloadStatus = "Could not download the file. Check your connection and try again.";
        }
        finally { OnPropertyChanged(nameof(DownloadStatus)); }
    }

    private void SetZoom(double zoom)
    {
        _zoom = Math.Clamp(zoom, 0.25, 8);
        NotifyState();
    }

    private void NotifyState()
    {
        foreach (var property in new[] { nameof(CurrentFile), nameof(Title), nameof(PositionText), nameof(IsLoading),
                     nameof(Image), nameof(Text), nameof(HasImage), nameof(HasText), nameof(ShowMessage), nameof(Message),
                     nameof(CanRetry), nameof(Zoom), nameof(ZoomText) }) OnPropertyChanged(property);
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        RetryCommand.NotifyCanExecuteChanged();
        DownloadCommand.NotifyCanExecuteChanged();
        ZoomInCommand.NotifyCanExecuteChanged();
        ZoomOutCommand.NotifyCanExecuteChanged();
        FitCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
        _activeLoad = null;
        _image = null;
        _text = null;
        _isLoading = false;
        NotifyState();
    }
}
