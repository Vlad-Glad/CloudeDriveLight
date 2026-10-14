using System.Globalization;
using System.Windows.Media;
using GDriveLight.Desktop.Services;
using GDriveLight.Desktop.Services.Api;

namespace GDriveLight.Desktop.ViewModels;

public sealed class UserProfileViewModel : ViewModelBase, IDisposable
{
    private readonly IAvatarService _avatarService;
    private readonly CancellationTokenSource _lifetime = new();
    private ImageSource? _avatar;
    private bool _disposed;
    private bool _loadStarted;

    public UserProfileViewModel(IDriveApiService apiService, IAvatarService avatarService)
    {
        _avatarService = avatarService;
        var user = apiService.CurrentUser;
        var firstName = user?.FirstName.Trim() ?? string.Empty;
        FirstName = firstName.Length > 0 ? firstName : "Your account";
        Email = user?.Email.Trim() ?? string.Empty;
        Initial = firstName.Length > 0 ? StringInfo.GetNextTextElement(firstName).ToUpperInvariant() : "?";
    }

    public string FirstName { get; }
    public string Email { get; }
    public string Initial { get; }
    public ImageSource? Avatar { get => _avatar; private set => SetProperty(ref _avatar, value); }

    public async Task LoadAvatarAsync()
    {
        if (_disposed || _loadStarted || string.IsNullOrWhiteSpace(Email)) return;
        _loadStarted = true;
        try
        {
            var avatar = await _avatarService.GetAvatarAsync(Email, _lifetime.Token);
            if (!_disposed) Avatar = avatar;
        }
        catch (OperationCanceledException) when (_disposed)
        {
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
        Avatar = null;
    }
}
