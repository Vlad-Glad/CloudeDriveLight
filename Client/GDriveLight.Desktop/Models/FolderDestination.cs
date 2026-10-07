namespace GDriveLight.Desktop.Models;

// A null ID represents Home; a null result from the dialog represents cancellation.
public sealed record FolderDestination(Guid? Id, string Name);
