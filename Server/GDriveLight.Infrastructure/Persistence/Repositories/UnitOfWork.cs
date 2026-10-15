using GDriveLight.Application.Abstractions.Repositories;
using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GDriveLight.Infrastructure.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _dbContext;

    public UnitOfWork(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IUserWriteTransaction> BeginUserWriteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // PostgreSQL must take a fresh snapshot after waiting for another writer.
        var isolation = _dbContext.Database.IsNpgsql() ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable;
        var transaction = await _dbContext.Database.BeginTransactionAsync(isolation, cancellationToken);
        try
        {
            if (_dbContext.Database.IsNpgsql())
            {
                var key = BinaryPrimitives.ReadInt64LittleEndian(SHA256.HashData(userId.ToByteArray()));
                await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock({key})", cancellationToken);
            }
            else if (_dbContext.Database.ProviderName != "Microsoft.EntityFrameworkCore.Sqlite")
            {
                throw new NotSupportedException("User storage transactions require PostgreSQL or SQLite.");
            }
            return new UserWriteTransaction(transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed class UserWriteTransaction(IDbContextTransaction transaction) : IUserWriteTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
