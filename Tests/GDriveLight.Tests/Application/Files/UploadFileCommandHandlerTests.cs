using System.Text;
using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Files.Commands;
using GDriveLight.Domain.Enums;
using GDriveLight.Domain.Models;
using Moq;

namespace GDriveLight.Tests.Application.Files;

public class UploadFileCommandHandlerTests
{
    private readonly Mock<IDriveFileRepository> _fileRepository = new(MockBehavior.Strict);
    private readonly Mock<IDriveFolderRepository> _folderRepository = new(MockBehavior.Strict);
    private readonly Mock<IFileTypeRepository> _fileTypeRepository = new(MockBehavior.Strict);
    private readonly Mock<IFileStorageService> _fileStorageService = new(MockBehavior.Strict);
    private readonly Mock<IChangeLogRepository> _changeLogRepository = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);

    public UploadFileCommandHandlerTests()
    {
        _fileRepository.Setup(r => r.GetUsedBytesForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0L);
    }

    [Fact]
    public async Task Handle_WhenTargetFolderNotFound_ReturnsFailure()
    {
        var folderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));

        _folderRepository.Setup(r => r.GetByIdAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveFolder?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new UploadFileCommand("test.txt", stream, userId, folderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("was not found", result.Error);
        _fileStorageService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenTargetFolderOwnedByAnotherUser_ReturnsFailure()
    {
        var folderId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var folder = new DriveFolder("Folder", ownerId);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));

        _folderRepository.Setup(r => r.GetByIdAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new UploadFileCommand("test.txt", stream, otherUserId, folderId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("permission", result.Error);
        _fileStorageService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenFileTypeIsUnsupported_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));

        _fileTypeRepository.Setup(r => r.GetByExtensionAsync(".unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((FileType?)null);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new UploadFileCommand("binary.unknown", stream, userId, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not supported", result.Error);
        _fileStorageService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenFileAlreadyExists_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test"));
        var fileType = new FileType("Text", "text/plain", ".txt");

        _fileTypeRepository.Setup(r => r.GetByExtensionAsync(".txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileType);
        _fileRepository.Setup(r => r.ExistsAsync("doc.txt", null, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new UploadFileCommand("doc.txt", stream, userId, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("already exists", result.Error);
        _fileStorageService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenValid_CalculatesHashSavesFileAndAddsToRepository()
    {
        var userId = Guid.NewGuid();
        var content = "Sample file content";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var fileType = CreateFileType(1);
        var savedUrl = "storage/unique-doc.txt";

        _fileTypeRepository.Setup(r => r.GetByExtensionAsync(".txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileType);
        _fileRepository.Setup(r => r.ExistsAsync("doc.txt", null, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _fileStorageService.Setup(s => s.SaveAsync(stream, "doc.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedUrl);
        _fileRepository.Setup(r => r.AddAsync(It.Is<DriveFile>(f => f.Name == "doc.txt" && f.FileUrl == savedUrl), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _changeLogRepository.Setup(r => r.AddAsync(It.Is<ChangeLogEntry>(e =>
                e.ResourceType == ResourceType.File &&
                e.EventType == ChangeType.Created &&
                e.Name == "doc.txt" &&
                e.UserId == userId), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new UploadFileCommand("doc.txt", stream, userId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
        _fileStorageService.Verify(s => s.SaveAsync(stream, "doc.txt", It.IsAny<CancellationToken>()), Times.Once);
        _fileRepository.Verify(r => r.AddAsync(It.IsAny<DriveFile>(), It.IsAny<CancellationToken>()), Times.Once);
        _changeLogRepository.Verify(r => r.AddAsync(It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDatabaseFails_DeletesPhysicalFileAndThrows()
    {
        var userId = Guid.NewGuid();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("sample"));
        var fileType = CreateFileType(1);
        var savedUrl = "storage/orphan.txt";

        _fileTypeRepository.Setup(r => r.GetByExtensionAsync(".txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileType);
        _fileRepository.Setup(r => r.ExistsAsync("orphan.txt", null, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _fileStorageService.Setup(s => s.SaveAsync(stream, "orphan.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedUrl);
        _fileRepository.Setup(r => r.AddAsync(It.IsAny<DriveFile>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection dropped"));
        _fileStorageService.Setup(s => s.DeleteAsync(savedUrl, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new UploadFileCommand("orphan.txt", stream, userId, null), CancellationToken.None));

        _fileStorageService.Verify(s => s.DeleteAsync(savedUrl, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenOriginDeviceIdProvided_RecordsOriginDeviceIdInChangeLog()
    {
        var userId = Guid.NewGuid();
        var originDeviceId = Guid.NewGuid();
        var content = "Device upload test";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var fileType = CreateFileType(1);
        var savedUrl = "storage/device-doc.txt";

        _fileTypeRepository.Setup(r => r.GetByExtensionAsync(".txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileType);
        _fileRepository.Setup(r => r.ExistsAsync("doc.txt", null, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _fileStorageService.Setup(s => s.SaveAsync(stream, "doc.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedUrl);
        _fileRepository.Setup(r => r.AddAsync(It.IsAny<DriveFile>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _changeLogRepository.Setup(r => r.AddAsync(It.Is<ChangeLogEntry>(e =>
                e.ResourceType == ResourceType.File &&
                e.EventType == ChangeType.Created &&
                e.Name == "doc.txt" &&
                e.UserId == userId &&
                e.OriginDeviceId == originDeviceId), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();
        var command = new UploadFileCommand("doc.txt", stream, userId, null, originDeviceId);
        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _changeLogRepository.Verify(r => r.AddAsync(It.IsAny<ChangeLogEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private UploadFileCommandHandler CreateHandler() =>
        new(_fileRepository.Object, _folderRepository.Object, _fileTypeRepository.Object, _fileStorageService.Object,
            _changeLogRepository.Object, TestUserWriteTransaction.Configure(_unitOfWork),
            new GDriveLight.Application.Storage.DefaultStorageQuotaPolicy());


    private static FileType CreateFileType(int id, string extension = ".txt")
    {
        var fileType = new FileType("Text", "text/plain", extension);
        typeof(FileType).GetProperty(nameof(FileType.Id))!
            .SetValue(fileType, id);
        return fileType;
    }
}
