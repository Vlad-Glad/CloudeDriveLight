namespace GDriveLight.Infrastructure.Entities;

public class DeviceEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUserEntity User { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
}
