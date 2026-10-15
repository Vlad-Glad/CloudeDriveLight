using System.Security.Cryptography;
using GDriveLight.Application.Common.Exceptions;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Files.Commands;
using GDriveLight.Application.Storage;
using GDriveLight.Infrastructure.Entities;
using GDriveLight.Infrastructure.Persistence;
using GDriveLight.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GDriveLight.Tests.Application.Files;

public sealed class StorageQuotaTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;
    private readonly Guid _user = Guid.NewGuid();
    private readonly MemoryStorage _storage = new();

    public StorageQuotaTests()
    {
        _connection.Open();
        _db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _db.Users.Add(new ApplicationUserEntity { Id = _user });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task EmptyAccountGetsTwoGiBFromServerPolicy()
    {
        var usage = await Usage();
        Assert.Equal(2147483648L, usage.QuotaBytes);
        Assert.Equal(0, usage.UsedBytes);
        Assert.Equal(usage.QuotaBytes, usage.AvailableBytes);
        Assert.False(usage.IsOverQuota);
    }

    [Fact]
    public async Task UploadAllowsExactLimitAndRecordsActualSizeAndHash()
    {
        SeedFile(DefaultStorageQuotaPolicy.LimitBytes - 3);
        var bytes = new byte[] { 1, 2, 3 };
        using var input = new MemoryStream(bytes);
        var result = await Upload().Handle(new("new.txt", input, _user, null), CancellationToken.None);
        Assert.True(result.IsSuccess);
        var saved = await _db.DriveFiles.SingleAsync(f => f.Id == result.Value);
        Assert.Equal(bytes.Length, saved.SizeBytes);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), saved.ContentHash);
        Assert.Equal(bytes, _storage.Files[saved.FileUrl]);
        Assert.Equal(0, (await Usage()).AvailableBytes);
        Assert.Single(await _db.ChangeLogs.ToListAsync());
    }

    [Fact]
    public async Task UploadOneByteOverLimitDeletesStagedContentWithoutMetadataOrEvents()
    {
        SeedFile(DefaultStorageQuotaPolicy.LimitBytes - 2);
        using var input = new MemoryStream(new byte[3]);
        var error = await Assert.ThrowsAsync<StorageQuotaExceededException>(() =>
            Upload().Handle(new("new.txt", input, _user, null), CancellationToken.None));
        Assert.Equal(3, error.RequiredBytes);
        Assert.Equal(DefaultStorageQuotaPolicy.LimitBytes - 2, error.UsedBytes);
        Assert.Empty(_storage.Files);
        Assert.Equal(1, await _db.DriveFiles.CountAsync());
        Assert.Empty(await _db.ChangeLogs.ToListAsync());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(2)]
    public async Task FullAccountCanReplaceWithSameSizeOrSmallerFile(int newSize)
    {
        var file = SeedFile(3);
        SeedFile(DefaultStorageQuotaPolicy.LimitBytes - 3);
        using var input = new MemoryStream(new byte[newSize]);
        var result = await Update().Handle(new(file.Id, input, _user), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(3 - newSize, (await Usage()).AvailableBytes);
    }

    [Fact]
    public async Task FullAccountCannotGrowExistingFileAndKeepsOldContent()
    {
        var file = SeedFile(3);
        SeedFile(DefaultStorageQuotaPolicy.LimitBytes - 3);
        using var input = new MemoryStream(new byte[4]);
        await Assert.ThrowsAsync<StorageQuotaExceededException>(() =>
            Update().Handle(new(file.Id, input, _user), CancellationToken.None));
        _db.ChangeTracker.Clear();
        var unchanged = await _db.DriveFiles.SingleAsync(f => f.Id == file.Id);
        Assert.Equal(file.FileUrl, unchanged.FileUrl);
        Assert.Equal(3, unchanged.SizeBytes);
        Assert.Empty(_storage.Files);
        Assert.Empty(await _db.ChangeLogs.ToListAsync());
    }

    [Fact]
    public async Task OverQuotaAccountCanShrinkAndUsageNeverReportsNegativeAvailability()
    {
        var file = SeedFile(DefaultStorageQuotaPolicy.LimitBytes + 10);
        var usage = await Usage();
        Assert.True(usage.IsOverQuota);
        Assert.Equal(0, usage.AvailableBytes);
        using var input = new MemoryStream(new byte[1]);
        Assert.True((await Update().Handle(new(file.Id, input, _user), CancellationToken.None)).IsSuccess);
        Assert.Equal(DefaultStorageQuotaPolicy.LimitBytes - 1, (await Usage()).AvailableBytes);
    }

    [Fact]
    public async Task NonSeekableUploadCountsAndPersistsActualBytes()
    {
        var bytes = new byte[] { 7, 8, 9, 10 };
        using var input = new NonSeekableStream(bytes);
        var result = await Upload().Handle(new("stream.txt", input, _user, null), CancellationToken.None);
        Assert.True(result.IsSuccess);
        var saved = await _db.DriveFiles.SingleAsync();
        Assert.Equal(4, saved.SizeBytes);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), saved.ContentHash);
        Assert.Equal(bytes, _storage.Files[saved.FileUrl]);
        Assert.True(input.CanRead);
    }

    [Fact]
    public async Task CancellationAfterStagingCleansUpWithoutUsingCancelledToken()
    {
        using var cancelled = new CancellationTokenSource();
        _storage.AfterSave = () => cancelled.Cancel();
        using var input = new MemoryStream(new byte[1]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Upload().Handle(new("cancelled.txt", input, _user, null), cancelled.Token));
        Assert.Empty(_storage.Files);
        Assert.Empty(await _db.DriveFiles.ToListAsync());
    }

    [Fact]
    public async Task OldContentCleanupFailureNeverDeletesCommittedReplacement()
    {
        var file = SeedFile(3);
        _storage.FailDelete = file.FileUrl;
        using var input = new MemoryStream(new byte[2]);
        var result = await Update().Handle(new(file.Id, input, _user), CancellationToken.None);
        Assert.True(result.IsSuccess);
        _db.ChangeTracker.Clear();
        var saved = await _db.DriveFiles.SingleAsync();
        Assert.NotEqual(file.FileUrl, saved.FileUrl);
        Assert.True(_storage.Files.ContainsKey(saved.FileUrl));
        Assert.Equal(2, (await Usage()).UsedBytes);
    }

    [Fact]
    public async Task UpdateRechecksCurrentSizeAfterStaging()
    {
        var file = SeedFile(10);
        SeedFile(DefaultStorageQuotaPolicy.LimitBytes - 10);
        _storage.AfterSave = () =>
        {
            _db.DriveFiles.Where(f => f.Id == file.Id).ExecuteUpdate(set => set.SetProperty(f => f.SizeBytes, 1));
        };
        using var input = new MemoryStream(new byte[3]);
        Assert.True((await Update().Handle(new(file.Id, input, _user), CancellationToken.None)).IsSuccess);
        Assert.Equal(DefaultStorageQuotaPolicy.LimitBytes - 7, (await Usage()).UsedBytes);
    }

    private DriveFileEntity SeedFile(long bytes)
    {
        var file = new DriveFileEntity
        {
            Id = Guid.NewGuid(), OwnerId = _user, Name = $"{Guid.NewGuid():N}.txt", FileTypeId = 2,
            FileUrl = Guid.NewGuid().ToString("N"), ContentHash = new string('0', 64), SizeBytes = bytes,
            UploadedAtUtc = DateTime.UtcNow
        };
        _db.DriveFiles.Add(file);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return file;
    }

    private Task<StorageUsage> Usage() => new GetStorageUsageQueryHandler(new DriveFileRepository(_db), new DefaultStorageQuotaPolicy())
        .Handle(new(_user), CancellationToken.None);

    private UploadFileCommandHandler Upload() => new(new DriveFileRepository(_db), new DriveFolderRepository(_db),
        new FileTypeRepository(_db), _storage, new ChangeLogRepository(_db), new UnitOfWork(_db), new DefaultStorageQuotaPolicy());

    private UpdateFileContentCommandHandler Update() => new(new DriveFileRepository(_db), _storage,
        new ChangeLogRepository(_db), new UnitOfWork(_db), new DefaultStorageQuotaPolicy(), NullLogger<UpdateFileContentCommandHandler>.Instance);

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private sealed class MemoryStorage : IFileStorageService
    {
        public Dictionary<string, byte[]> Files { get; } = [];
        public Action? AfterSave { get; set; }
        public string? FailDelete { get; set; }
        public async Task<string> SaveAsync(Stream source, string fileName, CancellationToken cancellationToken = default)
        {
            using var data = new MemoryStream();
            await source.CopyToAsync(data, cancellationToken);
            var key = Guid.NewGuid().ToString("N");
            Files.Add(key, data.ToArray());
            AfterSave?.Invoke();
            return key;
        }
        public Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (fileUrl == FailDelete) throw new IOException("Cleanup failed");
            Files.Remove(fileUrl);
            return Task.CompletedTask;
        }
        public Task<Stream> GetAsync(string fileUrl, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(Files[fileUrl]));
    }

    private sealed class NonSeekableStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) _inner.Dispose(); base.Dispose(disposing); }
    }
}
