using Microsoft.EntityFrameworkCore;

namespace GDriveLight.Infrastructure.Persistence.Repositories;

internal static class SyncTreeScope
{
    public static async Task<HashSet<Guid>> GetFolderIdsAsync(AppDbContext db, Guid userId, CancellationToken token)
    {
        var roots = await db.SyncFolders.Where(s => s.Device.UserId == userId).Select(s => s.DriveFolderId).ToListAsync(token);
        var folders = await db.DriveFolders.Where(f => f.OwnerId == userId).Select(f => new { f.Id, f.ParentFolderId }).ToListAsync(token);
        var children = folders.Where(f => f.ParentFolderId.HasValue).ToLookup(f => f.ParentFolderId!.Value, f => f.Id);
        var result = new HashSet<Guid>();
        var queue = new Queue<Guid>(roots);
        while (queue.TryDequeue(out var id))
        {
            if (!result.Add(id)) continue;
            foreach (var child in children[id]) queue.Enqueue(child);
        }
        return result;
    }
}
