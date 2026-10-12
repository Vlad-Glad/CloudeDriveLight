using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Files.Commands;
using GDriveLight.Domain.Models;
using Moq;

namespace GDriveLight.Tests.Application.Files;

public class RenameFileCommandHandlerTests
{
    private readonly Mock<IDriveFileRepository> _fileRepository = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);

    [Fact]
    public async Task Handle_WhenFileNotFound_ReturnsFailure()
    {
        var fileId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _fileRepository.Setup(r => r.GetByIdAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveFile?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(new RenameFileCommand(fileId, "new.txt", userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("was not found", result.Error);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotOwnFile_ReturnsFailure()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var file = new DriveFile("old.txt", "url", 1, new string('0', 64), 1024L, ownerId);

        _fileRepository.Setup(r => r.GetByIdAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);

        var handler = CreateHandler();
        var result = await handler.Handle(new RenameFileCommand(file.Id, "new.txt", otherUserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("permission", result.Error);
    }

    [Fact]
    public async Task Handle_WhenNameUnchanged_ReturnsSuccessImmediately()
    {
        var ownerId = Guid.NewGuid();
        var file = new DriveFile("same.txt", "url", 1, new string('0', 64), 1024L, ownerId);

        _fileRepository.Setup(r => r.GetByIdAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);

        var handler = CreateHandler();
        var result = await handler.Handle(new RenameFileCommand(file.Id, "same.txt", ownerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _fileRepository.Verify(r => r.Update(It.IsAny<DriveFile>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenFileNameConflictInSameFolder_ReturnsFailure()
    {
        var ownerId = Guid.NewGuid();
        var file = new DriveFile("old.txt", "url", 1, new string('0', 64), 1024L, ownerId);

        _fileRepository.Setup(r => r.GetByIdAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);
        _fileRepository.Setup(r => r.ExistsAsync("taken.txt", file.FolderId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var result = await handler.Handle(new RenameFileCommand(file.Id, "taken.txt", ownerId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("already exists", result.Error);
    }

    [Fact]
    public async Task Handle_WhenValidNewName_RenamesFileAndSavesChanges()
    {
        var ownerId = Guid.NewGuid();
        var file = new DriveFile("old.txt", "url", 1, new string('0', 64), 1024L, ownerId);

        _fileRepository.Setup(r => r.GetByIdAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);
        _fileRepository.Setup(r => r.ExistsAsync("new.txt", file.FolderId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _fileRepository.Setup(r => r.Update(file));
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();
        var result = await handler.Handle(new RenameFileCommand(file.Id, "new.txt", ownerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("new.txt", file.Name);
        _fileRepository.Verify(r => r.Update(file), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private RenameFileCommandHandler CreateHandler() =>
        new(_fileRepository.Object, _unitOfWork.Object);
}
