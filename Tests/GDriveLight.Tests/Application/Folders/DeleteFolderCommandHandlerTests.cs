using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Folders.Commands;
using GDriveLight.Domain.Enums;
using GDriveLight.Domain.Models;
using Moq;

namespace GDriveLight.Tests.Application.Folders;

public class DeleteFolderCommandHandlerTests
{
    private readonly Mock<IDriveFolderRepository> _folderRepository = new(MockBehavior.Strict);
    private readonly Mock<IFileStorageService> _fileStorageService = new(MockBehavior.Strict);
    private readonly Mock<IChangeLogRepository> _changeLogRepository = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);

    [Fact]
    public async Task Handle_WhenFolderDoesNotExist_ReturnsFailureWithoutDeleting()
    {
        var folderId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _folderRepository.Setup(r => r.GetByIdAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveFolder?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(new DeleteFolderCommand(folderId, ownerId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Folder not found", result.Error);
        _folderRepository.Verify(r => r.DeleteRecursivelyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _fileStorageService.VerifyNoOtherCalls();
        _changeLogRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenFolderBelongsToAnotherUser_ReturnsFailureWithoutDeleting()
    {
        var folderId = Guid.NewGuid();
        var actualOwnerId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();
        var folder = new DriveFolder("Secrets", actualOwnerId);

        _folderRepository.Setup(r => r.GetByIdAsync(folder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);

        var handler = CreateHandler();
        var result = await handler.Handle(new DeleteFolderCommand(folder.Id, anotherUserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("Folder not found or you do not have permission", result.Error);
        _folderRepository.Verify(r => r.DeleteRecursivelyAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _changeLogRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenFolderOwnedByUser_DeletesRecursivelyAndRemovesPhysicalFiles()
    {
        var ownerId = Guid.NewGuid();
        var folder = new DriveFolder("Temp", ownerId);
        var subFolder = new DriveFolder("Child", ownerId, folder.Id);
        var file = new DriveFile("file1.png", "storage/file1.png", 1, new string('0', 64), 1024L, ownerId, folder.Id);
        var fileUrls = new List<string> { "storage/file1.png", "storage/file2.pdf" };

        var cascadeResult = new FolderCascadeDeleteResult(
            new List<DriveFolder> { folder, subFolder },
            new List<DriveFile> { file },
            fileUrls
        );

        _folderRepository.Setup(r => r.GetByIdAsync(folder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);
        _folderRepository.Setup(r => r.DeleteRecursivelyAsync(folder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cascadeResult);
        _changeLogRepository.Setup(r => r.AddRangeAsync(It.Is<IEnumerable<ChangeLogEntry>>(entries =>
                entries.Count() == 3 &&
                entries.All(e => e.EventType == ChangeType.Deleted)), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _fileStorageService.Setup(s => s.DeleteAsync("storage/file1.png", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _fileStorageService.Setup(s => s.DeleteAsync("storage/file2.pdf", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var result = await handler.Handle(new DeleteFolderCommand(folder.Id, ownerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _folderRepository.Verify(r => r.DeleteRecursivelyAsync(folder.Id, It.IsAny<CancellationToken>()), Times.Once);
        _changeLogRepository.Verify(r => r.AddRangeAsync(It.IsAny<IEnumerable<ChangeLogEntry>>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _fileStorageService.Verify(s => s.DeleteAsync("storage/file1.png", It.IsAny<CancellationToken>()), Times.Once);
        _fileStorageService.Verify(s => s.DeleteAsync("storage/file2.pdf", It.IsAny<CancellationToken>()), Times.Once);
    }

    private DeleteFolderCommandHandler CreateHandler() =>
        new(_folderRepository.Object, _fileStorageService.Object, _changeLogRepository.Object, _unitOfWork.Object);
}
