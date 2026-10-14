namespace GDriveLight.Desktop.ViewModels;

public sealed record DeviceItemViewModel(Guid Id, string Name, Guid? SyncRootFolderId, string? LocalPath, bool IsCurrentDevice)
{
    public string DisplayName => IsCurrentDevice ? $"{Name} (this device)" : Name;
}
