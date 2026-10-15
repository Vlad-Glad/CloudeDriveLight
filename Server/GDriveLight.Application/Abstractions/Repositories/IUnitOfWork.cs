namespace GDriveLight.Application.Abstractions.Repositories;

public interface IUnitOfWork
{
    Task<IUserWriteTransaction> BeginUserWriteAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
