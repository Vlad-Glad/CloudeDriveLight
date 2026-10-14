using GDriveLight.Application.Devices.Commands.DisconnectDevice;
using GDriveLight.Application.Devices.Commands.RegisterDevice;
using GDriveLight.Domain.Enums;
using GDriveLight.Infrastructure.Entities;
using GDriveLight.Infrastructure.Persistence;
using GDriveLight.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GDriveLight.Tests.Infrastructure;

public sealed class DisconnectDeviceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _otherUser = Guid.NewGuid();
    private readonly Guid _device = Guid.NewGuid();
    private readonly Guid _root = Guid.NewGuid();
    private readonly Guid _child = Guid.NewGuid();
    private readonly Guid _file = Guid.NewGuid();

    public DisconnectDeviceTests()
    {
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _db.Users.AddRange(new ApplicationUserEntity { Id = _user }, new ApplicationUserEntity { Id = _otherUser });
        _db.Devices.Add(new DeviceEntity { Id = _device, UserId = _user, Name = "Laptop" });
        _db.DriveFolders.AddRange(
            new DriveFolderEntity { Id = Guid.NewGuid(), OwnerId = _user, Name = "Documents" },
            new DriveFolderEntity { Id = _root, OwnerId = _user, Name = "Documents" },
            new DriveFolderEntity { Id = _child, OwnerId = _user, Name = "Nested", ParentFolderId = _root });
        _db.SyncFolders.Add(new SyncFolderEntity { Id = Guid.NewGuid(), DeviceId = _device, DriveFolderId = _root, LocalPath = @"C:\Local" });
        _db.DriveFiles.Add(new DriveFileEntity
        {
            Id = _file, OwnerId = _user, Name = "keep.txt", FolderId = _child, FileTypeId = 1,
            FileUrl = "storage/preserved-content", ContentHash = "original-hash", SizeBytes = 15
        });
        _db.ChangeLogs.Add(new ChangeLogEntity
        {
            ResourceId = _file, UserId = _user, OriginDeviceId = _device, ResourceType = ResourceType.File,
            EventType = ChangeType.Created, TimestampUtc = DateTime.UtcNow
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private DisconnectDeviceCommandHandler Handler() => new(new DeviceRepository(_db), new SyncFolderRepository(_db),
        new DriveFolderRepository(_db), new DriveFileRepository(_db), new ChangeLogRepository(_db), new UnitOfWork(_db));

    [Fact]
    public async Task DisconnectPreservesTheWholeTreeAndPublishesItInHome()
    {
        var cursor = await _db.ChangeLogs.MaxAsync(c => c.Id);
        Assert.True((await Handler().Handle(new(_user, _device), default)).IsSuccess);
        _db.ChangeTracker.Clear();

        Assert.Empty(await _db.Devices.ToListAsync());
        Assert.Empty(await _db.SyncFolders.ToListAsync());
        var root = await _db.DriveFolders.SingleAsync(f => f.Id == _root);
        Assert.Null(root.ParentFolderId);
        Assert.Equal("Documents (2)", root.Name);
        Assert.Equal(_root, (await _db.DriveFolders.SingleAsync(f => f.Id == _child)).ParentFolderId);
        var file = await _db.DriveFiles.SingleAsync(f => f.Id == _file);
        Assert.Equal(_child, file.FolderId);
        Assert.Equal("storage/preserved-content", file.FileUrl);
        Assert.Equal("original-hash", file.ContentHash);
        Assert.Equal(15, file.SizeBytes);
        Assert.Contains(await new DriveFolderRepository(_db).GetRootFoldersForUserAsync(_user), f => f.Id == _root);
        var events = await new ChangeLogRepository(_db).GetChangesAsync(_user, cursor);
        Assert.Equal(new[] { _root, _child, _file }, events.Select(e => e.ResourceId));
        Assert.All(events, e => { Assert.Null(e.OriginDeviceId); Assert.Equal(ChangeType.Created, e.EventType); });
        Assert.Null((await _db.ChangeLogs.SingleAsync(c => c.Id == cursor)).OriginDeviceId);
    }

    [Fact]
    public async Task RepeatingDisconnectDoesNotCreateDuplicateFoldersOrEvents()
    {
        var request = new DisconnectDeviceCommand(_user, _device);
        Assert.True((await Handler().Handle(request, default)).IsSuccess);
        _db.ChangeTracker.Clear();
        var count = await _db.ChangeLogs.CountAsync();
        Assert.True((await Handler().Handle(request, default)).IsSuccess);
        Assert.Equal(count, await _db.ChangeLogs.CountAsync());
        Assert.Equal(3, await _db.DriveFolders.CountAsync());
    }

    [Fact]
    public async Task AnotherAccountCannotDisconnectAnOwnedDevice()
    {
        Assert.True((await Handler().Handle(new(_otherUser, _device), default)).IsFailure);
        Assert.Single(await _db.Devices.ToListAsync());
        Assert.Single(await _db.SyncFolders.ToListAsync());
        Assert.Single(await _db.ChangeLogs.ToListAsync());
    }

    [Fact]
    public async Task AnotherAccountCanRegisterTheReleasedDeviceAndOldRetriesCannotReleaseIt()
    {
        await Handler().Handle(new(_user, _device), default);
        _db.ChangeTracker.Clear();
        var result = await new RegisterDeviceCommandHandler(new DeviceRepository(_db), new UnitOfWork(_db))
            .Handle(new(_device, _otherUser, "Laptop"), default);
        Assert.True(result.IsSuccess);
        _db.ChangeTracker.Clear();
        Assert.True((await Handler().Handle(new(_user, _device), default)).IsFailure);
        Assert.Equal(_otherUser, (await _db.Devices.SingleAsync()).UserId);
        Assert.Equal(_user, (await _db.DriveFolders.SingleAsync(f => f.Id == _root)).OwnerId);
    }

    [Fact]
    public async Task DatabaseFailureRollsBackFolderChangesBindingsAndNewEvents()
    {
        await _db.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reject_disconnect BEFORE DELETE ON Devices
            BEGIN SELECT RAISE(ABORT, 'Simulated database failure'); END;
            """);
        await Assert.ThrowsAsync<DbUpdateException>(() => Handler().Handle(new(_user, _device), default));
        _db.ChangeTracker.Clear();
        Assert.Single(await _db.Devices.ToListAsync());
        Assert.Single(await _db.SyncFolders.ToListAsync());
        Assert.Single(await _db.ChangeLogs.ToListAsync());
        Assert.Equal("Documents", (await _db.DriveFolders.SingleAsync(f => f.Id == _root)).Name);
        Assert.DoesNotContain(await new DriveFolderRepository(_db).GetRootFoldersForUserAsync(_user), f => f.Id == _root);
        Assert.Equal("original-hash", (await _db.DriveFiles.SingleAsync()).ContentHash);
    }

    public void Dispose() { _db.Dispose(); _connection.Dispose(); }
}
