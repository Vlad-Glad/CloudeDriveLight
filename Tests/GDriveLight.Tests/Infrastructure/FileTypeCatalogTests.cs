using GDriveLight.Application.Files.Queries;
using GDriveLight.Infrastructure.Entities;
using GDriveLight.Infrastructure.Persistence;
using GDriveLight.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GDriveLight.Tests.Infrastructure;

public sealed class FileTypeCatalogTests
{
    [Fact]
    public async Task CatalogIncludesTypesWithoutFilesAndNewlySavedTypesOnTheNextRequest()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var handler = new GetFileTypesQueryHandler(new FileTypeRepository(db));

        var initial = await handler.Handle(new GetFileTypesQuery(), CancellationToken.None);
        Assert.True(initial.IsSuccess);
        Assert.Contains(initial.Value, type => type.Extension == ".html");
        Assert.Empty(db.DriveFiles);

        db.FileTypes.Add(new FileTypeEntity { Id = 99, Name = "Markdown", MimeType = "text/markdown", Extension = ".md" });
        await db.SaveChangesAsync();

        var refreshed = await handler.Handle(new GetFileTypesQuery(), CancellationToken.None);
        Assert.True(refreshed.IsSuccess);
        var added = Assert.Single(refreshed.Value, type => type.Id == 99);
        Assert.Equal("Markdown", added.Name);
        Assert.Equal(".md", added.Extension);
        Assert.Equal("text/markdown", added.MimeType);
        Assert.Equal("text", added.PreviewKind);
        Assert.Equal(initial.Value.Count() + 1, refreshed.Value.Count());
    }
}
