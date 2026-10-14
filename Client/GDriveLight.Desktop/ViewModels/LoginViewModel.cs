using System.Net.Http;
using System.Text.Json;
using GDriveLight.Desktop.Commands;
using GDriveLight.Desktop.Services.Api;
using GDriveLight.Desktop.Services.Sync;

namespace GDriveLight.Desktop.ViewModels;

public sealed class LoginViewModel : ViewModelBase, IDisposable
{
    private readonly IDriveApiService _apiService;
    private readonly IDeviceSyncConnection? _deviceConnection;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _disposed;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _firstName = string.Empty;
    private bool _isRegisterMode;
    private bool _isLoading;
    private string _errorMessage = string.Empty;

    public LoginViewModel(IDriveApiService apiService, IDeviceSyncConnection? deviceConnection = null)
    {
        _apiService = apiService;
        _deviceConnection = deviceConnection;
        ToggleModeCommand = new RelayCommand(() =>
        {
            IsRegisterMode = !IsRegisterMode;
            ErrorMessage = string.Empty;
        }, () => !IsLoading);
        SubmitCommand = new AsyncRelayCommand(SubmitAsync, HandleError, () => !_disposed && !IsLoading);
    }

    public string Email { get => _email; set => SetProperty(ref _email, value); }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public string FirstName { get => _firstName; set => SetProperty(ref _firstName, value); }

    public bool IsRegisterMode
    {
        get => _isRegisterMode;
        set
        {
            if (SetProperty(ref _isRegisterMode, value))
            {
                OnPropertyChanged(nameof(IsLoginMode));
                OnPropertyChanged(nameof(PrimaryButtonText));
            }
        }
    }

    public bool IsLoginMode => !IsRegisterMode;
    public string PrimaryButtonText => IsLoading ? "Please wait..." : IsRegisterMode ? "Register" : "Sign In";
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(PrimaryButtonText));
                OnPropertyChanged(nameof(CanEdit));
                SubmitCommand.NotifyCanExecuteChanged();
                ToggleModeCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool CanEdit => !IsLoading;
    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public event EventHandler? LoginSucceeded;
    public RelayCommand ToggleModeCommand { get; }
    public AsyncRelayCommand SubmitCommand { get; }

    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrEmpty(Password) ||
            (IsRegisterMode && string.IsNullOrWhiteSpace(FirstName)))
        {
            ErrorMessage = "Complete all required fields.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        var tokenSet = false;
        try
        {
            var token = IsRegisterMode
                ? await _apiService.RegisterAsync(Email.Trim(), Password, FirstName.Trim(), _lifetime.Token)
                : await _apiService.LoginAsync(Email.Trim(), Password, _lifetime.Token);
            if (_disposed)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                ErrorMessage = "Sign in failed. Please check your details.";
                return;
            }

            _apiService.SetAuthToken(token);
            tokenSet = true;
            if (_deviceConnection is not null && _apiService.CurrentUserId is Guid userId)
                await _deviceConnection.InitializeAsync(userId, _lifetime.Token);
            else
                await _apiService.EnsureDeviceRegisteredAsync(_lifetime.Token);
            Password = string.Empty;
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (_disposed)
        {
        }
        catch (Exception exception)
        {
            if (tokenSet)
            {
                _apiService.Logout();
            }
            HandleError(exception);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void HandleError(Exception exception)
    {
        if (_disposed)
        {
            return;
        }

        ErrorMessage = exception switch
        {
            DeviceConnectionException => exception.Message,
            DriveApiException => exception.Message,
            ArgumentException => "Enter a valid HTTP or HTTPS server address.",
            OperationCanceledException => "The request timed out. Please try again.",
            HttpRequestException => "Could not connect to the server. Check its address and HTTPS certificate.",
            JsonException => "The server returned an invalid response.",
            _ => "Sign in could not be completed. Please try again."
        };
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
        Password = string.Empty;
    }
}
