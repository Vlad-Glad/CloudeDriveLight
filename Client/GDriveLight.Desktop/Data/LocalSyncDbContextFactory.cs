using System.IO;
using Microsoft.EntityFrameworkCore;

namespace GDriveLight.Desktop.Data;

public interface ILocalSyncDbContextFactory
{
    LocalSyncDbContext CreateDbContext(Guid userId);
    string GetDatabasePath(Guid userId);
}

public class LocalSyncDbContextFactory : ILocalSyncDbContextFactory
{
    private readonly string? _baseDirectory;

    public LocalSyncDbContextFactory(string? baseDirectory = null)
    {
        _baseDirectory = baseDirectory;
    }

    public string GetDatabasePath(Guid userId)
    {
        var baseDir = _baseDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GDriveLight");

        return Path.Combine(baseDir, $"sync_{userId}.db");
    }

    public LocalSyncDbContext CreateDbContext(Guid userId)
    {
        var dbPath = GetDatabasePath(userId);
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var options = new DbContextOptionsBuilder<LocalSyncDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        var context = new LocalSyncDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
