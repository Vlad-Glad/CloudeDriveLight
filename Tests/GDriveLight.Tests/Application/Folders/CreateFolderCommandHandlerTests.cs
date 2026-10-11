using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Folders.Commands;
using GDriveLight.Domain.Models;
using Moq;

namespace GDriveLight.Tests.Application.Folders;

public class CreateFolderCommandHandlerTests
{
    private readonly Mock<IDriveFolderRepository> _folderRepository = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);

    [Fact]
    public async Task Handle_WhenFolderAlreadyExists_ReturnsFailureWithoutAdding()
    {
        var ownerId = Guid.NewGuid();
        var folderName = "Documents";
        _folderRepository.Setup(r => r.ExistsAsync(folderName, null, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = CreateHandler();
        var result = await handler.Handle(new CreateFolderCommand(folderName, ownerId, null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("already exists", result.Error);
        _folderRepository.Verify(r => r.AddAsync(It.IsAny<DriveFolder>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenValidRootFolder_AddsFolderAndSavesChanges()
    {
        var ownerId = Guid.NewGuid();
        var folderName = "Projects";
        _folderRepository.Setup(r => r.ExistsAsync(folderName, null, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _folderRepository.Setup(r => r.AddAsync(It.Is<DriveFolder>(f => f.Name == folderName && f.OwnerId == ownerId && f.ParentFolderId == null), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();
        var result = await handler.Handle(new CreateFolderCommand(folderName, ownerId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
        _folderRepository.Verify(r => r.AddAsync(It.IsAny<DriveFolder>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenValidSubFolder_AddsFolderWithParentAndSavesChanges()
    {
        var ownerId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var folderName = "2026";
        _folderRepository.Setup(r => r.ExistsAsync(folderName, parentId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _folderRepository.Setup(r => r.AddAsync(It.Is<DriveFolder>(f => f.Name == folderName && f.OwnerId == ownerId && f.ParentFolderId == parentId), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var handler = CreateHandler();
        var result = await handler.Handle(new CreateFolderCommand(folderName, ownerId, parentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
    }

    private CreateFolderCommandHandler CreateHandler() =>
        new(_folderRepository.Object, _unitOfWork.Object);
}
