using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Devices.Commands.ConfigureSyncFolder;
using GDriveLight.Application.Devices.Queries;
using GDriveLight.Domain.Enums;
using GDriveLight.Domain.Models;
using Moq;

namespace GDriveLight.Tests.Application.Devices;

public class ConfigureSyncFolderTests
{
    [Fact]
    public async Task NewConfigurationCreatesIndependentDeviceRootInOneSave()
    {
        var userId = Guid.NewGuid();
        var device = new Device(userId, "Laptop");
        var devices = new Mock<IDeviceRepository>();
        var roots = new Mock<ISyncFolderRepository>();
        var folders = new Mock<IDriveFolderRepository>();
        var unit = new Mock<IUnitOfWork>();
        devices.Setup(d => d.GetByIdAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(device);
        roots.Setup(r => r.GetByDeviceIdAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<SyncFolder>());
        DriveFolder? createdRoot = null;
        SyncFolder? createdBinding = null;
        folders.Setup(f => f.AddAsync(It.IsAny<DriveFolder>(), It.IsAny<CancellationToken>())).Callback<DriveFolder, CancellationToken>((f, _) => createdRoot = f).Returns(Task.CompletedTask);
        roots.Setup(r => r.AddAsync(It.IsAny<SyncFolder>(), It.IsAny<CancellationToken>())).Callback<SyncFolder, CancellationToken>((f, _) => createdBinding = f).Returns(Task.CompletedTask);
        var handler = new ConfigureSyncFolderCommandHandler(devices.Object, roots.Object, folders.Object, unit.Object);
        var result = await handler.Handle(new(userId, device.Id, @"C:\Sync"), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(createdRoot!.Id, createdBinding!.DriveFolderId);
        Assert.Equal(device.Id, createdBinding.DeviceId);
        Assert.Equal(@"C:\Sync", result.Value.LocalPath);
        unit.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PathChangeKeepsExistingCloudTree()
    {
        var userId = Guid.NewGuid();
        var device = new Device(userId, "Laptop");
        var rootId = Guid.NewGuid();
        var binding = new SyncFolder(rootId, device.Id, SyncMode.CloudToLocal);
        var devices = new Mock<IDeviceRepository>();
        var roots = new Mock<ISyncFolderRepository>();
        var folders = new Mock<IDriveFolderRepository>(MockBehavior.Strict);
        var unit = new Mock<IUnitOfWork>();
        devices.Setup(d => d.GetByIdAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(device);
        roots.Setup(r => r.GetByDeviceIdAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { binding });
        var result = await new ConfigureSyncFolderCommandHandler(devices.Object, roots.Object, folders.Object, unit.Object)
            .Handle(new(userId, device.Id, @"D:\New"), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(rootId, result.Value.SyncRootFolderId);
        Assert.Equal(@"D:\New", binding.LocalPath);
        roots.Verify(r => r.Update(binding), Times.Once);
    }

    [Fact]
    public async Task CannotConfigureAnotherUsersDevice()
    {
        var device = new Device(Guid.NewGuid(), "Other");
        var devices = new Mock<IDeviceRepository>();
        devices.Setup(d => d.GetByIdAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(device);
        var result = await new ConfigureSyncFolderCommandHandler(devices.Object,
            new Mock<ISyncFolderRepository>(MockBehavior.Strict).Object,
            new Mock<IDriveFolderRepository>(MockBehavior.Strict).Object,
            new Mock<IUnitOfWork>(MockBehavior.Strict).Object).Handle(new(Guid.NewGuid(), device.Id, "path"), default);
        Assert.True(result.IsFailure);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyLocalPathIsRejected(string path)
    {
        Assert.False(new ConfigureSyncFolderCommandValidator().Validate(new ConfigureSyncFolderCommand(Guid.NewGuid(), Guid.NewGuid(), path)).IsValid);
    }
}
