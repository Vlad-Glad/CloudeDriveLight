using System.Windows.Input;
using GDriveLight.Desktop.Commands;
using GDriveLight.Desktop.Services.Api;

namespace GDriveLight.Desktop.ViewModels;

public sealed class LoginViewModel : ViewModelBase
{
    private readonly IDriveApiService _apiService;

    private string _serverUrl = "https://localhost:7220";
    public string ServerUrl
    {
        get => _serverUrl;
        set => SetProperty(ref _serverUrl, value);
    }

    private string _email = "test@example.com";
    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    private string _password = "Password123!";
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }
    
    private string _firstName = "Test User";
    public string FirstName
    {
        get => _firstName;
        set => SetProperty(ref _firstName, value);
    }

    private bool _isRegisterMode;
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
    public string PrimaryButtonText => IsRegisterMode ? "Register" : "Sign In";

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set 
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public Action? OnLoginSuccess { get; set; }

    public ICommand ToggleModeCommand { get; }
    public ICommand SubmitCommand { get; }

    public LoginViewModel(IDriveApiService apiService)
    {
        _apiService = apiService;
        
        ServerUrl = _apiService.GetServerUrl();

        ToggleModeCommand = new RelayCommand(() =>
        {
            IsRegisterMode = !IsRegisterMode;
            ErrorMessage = string.Empty;
        });

        SubmitCommand = new RelayCommand(async () => await SubmitAsync());
    }

    private async Task SubmitAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        ErrorMessage = string.Empty;

        try
        {
            _apiService.SetServerUrl(ServerUrl);

            string? token;
            if (IsRegisterMode)
            {
                token = await _apiService.RegisterAsync(Email, Password, FirstName);
            }
            else
            {
                token = await _apiService.LoginAsync(Email, Password);
            }

            if (!string.IsNullOrEmpty(token))
            {
                _apiService.SetAuthToken(token);
                OnLoginSuccess?.Invoke();
            }
            else
            {
                ErrorMessage = IsRegisterMode ? "Registration failed. Check details." : "Invalid email or password.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Connection error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
