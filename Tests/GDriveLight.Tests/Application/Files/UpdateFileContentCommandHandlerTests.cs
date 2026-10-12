using System.Text;
using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Files.Commands;
using GDriveLight.Domain.Enums;
using GDriveLight.Domain.Models;
using Moq;

namespace GDriveLight.Tests.Application.Files;

public class UpdateFileContentCommandHandlerTests
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
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("new content"));

        _fileRepository.Setup(r => r.GetByIdAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveFile?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new UpdateFileContentCommand(fileId, stream, userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("was not found", result.Error);
        _fileStorageService.VerifyNoOtherCalls();
        _changeLogRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotOwnFile_ReturnsFailure()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var file = new DriveFile("test.txt", "storage/old.txt", 1, new string('0', 64), 100L, ownerId);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("new content"));

        _fileRepository.Setup(r => r.GetByIdAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new UpdateFileContentCommand(file.Id, stream, otherUserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("permission", result.Error);
        _fileStorageService.VerifyNoOtherCalls();
        _changeLogRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenValid_UpdatesFileContentSavesNewFileDeletesOldFileAndRecordsChangeLog()
    {
        var ownerId = Guid.NewGuid();
        var oldUrl = "storage/old.txt";
        var newUrl = "storage/new.txt";
        var file = new DriveFile("test.txt", oldUrl, 1, new string('0', 64), 100L, ownerId);
        var newContent = "updated content text";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(newContent));

        _fileRepository.Setup(r => r.GetByIdAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);
        _fileStorageService.Setup(s => s.SaveAsync(stream, "test.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(newUrl);
        _fileRepository.Setup(r => r.Update(file));
        _changeLogRepository.Setup(r => r.AddAsync(It.Is<ChangeLogEntry>(e =>
                e.ResourceId == file.Id &&
                e.EventType == ChangeType.Modified &&
                e.ResourceType == ResourceType.File &&
                e.SizeBytes == stream.Length), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _fileStorageService.Setup(s => s.DeleteAsync(oldUrl, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new UpdateFileContentCommand(file.Id, stream, ownerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(newUrl, file.FileUrl);
        Assert.Equal(stream.Length, file.SizeBytes);
        _fileStorageService.Verify(s => s.SaveAsync(stream, "test.txt", It.IsAny<CancellationToken>()), Times.Once);
        _fileRepository.Verify(r => r.Update(file), Times.Once);
        _changeLogRepository.Verify(r => r.AddAsync(It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _fileStorageService.Verify(s => s.DeleteAsync(oldUrl, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDatabaseFails_DeletesNewlySavedFileAndThrows()
    {
        var ownerId = Guid.NewGuid();
        var oldUrl = "storage/old.txt";
        var newUrl = "storage/new-orphan.txt";
        var file = new DriveFile("test.txt", oldUrl, 1, new string('0', 64), 100L, ownerId);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("crash test"));

        _fileRepository.Setup(r => r.GetByIdAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);
        _fileStorageService.Setup(s => s.SaveAsync(stream, "test.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(newUrl);
        _fileRepository.Setup(r => r.Update(file));
        _changeLogRepository.Setup(r => r.AddAsync(It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB error"));
        _fileStorageService.Setup(s => s.DeleteAsync(newUrl, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new UpdateFileContentCommand(file.Id, stream, ownerId), CancellationToken.None));

        _fileStorageService.Verify(s => s.DeleteAsync(newUrl, It.IsAny<CancellationToken>()), Times.Once);
        _fileStorageService.Verify(s => s.DeleteAsync(oldUrl, It.IsAny<CancellationToken>()), Times.Never);
    }

    private UpdateFileContentCommandHandler CreateHandler() =>
        new(_fileRepository.Object, _fileStorageService.Object, _changeLogRepository.Object, _unitOfWork.Object);
}
