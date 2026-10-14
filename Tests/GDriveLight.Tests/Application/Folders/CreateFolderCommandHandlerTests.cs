using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Folders.Commands;
using GDriveLight.Domain.Enums;
using GDriveLight.Domain.Models;
using Moq;

namespace GDriveLight.Tests.Application.Folders;

public class CreateFolderCommandHandlerTests
{
    private readonly Mock<IDriveFolderRepository> _folderRepository = new(MockBehavior.Strict);
    private readonly Mock<IChangeLogRepository> _changeLogRepository = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);

    [Fact]
    public async Task Handle_WhenFolderAlreadyExists_ReturnsFailureWithoutAdding()
    {
        var ownerId = Guid.NewGuid();
        var folderName = "Documents";
        _folderRepository.Setup(r => r.ExistsAsync(folderName, null, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var result = await handler.Handle(new CreateFolderCommand(folderName, ownerId, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("already exists", result.Error);
        _folderRepository.Verify(r => r.AddAsync(It.IsAny<DriveFolder>(), It.IsAny<CancellationToken>()), Times.Never);
        _changeLogRepository.VerifyNoOtherCalls();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenValidRootFolder_AddsFolderAndSavesChanges()
    {
        var ownerId = Guid.NewGuid();
        var folderName = "Projects";
        _folderRepository.Setup(r => r.ExistsAsync(folderName, null, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _folderRepository.Setup(r => r.AddAsync(It.Is<DriveFolder>(f => f.Name == folderName && f.OwnerId == ownerId && f.ParentFolderId == null), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _changeLogRepository.Setup(r => r.AddAsync(It.Is<ChangeLogEntry>(e =>
                e.ResourceType == ResourceType.Folder &&
                e.EventType == ChangeType.Created &&
                e.Name == folderName &&
                e.UserId == ownerId &&
                e.ParentFolderId == null), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();
        var result = await handler.Handle(new CreateFolderCommand(folderName, ownerId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
        _folderRepository.Verify(r => r.AddAsync(It.IsAny<DriveFolder>(), It.IsAny<CancellationToken>()), Times.Once);
        _changeLogRepository.Verify(r => r.AddAsync(It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenValidSubFolder_AddsFolderWithParentAndSavesChanges()
    {
        var ownerId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var folderName = "2026";
        var parentFolder = new DriveFolder("Parent", ownerId);
        _folderRepository.Setup(r => r.GetByIdAsync(parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentFolder);
        _folderRepository.Setup(r => r.ExistsAsync(folderName, parentId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _folderRepository.Setup(r => r.AddAsync(It.Is<DriveFolder>(f => f.Name == folderName && f.OwnerId == ownerId && f.ParentFolderId == parentId), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _changeLogRepository.Setup(r => r.AddAsync(It.Is<ChangeLogEntry>(e =>
                e.ResourceType == ResourceType.Folder &&
                e.EventType == ChangeType.Created &&
                e.Name == folderName &&
                e.UserId == ownerId &&
                e.ParentFolderId == parentId), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();
        var result = await handler.Handle(new CreateFolderCommand(folderName, ownerId, parentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
    }

    [Fact]
    public async Task Handle_WhenParentFolderNotFound_ReturnsFailure()
    {
        var ownerId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        _folderRepository.Setup(r => r.GetByIdAsync(parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveFolder?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(new CreateFolderCommand("Sub", ownerId, parentId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("was not found", result.Error);
        _folderRepository.Verify(r => r.AddAsync(It.IsAny<DriveFolder>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenParentFolderOwnedByAnotherUser_ReturnsFailure()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var parentFolder = new DriveFolder("Parent", otherUserId);
        _folderRepository.Setup(r => r.GetByIdAsync(parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentFolder);

        var handler = CreateHandler();
        var result = await handler.Handle(new CreateFolderCommand("Sub", ownerId, parentId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("permission", result.Error);
        _folderRepository.Verify(r => r.AddAsync(It.IsAny<DriveFolder>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOriginDeviceIdProvided_RecordsOriginDeviceIdInChangeLog()
    {
        var ownerId = Guid.NewGuid();
        var originDeviceId = Guid.NewGuid();
        var folderName = "DeviceFolder";
        _folderRepository.Setup(r => r.ExistsAsync(folderName, null, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _folderRepository.Setup(r => r.AddAsync(It.IsAny<DriveFolder>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _changeLogRepository.Setup(r => r.AddAsync(It.Is<ChangeLogEntry>(e =>
                e.ResourceType == ResourceType.Folder &&
                e.EventType == ChangeType.Created &&
                e.Name == folderName &&
                e.UserId == ownerId &&
                e.OriginDeviceId == originDeviceId), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();
        var result = await handler.Handle(new CreateFolderCommand(folderName, ownerId, null, originDeviceId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _changeLogRepository.Verify(r => r.AddAsync(It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private CreateFolderCommandHandler CreateHandler() =>
        new(_folderRepository.Object, _changeLogRepository.Object, _unitOfWork.Object);
}

