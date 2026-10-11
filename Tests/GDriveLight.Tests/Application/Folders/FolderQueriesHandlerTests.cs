using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Folders.Queries;
using GDriveLight.Domain.Models;
using Moq;

namespace GDriveLight.Tests.Application.Folders;

public class FolderQueriesHandlerTests
{
    private readonly Mock<IDriveFolderRepository> _folderRepository = new(MockBehavior.Strict);

    #region GetFolderById

    [Fact]
    public async Task GetFolderById_WhenNotFound_ReturnsFailure()
    {
        var folderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _folderRepository.Setup(r => r.GetByIdAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveFolder?)null);

        var handler = new GetFolderByIdQueryHandler(_folderRepository.Object);
        var result = await handler.Handle(new GetFolderByIdQuery(folderId, userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("was not found", result.Error);
    }

    [Fact]
    public async Task GetFolderById_WhenOwnedByAnotherUser_ReturnsFailure()
    {
        var folderId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var folder = new DriveFolder("Docs", ownerId);

        _folderRepository.Setup(r => r.GetByIdAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);

        var handler = new GetFolderByIdQueryHandler(_folderRepository.Object);
        var result = await handler.Handle(new GetFolderByIdQuery(folderId, otherUserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("permission", result.Error);
    }

    [Fact]
    public async Task GetFolderById_WhenValid_ReturnsFolderDto()
    {
        var userId = Guid.NewGuid();
        var folder = new DriveFolder("Docs", userId);

        _folderRepository.Setup(r => r.GetByIdAsync(folder.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);

        var handler = new GetFolderByIdQueryHandler(_folderRepository.Object);
        var result = await handler.Handle(new GetFolderByIdQuery(folder.Id, userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(folder.Id, result.Value.Id);
        Assert.Equal(folder.Name, result.Value.Name);
        Assert.Equal(userId, result.Value.OwnerId);
    }

    #endregion

    #region GetFolderContent

    [Fact]
    public async Task GetFolderContent_WhenParentFolderNotFound_ReturnsFailure()
    {
        var parentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _folderRepository.Setup(r => r.GetByIdAsync(parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveFolder?)null);

        var handler = new GetFolderContentQueryHandler(_folderRepository.Object);
        var result = await handler.Handle(new GetFolderContentQuery(parentId, userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("was not found", result.Error);
    }

    [Fact]
    public async Task GetFolderContent_WhenParentFolderOwnedByAnotherUser_ReturnsFailure()
    {
        var parentId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var parentFolder = new DriveFolder("Parent", ownerId);

        _folderRepository.Setup(r => r.GetByIdAsync(parentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentFolder);

        var handler = new GetFolderContentQueryHandler(_folderRepository.Object);
        var result = await handler.Handle(new GetFolderContentQuery(parentId, otherUserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("permission", result.Error);
    }

    [Fact]
    public async Task GetFolderContent_WhenParentFolderNull_ReturnsRootFolders()
    {
        var userId = Guid.NewGuid();
        var rootFolders = new List<DriveFolder>
        {
            new("Root1", userId),
            new("Root2", userId)
        };

        _folderRepository.Setup(r => r.GetRootFoldersForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rootFolders);

        var handler = new GetFolderContentQueryHandler(_folderRepository.Object);
        var result = await handler.Handle(new GetFolderContentQuery(null, userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count());
    }

    [Fact]
    public async Task GetFolderContent_WhenParentFolderProvided_ReturnsSubFolders()
    {
        var userId = Guid.NewGuid();
        var parent = new DriveFolder("Parent", userId);
        var subFolders = new List<DriveFolder>
        {
            new("Child1", userId, parent.Id)
        };

        _folderRepository.Setup(r => r.GetByIdAsync(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);
        _folderRepository.Setup(r => r.GetSubFoldersAsync(parent.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subFolders);

        var handler = new GetFolderContentQueryHandler(_folderRepository.Object);
        var result = await handler.Handle(new GetFolderContentQuery(parent.Id, userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value);
        Assert.Equal("Child1", item.Name);
    }

    #endregion

    #region GetFolderPath

    [Fact]
    public async Task GetFolderPath_WhenFolderNotFound_ReturnsFailure()
    {
        var folderId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _folderRepository.Setup(r => r.GetByIdAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DriveFolder?)null);

        var handler = new GetFolderPathQueryHandler(_folderRepository.Object);
        var result = await handler.Handle(new GetFolderPathQuery(folderId, userId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("was not found", result.Error);
    }

    [Fact]
    public async Task GetFolderPath_WhenUserDoesNotOwnFolder_ReturnsFailure()
    {
        var folderId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var folder = new DriveFolder("Secret", ownerId);

        _folderRepository.Setup(r => r.GetByIdAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(folder);

        var handler = new GetFolderPathQueryHandler(_folderRepository.Object);
        var result = await handler.Handle(new GetFolderPathQuery(folderId, otherUserId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("permission", result.Error);
    }

    [Fact]
    public async Task GetFolderPath_WhenNestedFolder_ReturnsBreadcrumbPathFromRoot()
    {
        var userId = Guid.NewGuid();
        var root = new DriveFolder("Root", userId);
        var child = new DriveFolder("Child", userId, root.Id);
        var grandchild = new DriveFolder("Grandchild", userId, child.Id);

        _folderRepository.Setup(r => r.GetByIdAsync(grandchild.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(grandchild);
        _folderRepository.Setup(r => r.GetByIdAsync(child.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(child);
        _folderRepository.Setup(r => r.GetByIdAsync(root.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(root);

        var handler = new GetFolderPathQueryHandler(_folderRepository.Object);
        var result = await handler.Handle(new GetFolderPathQuery(grandchild.Id, userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var pathList = result.Value.ToList();
        Assert.Equal(3, pathList.Count);
        Assert.Equal("Root", pathList[0].Name);
        Assert.Equal("Child", pathList[1].Name);
        Assert.Equal("Grandchild", pathList[2].Name);
    }

    #endregion

    #region GetFoldersByName

    [Fact]
    public async Task GetFoldersByName_WhenNameIsEmpty_ReturnsEmptyListImmediately()
    {
        var userId = Guid.NewGuid();
        var handler = new GetFoldersByNameQueryHandler(_folderRepository.Object);

        var result = await handler.Handle(new GetFoldersByNameQuery("   ", userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        _folderRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetFoldersByName_WhenMatchingFoldersFound_ReturnsDtos()
    {
        var userId = Guid.NewGuid();
        var matching = new List<DriveFolder>
        {
            new("Reports 2025", userId),
            new("Reports 2026", userId)
        };

        _folderRepository.Setup(r => r.SearchByNameAsync("Reports", userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(matching);

        var handler = new GetFoldersByNameQueryHandler(_folderRepository.Object);
        var result = await handler.Handle(new GetFoldersByNameQuery("Reports", userId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count());
    }

    #endregion
}
