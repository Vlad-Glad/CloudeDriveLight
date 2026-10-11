using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Folders.Commands;
using GDriveLight.Domain.Models;
using Moq;

namespace GDriveLight.Tests.Application.Folders;

public class RenameFolderCommandHandlerTests
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
        var result = await handler.Handle(new RenameFolderCommand(folderId, "NewName", userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Folder not found", result.Error);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotOwnFolder_ReturnsFailure()
    {
        var folderId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var folder = new DriveFolder("OldName", ownerId);

        _folderRepository.Setup(r => r.GetByIdAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);

        var handler = CreateHandler();
        var result = await handler.Handle(new RenameFolderCommand(folderId, "NewName", otherUser), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Folder not found or you do not have permission", result.Error);
    }

    [Fact]
    public async Task Handle_WhenNameAlreadyExistsInSameLocation_ReturnsFailure()
    {
        var ownerId = Guid.NewGuid();
        var folder = new DriveFolder("OldName", ownerId);
        var newName = "ExistingName";

        _folderRepository.Setup(r => r.GetByIdAsync(folder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);
        _folderRepository.Setup(r => r.ExistsAsync(newName, folder.ParentFolderId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var result = await handler.Handle(new RenameFolderCommand(folder.Id, newName, ownerId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("already exists", result.Error);
        _folderRepository.Verify(r => r.Update(It.IsAny<DriveFolder>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenValidNewName_RenamesAndSavesChanges()
    {
        var ownerId = Guid.NewGuid();
        var folder = new DriveFolder("OldName", ownerId);
        var newName = "FreshName";

        _folderRepository.Setup(r => r.GetByIdAsync(folder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);
        _folderRepository.Setup(r => r.ExistsAsync(newName, folder.ParentFolderId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _folderRepository.Setup(r => r.Update(folder));
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();
        var result = await handler.Handle(new RenameFolderCommand(folder.Id, newName, ownerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(newName, folder.Name);
        _folderRepository.Verify(r => r.Update(folder), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private RenameFolderCommandHandler CreateHandler() =>
        new(_folderRepository.Object, _unitOfWork.Object);
}
