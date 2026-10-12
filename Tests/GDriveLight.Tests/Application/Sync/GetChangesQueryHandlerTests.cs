using FluentValidation.TestHelper;
using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Sync.Queries;
using GDriveLight.Domain.Enums;
using GDriveLight.Domain.Models;
using Moq;

namespace GDriveLight.Tests.Application.Sync;

public class GetChangesQueryHandlerTests
{
    private readonly Mock<IChangeLogRepository> _changeLogRepository = new(MockBehavior.Strict);
    private readonly GetChangesQueryValidator _validator = new();

    [Fact]
    public async Task Handle_WhenNoChanges_ReturnsEmptyListSameCursorAndHasMoreFalse()
    {
        var userId = Guid.NewGuid();
        var cursor = 42L;
        var limit = 50;

        _changeLogRepository
            .Setup(r => r.GetChangesAsync(userId, cursor, limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChangeLogEntry>());

        var handler = new GetChangesQueryHandler(_changeLogRepository.Object);
        var query = new GetChangesQuery(userId, cursor, limit);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Changes);
        Assert.Equal(cursor, result.Value.NextCursor);
        Assert.False(result.Value.HasMore);
    }

    [Fact]
    public async Task Handle_WhenChangesExist_ReturnsMappedChangesAndLastIdAsNextCursor()
    {
        var userId = Guid.NewGuid();
        var cursor = 10L;
        var limit = 10;
        var fileId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var originDeviceId = Guid.NewGuid();

        var entry1 = new ChangeLogEntry(
            resourceId: fileId,
            resourceType: ResourceType.File,
            eventType: ChangeType.Created,
            userId: userId,
            originDeviceId: originDeviceId,
            parentFolderId: null,
            name: "test.txt",
            contentHash: "hash123",
            sizeBytes: 256,
            id: 11L);

        var entry2 = new ChangeLogEntry(
            resourceId: folderId,
            resourceType: ResourceType.Folder,
            eventType: ChangeType.Created,
            userId: userId,
            originDeviceId: originDeviceId,
            parentFolderId: null,
            name: "Documents",
            contentHash: null,
            sizeBytes: null,
            id: 15L);

        _changeLogRepository
            .Setup(r => r.GetChangesAsync(userId, cursor, limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry1, entry2 });

        var handler = new GetChangesQueryHandler(_changeLogRepository.Object);
        var query = new GetChangesQuery(userId, cursor, limit);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Changes.Count);
        Assert.Equal(15L, result.Value.NextCursor);
        Assert.False(result.Value.HasMore);

        var item1 = result.Value.Changes[0];
        Assert.Equal(11L, item1.Id);
        Assert.Equal(fileId, item1.ResourceId);
        Assert.Equal(ResourceType.File, item1.ResourceType);
        Assert.Equal(ChangeType.Created, item1.EventType);
        Assert.Equal(originDeviceId, item1.OriginDeviceId);
        Assert.Equal("test.txt", item1.Name);
        Assert.Equal("hash123", item1.ContentHash);
        Assert.Equal(256L, item1.SizeBytes);

        var item2 = result.Value.Changes[1];
        Assert.Equal(15L, item2.Id);
        Assert.Equal(folderId, item2.ResourceId);
        Assert.Equal(ResourceType.Folder, item2.ResourceType);
    }

    [Fact]
    public async Task Handle_WhenChangesCountEqualsLimit_ReturnsHasMoreTrue()
    {
        var userId = Guid.NewGuid();
        var cursor = 0L;
        var limit = 2;

        var entry1 = new ChangeLogEntry(Guid.NewGuid(), ResourceType.File, ChangeType.Created, userId, id: 1L);
        var entry2 = new ChangeLogEntry(Guid.NewGuid(), ResourceType.File, ChangeType.Modified, userId, id: 2L);

        _changeLogRepository
            .Setup(r => r.GetChangesAsync(userId, cursor, limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry1, entry2 });

        var handler = new GetChangesQueryHandler(_changeLogRepository.Object);
        var query = new GetChangesQuery(userId, cursor, limit);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Changes.Count);
        Assert.Equal(2L, result.Value.NextCursor);
        Assert.True(result.Value.HasMore);
    }

    [Fact]
    public void Validator_WhenQueryIsValid_PassesValidation()
    {
        var query = new GetChangesQuery(Guid.NewGuid(), Cursor: 10, Limit: 100);
        var result = _validator.TestValidate(query);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validator_WhenUserIdIsEmpty_HasValidationError()
    {
        var query = new GetChangesQuery(Guid.Empty, Cursor: 0, Limit: 50);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Validator_WhenCursorIsNegative_HasValidationError()
    {
        var query = new GetChangesQuery(Guid.NewGuid(), Cursor: -1, Limit: 50);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Cursor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1001)]
    public void Validator_WhenLimitIsOutOfRange_HasValidationError(int invalidLimit)
    {
        var query = new GetChangesQuery(Guid.NewGuid(), Cursor: 0, Limit: invalidLimit);
        var result = _validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Limit);
    }
}
