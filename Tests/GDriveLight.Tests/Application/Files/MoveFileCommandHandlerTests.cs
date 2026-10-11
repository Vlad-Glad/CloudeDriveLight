using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Files.Commands;
using GDriveLight.Domain.Models;
using Moq;

namespace GDriveLight.Tests.Application.Files;

public class MoveFileCommandHandlerTests
{
    private readonly Mock<IDriveFileRepository> _fileRepository = new(MockBehavior.Strict);
    private readonly Mock<IDriveFolderRepository> _folderRepository = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);

    [Fact]
    public async Task Handle_WhenFileNotFound_ReturnsFailure()
    {
        var fileId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _fileRepository.Setup(r => r.GetByIdAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveFile?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(new MoveFileCommand(fileId, null, userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("was not found", result.Error);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotOwnFile_ReturnsFailure()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var file = new DriveFile("doc.txt", "url", 1, new string('0', 64), ownerId);

        _fileRepository.Setup(r => r.GetByIdAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);

        var handler = CreateHandler();
        var result = await handler.Handle(new MoveFileCommand(file.Id, null, otherUserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("permission", result.Error);
    }

    [Fact]
    public async Task Handle_WhenMovingToSameLocation_ReturnsSuccessImmediately()
    {
        var ownerId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var file = new DriveFile("doc.txt", "url", 1, new string('0', 64), ownerId, folderId);

        _fileRepository.Setup(r => r.GetByIdAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);

        var handler = CreateHandler();
        var result = await handler.Handle(new MoveFileCommand(file.Id, folderId, ownerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _fileRepository.Verify(r => r.Update(It.IsAny<DriveFile>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDestinationFolderNotFound_ReturnsFailure()
    {
        var ownerId = Guid.NewGuid();
        var file = new DriveFile("doc.txt", "url", 1, new string('0', 64), ownerId);
        var targetFolderId = Guid.NewGuid();

        _fileRepository.Setup(r => r.GetByIdAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);
        _folderRepository.Setup(r => r.GetByIdAsync(targetFolderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveFolder?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(new MoveFileCommand(file.Id, targetFolderId, ownerId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Destination folder", result.Error);
    }

    [Fact]
    public async Task Handle_WhenDestinationFolderOwnedByAnotherUser_ReturnsFailure()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var file = new DriveFile("doc.txt", "url", 1, new string('0', 64), ownerId);
        var foreignFolder = new DriveFolder("Foreign", otherUserId);

        _fileRepository.Setup(r => r.GetByIdAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);
        _folderRepository.Setup(r => r.GetByIdAsync(foreignFolder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(foreignFolder);

        var handler = CreateHandler();
        var result = await handler.Handle(new MoveFileCommand(file.Id, foreignFolder.Id, ownerId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("permission", result.Error);
    }

    [Fact]
    public async Task Handle_WhenNameConflictInDestinationFolder_ReturnsFailure()
    {
        var ownerId = Guid.NewGuid();
        var file = new DriveFile("doc.txt", "url", 1, new string('0', 64), ownerId);
        var targetFolder = new DriveFolder("Target", ownerId);

        _fileRepository.Setup(r => r.GetByIdAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);
        _folderRepository.Setup(r => r.GetByIdAsync(targetFolder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetFolder);
        _fileRepository.Setup(r => r.ExistsAsync(file.Name, targetFolder.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var result = await handler.Handle(new MoveFileCommand(file.Id, targetFolder.Id, ownerId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("already exists", result.Error);
    }

    [Fact]
    public async Task Handle_WhenValidMoveToFolder_UpdatesFileAndSavesChanges()
    {
        var ownerId = Guid.NewGuid();
        var file = new DriveFile("doc.txt", "url", 1, new string('0', 64), ownerId);
        var targetFolder = new DriveFolder("Target", ownerId);

        _fileRepository.Setup(r => r.GetByIdAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);
        _folderRepository.Setup(r => r.GetByIdAsync(targetFolder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetFolder);
        _fileRepository.Setup(r => r.ExistsAsync(file.Name, targetFolder.Id, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _fileRepository.Setup(r => r.Update(file));
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();
        var result = await handler.Handle(new MoveFileCommand(file.Id, targetFolder.Id, ownerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(targetFolder.Id, file.FolderId);
        _fileRepository.Verify(r => r.Update(file), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private MoveFileCommandHandler CreateHandler() =>
        new(_fileRepository.Object, _folderRepository.Object, _unitOfWork.Object);
}
