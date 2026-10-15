namespace GDriveLight.Application.Abstractions.Services;

public interface IStorageQuotaPolicy
{
    long GetLimitBytes(Guid userId);
}
