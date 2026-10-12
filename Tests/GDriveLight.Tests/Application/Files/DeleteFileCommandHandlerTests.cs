using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Files.Commands;
using GDriveLight.Domain.Enums;
using GDriveLight.Domain.Models;
using Moq;

namespace GDriveLight.Tests.Application.Files;

public class DeleteFileCommandHandlerTests
{
    private readonly Mock<IDriveFileRepository> _fileRepository = new(MockBehavior.Strict);
    private readonly Mock<IFileStorageService> _fileStorageService = new(MockBehavior.Strict);
    private readonly Mock<IChangeLogRepository> _changeLogRepository = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);

    [Fact]
    public async Task Handle_WhenFileNotFound_ReturnsFailure()
    {
        var fileId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _fileRepository.Setup(r => r.GetByIdAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveFile?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(new DeleteFileCommand(fileId, userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("was not found", result.Error);
        _fileStorageService.VerifyNoOtherCalls();
        _changeLogRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenFileOwnedByAnotherUser_ReturnsFailure()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var file = new DriveFile("doc.txt", "url", 1, new string('0', 64), 1024L, ownerId);

        _fileRepository.Setup(r => r.GetByIdAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);

        var handler = CreateHandler();
        var result = await handler.Handle(new DeleteFileCommand(file.Id, otherUserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("permission", result.Error);
        _fileStorageService.VerifyNoOtherCalls();
        _changeLogRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenValid_DeletesFromDbAndStorage()
    {
        var ownerId = Guid.NewGuid();
        var file = new DriveFile("doc.txt", "storage/doc.txt", 1, new string('0', 64), 1024L, ownerId);

        _fileRepository.Setup(r => r.GetByIdAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);
        _fileRepository.Setup(r => r.Delete(file));
        _changeLogRepository.Setup(r => r.AddAsync(It.Is<ChangeLogEntry>(e =>
                e.ResourceId == file.Id &&
                e.EventType == ChangeType.Deleted &&
                e.ResourceType == ResourceType.File), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _fileStorageService.Setup(s => s.DeleteAsync(file.FileUrl, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var result = await handler.Handle(new DeleteFileCommand(file.Id, ownerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        _fileRepository.Verify(r => r.Delete(file), Times.Once);
        _changeLogRepository.Verify(r => r.AddAsync(It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _fileStorageService.Verify(s => s.DeleteAsync(file.FileUrl, It.IsAny<CancellationToken>()), Times.Once);
    }

    private DeleteFileCommandHandler CreateHandler() =>
        new(_fileRepository.Object, _fileStorageService.Object, _changeLogRepository.Object, _unitOfWork.Object);
}
