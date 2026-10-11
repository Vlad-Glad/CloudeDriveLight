using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Folders.Commands;
using GDriveLight.Domain.Models;
using Moq;

namespace GDriveLight.Tests.Application.Folders;

public class MoveFolderCommandHandlerTests
{
    private readonly Mock<IDriveFolderRepository> _folderRepository = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);

    [Fact]
    public async Task Handle_WhenFolderNotFound_ReturnsFailure()
    {
        var folderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _folderRepository.Setup(r => r.GetByIdAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveFolder?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(new MoveFolderCommand(folderId, null, userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("was not found", result.Error);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotOwnFolder_ReturnsFailure()
    {
        var folderId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var folder = new DriveFolder("Sub", ownerId);

        _folderRepository.Setup(r => r.GetByIdAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);

        var handler = CreateHandler();
        var result = await handler.Handle(new MoveFolderCommand(folderId, null, otherUserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("permission", result.Error);
    }

    [Fact]
    public async Task Handle_WhenTargetFolderNotFound_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        var folder = new DriveFolder("Sub", userId);
        var targetParentId = Guid.NewGuid();

        _folderRepository.Setup(r => r.GetByIdAsync(folder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);
        _folderRepository.Setup(r => r.GetByIdAsync(targetParentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveFolder?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(new MoveFolderCommand(folder.Id, targetParentId, userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Target parent folder", result.Error);
    }

    [Fact]
    public async Task Handle_WhenTargetFolderOwnedByAnotherUser_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        var anotherUser = Guid.NewGuid();
        var folder = new DriveFolder("Sub", userId);
        var targetParent = new DriveFolder("Target", anotherUser);

        _folderRepository.Setup(r => r.GetByIdAsync(folder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);
        _folderRepository.Setup(r => r.GetByIdAsync(targetParent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetParent);

        var handler = CreateHandler();
        var result = await handler.Handle(new MoveFolderCommand(folder.Id, targetParent.Id, userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("permission to access the target parent folder", result.Error);
    }

    [Fact]
    public async Task Handle_WhenMovingIntoOwnDescendant_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        var folder = new DriveFolder("RootFolder", userId);
        var childFolder = new DriveFolder("ChildFolder", userId, folder.Id);

        _folderRepository.Setup(r => r.GetByIdAsync(folder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);
        _folderRepository.Setup(r => r.GetByIdAsync(childFolder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(childFolder);

        var handler = CreateHandler();
        var result = await handler.Handle(new MoveFolderCommand(folder.Id, childFolder.Id, userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Cannot move a folder into its own descendant", result.Error);
    }

    [Fact]
    public async Task Handle_WhenConflictInTargetFolder_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        var folder = new DriveFolder("Duplicate", userId);
        var targetParent = new DriveFolder("Target", userId);

        _folderRepository.Setup(r => r.GetByIdAsync(folder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);
        _folderRepository.Setup(r => r.GetByIdAsync(targetParent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetParent);
        _folderRepository.Setup(r => r.ExistsAsync(folder.Name, targetParent.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var result = await handler.Handle(new MoveFolderCommand(folder.Id, targetParent.Id, userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("already exists in the target location", result.Error);
    }

    [Fact]
    public async Task Handle_WhenConflictInRoot_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        var folder = new DriveFolder("Duplicate", userId, Guid.NewGuid());

        _folderRepository.Setup(r => r.GetByIdAsync(folder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);
        _folderRepository.Setup(r => r.ExistsAsync(folder.Name, null, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var result = await handler.Handle(new MoveFolderCommand(folder.Id, null, userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("already exists in the root location", result.Error);
    }

    [Fact]
    public async Task Handle_WhenValidMoveToSubfolder_UpdatesAndSavesChanges()
    {
        var userId = Guid.NewGuid();
        var folder = new DriveFolder("MyFolder", userId);
        var targetParent = new DriveFolder("Target", userId);

        _folderRepository.Setup(r => r.GetByIdAsync(folder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);
        _folderRepository.Setup(r => r.GetByIdAsync(targetParent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetParent);
        _folderRepository.Setup(r => r.ExistsAsync(folder.Name, targetParent.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _folderRepository.Setup(r => r.Update(folder));
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();
        var result = await handler.Handle(new MoveFolderCommand(folder.Id, targetParent.Id, userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(targetParent.Id, folder.ParentFolderId);
        _folderRepository.Verify(r => r.Update(folder), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenValidMoveToRoot_UpdatesAndSavesChanges()
    {
        var userId = Guid.NewGuid();
        var oldParentId = Guid.NewGuid();
        var folder = new DriveFolder("MyFolder", userId, oldParentId);

        _folderRepository.Setup(r => r.GetByIdAsync(folder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);
        _folderRepository.Setup(r => r.ExistsAsync(folder.Name, null, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _folderRepository.Setup(r => r.Update(folder));
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();
        var result = await handler.Handle(new MoveFolderCommand(folder.Id, null, userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(folder.ParentFolderId);
        _folderRepository.Verify(r => r.Update(folder), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private MoveFolderCommandHandler CreateHandler() =>
        new(_folderRepository.Object, _unitOfWork.Object);
}
