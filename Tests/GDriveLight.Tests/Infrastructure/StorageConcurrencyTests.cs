using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Common.Exceptions;
using GDriveLight.Application.Files.Commands;
using GDriveLight.Application.Storage;
using GDriveLight.Infrastructure.Entities;
using GDriveLight.Infrastructure.Persistence;
using GDriveLight.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace GDriveLight.Tests.Infrastructure;

// Uses a unique, disposable schema. Never alters the application's tables.
public sealed class StorageConcurrencyTests : IAsyncLifetime
{
    private readonly string _schema = $"quota_test_{Guid.NewGuid():N}";
    private readonly Guid _user = Guid.NewGuid();
    private string _connectionString = "";
    private bool _created;

    public async Task InitializeAsync()
    {
        var original = Environment.GetEnvironmentVariable("GDRIVELIGHT_TEST_POSTGRES")!;
        await using var connection = new NpgsqlConnection(original);
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE SCHEMA \"{_schema}\"", connection);
        await create.ExecuteNonQueryAsync();
        _created = true;
        var options = new NpgsqlConnectionStringBuilder(original) { SearchPath = _schema, Pooling = false };
        if (options.Host == "localhost") options.Host = "127.0.0.1";
        _connectionString = options.ConnectionString;
        await using var db = Context();
        await db.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        db.Users.Add(new ApplicationUserEntity { Id = _user });
        db.DriveFiles.Add(File(DefaultStorageQuotaPolicy.LimitBytes - 1));
        await db.SaveChangesAsync();
    }

    [PostgreSqlFact]
    public async Task CompetingUploadWaitsAndRechecksCommittedUsageBeforeAcceptingBytes()
    {
        await using var first = Context();
        await first.Database.OpenConnectionAsync();
        await using var second = Context();
        await second.Database.OpenConnectionAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var transaction = await new UnitOfWork(first).BeginUserWriteAsync(_user, timeout.Token);
        first.DriveFiles.Add(File(1));
        await first.SaveChangesAsync(timeout.Token);

        var storage = new TestStorage();
        var upload = new UploadFileCommandHandler(new DriveFileRepository(second), new DriveFolderRepository(second),
            new FileTypeRepository(second), storage, new ChangeLogRepository(second), new UnitOfWork(second), new DefaultStorageQuotaPolicy());
        using var content = new MemoryStream(new byte[1]);
        var competing = upload.Handle(new("competing.txt", content, _user, null), timeout.Token);
        await Task.Delay(150, timeout.Token);
        Assert.False(competing.IsCompleted);
        await transaction.CommitAsync(timeout.Token);
        var error = await Assert.ThrowsAsync<StorageQuotaExceededException>(() => competing);
        Assert.Equal(DefaultStorageQuotaPolicy.LimitBytes, error.UsedBytes);
        Assert.True(storage.Deleted);
        Assert.Equal(DefaultStorageQuotaPolicy.LimitBytes, await new DriveFileRepository(second).GetUsedBytesForUserAsync(_user));
        Assert.Empty(await second.ChangeLogs.ToListAsync());
    }

    [PostgreSqlFact]
    public async Task DifferentUsersDoNotBlockEachOther()
    {
        await using var first = Context();
        await first.Database.OpenConnectionAsync();
        await using var second = Context();
        await second.Database.OpenConnectionAsync();
        await using var transaction = await new UnitOfWork(first).BeginUserWriteAsync(_user);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await using var independent = await new UnitOfWork(second).BeginUserWriteAsync(Guid.NewGuid(), timeout.Token);
        await independent.CommitAsync(timeout.Token);
    }

    [PostgreSqlFact]
    public async Task CancelledWaitRollsBackAndReleasesItsTransaction()
    {
        await using var first = Context();
        await first.Database.OpenConnectionAsync();
        await using var second = Context();
        await second.Database.OpenConnectionAsync();
        await using var held = await new UnitOfWork(first).BeginUserWriteAsync(_user);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new UnitOfWork(second).BeginUserWriteAsync(_user, timeout.Token));
        Assert.Null(second.Database.CurrentTransaction);
        await held.CommitAsync();
        await using var retry = await new UnitOfWork(second).BeginUserWriteAsync(_user);
        await retry.CommitAsync();
    }

    [PostgreSqlFact]
    public async Task DisposingWithoutCommitRollsBackSavedMetadata()
    {
        await using (var first = Context())
        {
            await using var transaction = await new UnitOfWork(first).BeginUserWriteAsync(_user);
            first.DriveFiles.Add(File(1));
            await first.SaveChangesAsync();
        }
        await using var verify = Context();
        Assert.Equal(DefaultStorageQuotaPolicy.LimitBytes - 1, await new DriveFileRepository(verify).GetUsedBytesForUserAsync(_user));
    }

    private AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connectionString).Options);
    private DriveFileEntity File(long size) => new()
    {
        Id = Guid.NewGuid(), OwnerId = _user, FileTypeId = 2, Name = $"{Guid.NewGuid():N}.txt",
        ContentHash = "hash", FileUrl = "test", UploadedAtUtc = DateTime.UtcNow, SizeBytes = size
    };

    public async Task DisposeAsync()
    {
        if (!_created) return;
        await using var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("GDRIVELIGHT_TEST_POSTGRES"));
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP SCHEMA \"{_schema}\" CASCADE", connection);
        await drop.ExecuteNonQueryAsync();
    }

    private sealed class TestStorage : IFileStorageService
    {
        public bool Deleted { get; private set; }
        public Task<string> SaveAsync(Stream stream, string fileName, CancellationToken cancellationToken = default) => Task.FromResult("staged");
        public Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default) { Deleted = true; return Task.CompletedTask; }
        public Task<Stream> GetAsync(string fileUrl, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GDRIVELIGHT_TEST_POSTGRES")))
            Skip = "Set GDRIVELIGHT_TEST_POSTGRES to run isolated PostgreSQL storage concurrency tests.";
    }
}
