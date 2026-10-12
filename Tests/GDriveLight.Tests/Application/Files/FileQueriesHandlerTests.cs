using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Files.Queries;
using GDriveLight.Domain.Models;
using Moq;

namespace GDriveLight.Tests.Application.Files;

public class FileQueriesHandlerTests
{
    private readonly Mock<IDriveFileRepository> _fileRepository = new(MockBehavior.Strict);
    private readonly Mock<IDriveFolderRepository> _folderRepository = new(MockBehavior.Strict);

    #region GetFileById

    [Fact]
    public async Task GetFileById_WhenNotFound_ReturnsFailure()
    {
        var fileId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _fileRepository.Setup(r => r.GetByIdAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveFile?)null);

        var handler = new GetFileByIdQueryHandler(_fileRepository.Object);
        var result = await handler.Handle(new GetFileByIdQuery(fileId, userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("was not found", result.Error);
    }

    [Fact]
    public async Task GetFileById_WhenOwnedByAnotherUser_ReturnsFailure()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var file = new DriveFile("doc.txt", "url", 1, new string('0', 64), 1024L, ownerId);

        _fileRepository.Setup(r => r.GetByIdAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);

        var handler = new GetFileByIdQueryHandler(_fileRepository.Object);
        var result = await handler.Handle(new GetFileByIdQuery(file.Id, otherUserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("permission", result.Error);
    }

    [Fact]
    public async Task GetFileById_WhenValid_ReturnsFileDto()
    {
        var userId = Guid.NewGuid();
        var file = new DriveFile("doc.txt", "url", 1, new string('0', 64), 1024L, userId);

        _fileRepository.Setup(r => r.GetByIdAsync(file.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);

        var handler = new GetFileByIdQueryHandler(_fileRepository.Object);
        var result = await handler.Handle(new GetFileByIdQuery(file.Id, userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(file.Id, result.Value.Id);
        Assert.Equal(file.Name, result.Value.Name);
        Assert.Equal(file.FileUrl, result.Value.FileUrl);
        Assert.Equal(userId, result.Value.OwnerId);
    }

    #endregion

    #region GetFilesByFolder

    [Fact]
    public async Task GetFilesByFolder_WhenTargetFolderNotFound_ReturnsFailure()
    {
        var folderId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _folderRepository.Setup(r => r.GetByIdAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveFolder?)null);

        var handler = new GetFilesByFolderQueryHandler(_fileRepository.Object, _folderRepository.Object);
        var result = await handler.Handle(new GetFilesByFolderQuery(folderId, userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("was not found", result.Error);
    }

    [Fact]
    public async Task GetFilesByFolder_WhenTargetFolderOwnedByAnotherUser_ReturnsFailure()
    {
        var folderId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var folder = new DriveFolder("Folder", ownerId);

        _folderRepository.Setup(r => r.GetByIdAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);

        var handler = new GetFilesByFolderQueryHandler(_fileRepository.Object, _folderRepository.Object);
        var result = await handler.Handle(new GetFilesByFolderQuery(folderId, otherUserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("permission", result.Error);
    }

    [Fact]
    public async Task GetFilesByFolder_WhenFolderProvided_ReturnsFilesInFolder()
    {
        var userId = Guid.NewGuid();
        var folder = new DriveFolder("Folder", userId);
        var files = new List<DriveFile>
        {
            new("file1.txt", "url1", 1, new string('0', 64), 1024L, userId, folder.Id)
        };

        _folderRepository.Setup(r => r.GetByIdAsync(folder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);
        _fileRepository.Setup(r => r.GetByFolderIdAsync(folder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);

        var handler = new GetFilesByFolderQueryHandler(_fileRepository.Object, _folderRepository.Object);
        var result = await handler.Handle(new GetFilesByFolderQuery(folder.Id, userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var fileDto = Assert.Single(result.Value);
        Assert.Equal("file1.txt", fileDto.Name);
    }

    [Fact]
    public async Task GetFilesByFolder_WhenFolderNull_ReturnsRootFiles()
    {
        var userId = Guid.NewGuid();
        var files = new List<DriveFile>
        {
            new("root1.txt", "url1", 1, new string('0', 64), 1024L, userId, null),
            new("root2.txt", "url2", 1, new string('0', 64), 1024L, userId, null)
        };

        _fileRepository.Setup(r => r.GetRootFilesForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);

        var handler = new GetFilesByFolderQueryHandler(_fileRepository.Object, _folderRepository.Object);
        var result = await handler.Handle(new GetFilesByFolderQuery(null, userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count());
    }

    #endregion

    #region SearchFiles

    [Fact]
    public async Task SearchFiles_WhenNameIsEmpty_ReturnsEmptyListImmediately()
    {
        var userId = Guid.NewGuid();
        var handler = new SearchFilesQueryHandler(_fileRepository.Object);

        var result = await handler.Handle(new SearchFilesQuery("   ", userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        _fileRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SearchFiles_WhenMatchingFilesFound_ReturnsDtos()
    {
        var userId = Guid.NewGuid();
        var files = new List<DriveFile>
        {
            new("project_specs.docx", "url1", 1, new string('0', 64), 1024L, userId),
            new("project_budget.xlsx", "url2", 1, new string('0', 64), 1024L, userId)
        };

        _fileRepository.Setup(r => r.SearchByNameAsync("project", userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);

        var handler = new SearchFilesQueryHandler(_fileRepository.Object);
        var result = await handler.Handle(new SearchFilesQuery("project", userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count());
    }

    #endregion
}
