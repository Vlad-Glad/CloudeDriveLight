using GDriveLight.Application.Abstractions.Repositories;
using Moq;

namespace GDriveLight.Tests;

internal sealed class TestUserWriteTransaction : IUserWriteTransaction
{
    public bool Committed { get; private set; }
    public bool Disposed { get; private set; }
    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        Committed = true;
        return Task.CompletedTask;
    }
    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }

    public static IUnitOfWork Configure(Mock<IUnitOfWork> mock)
    {
        mock.Setup(u => u.BeginUserWriteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new TestUserWriteTransaction());
        return mock.Object;
    }
}
