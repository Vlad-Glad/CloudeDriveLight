using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Files.Queries;
using GDriveLight.Domain.Models;
using Moq;

namespace GDriveLight.Tests.Application.Files;

public class DownloadFileQueryHandlerTests
{
    private readonly Mock<IDriveFileRepository> _fileRepository = new(MockBehavior.Strict);
    private readonly Mock<IFileTypeRepository> _fileTypeRepository = new(MockBehavior.Strict);
    private readonly Mock<IFileStorageService> _fileStorageService = new(MockBehavior.Strict);

    [Fact]
    public async Task Handle_WhenFileDoesNotExist_ReturnsFailureWithoutAccessingStorage()
    {
        var fileId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var cancellationToken = cancellation.Token;
        _fileRepository.Setup(repository => repository.GetByIdAsync(fileId, cancellationToken))
            .ReturnsAsync((DriveFile?)null);

        var result = await CreateHandler().Handle(new DownloadFileQuery(fileId, userId), cancellationToken);

        Assert.True(result.IsFailure);
        _fileStorageService.VerifyNoOtherCalls();
        _fileTypeRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenFileBelongsToAnotherUser_ReturnsFailureWithoutAccessingStorage()
    {
        var file = CreateFile(Guid.NewGuid());
        var requestingUserId = Guid.NewGuid();
        _fileRepository.Setup(repository => repository.GetByIdAsync(file.Id, CancellationToken.None))
            .ReturnsAsync(file);

        var result = await CreateHandler().Handle(
            new DownloadFileQuery(file.Id, requestingUserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        _fileStorageService.VerifyNoOtherCalls();
        _fileTypeRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_WhenPhysicalFileIsMissing_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        var file = CreateFile(userId);
        _fileRepository.Setup(repository => repository.GetByIdAsync(file.Id, CancellationToken.None))
            .ReturnsAsync(file);
        _fileTypeRepository.Setup(repository => repository.GetByIdAsync(file.FileTypeId, CancellationToken.None))
            .ReturnsAsync(new FileType("Text", "text/plain", ".txt"));
        _fileStorageService.Setup(storage => storage.GetAsync(file.FileUrl, CancellationToken.None))
            .ThrowsAsync(new FileNotFoundException());

        var result = await CreateHandler().Handle(
            new DownloadFileQuery(file.Id, userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        _fileStorageService.Verify(storage => storage.GetAsync(file.FileUrl, CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData(true, "text/plain")]
    [InlineData(false, "application/octet-stream")]
    public async Task Handle_WhenUserOwnsFile_ReturnsStreamNameAndMimeType(
        bool hasFileType, string expectedMimeType)
    {
        var userId = Guid.NewGuid();
        var file = CreateFile(userId);
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        using var cancellation = new CancellationTokenSource();
        var cancellationToken = cancellation.Token;
        FileType? fileType = hasFileType ? new FileType("Text", "text/plain", ".txt") : null;
        _fileRepository.Setup(repository => repository.GetByIdAsync(file.Id, cancellationToken))
            .ReturnsAsync(file);
        _fileTypeRepository.Setup(repository => repository.GetByIdAsync(file.FileTypeId, cancellationToken))
            .ReturnsAsync(fileType);
        _fileStorageService.Setup(storage => storage.GetAsync(file.FileUrl, cancellationToken))
            .ReturnsAsync(stream);

        var result = await CreateHandler().Handle(
            new DownloadFileQuery(file.Id, userId), cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Same(stream, result.Value.FileStream);
        Assert.True(result.Value.FileStream.CanRead);
        Assert.Equal(file.Name, result.Value.FileName);
        Assert.Equal(expectedMimeType, result.Value.MimeType);
        _fileStorageService.Verify(storage => storage.GetAsync(file.FileUrl, cancellationToken), Times.Once);
    }

    private DownloadFileQueryHandler CreateHandler() => new(
        _fileRepository.Object, _fileTypeRepository.Object, _fileStorageService.Object);

    private static DriveFile CreateFile(Guid ownerId) => new(
        "report.txt", "stored-report.txt", 2, new string('a', 64), 1024L, ownerId);
}
