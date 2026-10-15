namespace GDriveLight.Application.Abstractions.Repositories;

public interface IUserWriteTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
