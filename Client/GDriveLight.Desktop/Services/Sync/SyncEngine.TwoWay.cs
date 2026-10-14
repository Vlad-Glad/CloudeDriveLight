using System.IO;
using System.Net;
using System.Security.Cryptography;
using GDriveLight.Desktop.Data;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Models.Sync;
using GDriveLight.Desktop.Services.Api;
using Microsoft.EntityFrameworkCore;

namespace GDriveLight.Desktop.Services.Sync;

public partial class SyncEngine
{
    private static SyncResult CombineResults(SyncResult download, SyncResult upload)
    {
        var failed = download.FailedCount + upload.FailedCount;
        var processed = download.ProcessedChangesCount + upload.ProcessedChangesCount;
        var errors = (download.Errors ?? []).Concat(upload.Errors ?? []).ToList();
        return new SyncResult(download.Success && upload.Success, processed,
            failed == 0 ? $"Synchronized {processed} items in both directions."
                : $"Synchronization completed with {failed} error(s). " + string.Join("\n", errors.Take(5)),
            failed, errors);
    }

    private async Task<SyncResult> RunLocalSyncAsync(string rootPath, LocalSyncDbContext db, Guid serverRoot, CancellationToken token)
    {
        var errors = new List<string>();
        var processed = 0;
        List<(string Path, bool IsDirectory)> entries;
        try
        {
            // Obtain the complete tree before interpreting a missing entry as a deletion.
            entries = await Task.Run(() => ScanLocalTree(rootPath, token), token);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new SyncResult(false, 0, "Could not read the local sync folder.", 1, [ex.Message]);
        }

        var existingTypes = entries.ToDictionary(e => e.Path, e => e.IsDirectory, StringComparer.OrdinalIgnoreCase);
        var states = await db.Items.ToListAsync(token);
        var blockedPaths = states.Where(s => s.Status == SyncStatus.Conflict).Select(s => s.RelativePath).ToList();
        foreach (var path in blockedPaths) errors.Add($"Unresolved conflict: {path}");

        bool IsBlocked(string path) => blockedPaths.Any(blocked => string.Equals(path, blocked, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(blocked + "/", StringComparison.OrdinalIgnoreCase));

        var hasTransferFailure = false;
        // Parents are visited before children, so a new file never falls back to Home.
        foreach (var entry in entries.OrderBy(e => e.Path.Count(c => c == '/')).ThenByDescending(e => e.IsDirectory))
        {
            token.ThrowIfCancellationRequested();
            if (IsBlocked(entry.Path)) continue;
            try
            {
                var parentPath = entry.Path.Contains('/') ? entry.Path[..entry.Path.LastIndexOf('/')] : string.Empty;
                var parentId = parentPath.Length == 0 ? serverRoot :
                    (await db.Items.SingleOrDefaultAsync(s => s.RelativePath == parentPath && s.IsDirectory, token))?.ServerResourceId;
                if (!parentId.HasValue) throw new IOException("The parent folder could not be synchronized.");
                var state = await db.Items.SingleOrDefaultAsync(s => s.RelativePath == entry.Path, token);
                var path = ResolveLocalPath(rootPath, entry.Path);
                if (state is not null && state.IsDirectory != entry.IsDirectory)
                    throw new IOException("The local and cloud item types do not match.");
                if (entry.IsDirectory)
                {
                    if (state?.ServerResourceId is not null) continue;
                    var id = await _apiService.CreateFolderAsync(Path.GetFileName(path), parentId, true, token);
                    state ??= new LocalItemState { RelativePath = entry.Path, IsDirectory = true };
                    if (db.Entry(state).State == EntityState.Detached) db.Items.Add(state);
                    state.ServerResourceId = id;
                    state.Status = SyncStatus.Synced;
                    state.LocalModifiedAtUtc = Directory.GetLastWriteTimeUtc(path);
                }
                else
                {
                    // Keep a read handle open across hashing and transfer, excluding concurrent writes.
                    await using var guard = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                        65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
                    var hash = Convert.ToHexString(await SHA256.HashDataAsync(guard, token)).ToLowerInvariant();
                    if (state?.ServerResourceId is not null && string.Equals(state.LastSyncedHash, hash, StringComparison.OrdinalIgnoreCase)) continue;
                    var id = state?.ServerResourceId;
                    if (id.HasValue)
                    {
                        await using var content = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                            65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
                        await _apiService.UpdateFileContentAsync(id.Value, content, Path.GetFileName(path), token);
                    }
                    else
                    {
                        id = await _apiService.UploadFileAsync(path, parentId, true, token);
                    }
                    state ??= new LocalItemState { RelativePath = entry.Path };
                    if (db.Entry(state).State == EntityState.Detached) db.Items.Add(state);
                    state.ServerResourceId = id;
                    state.LastSyncedHash = hash;
                    state.LocalSize = guard.Length;
                    state.LocalModifiedAtUtc = File.GetLastWriteTimeUtc(path);
                    state.Status = SyncStatus.Synced;
                }
                await db.SaveChangesAsync(token);
                processed++;
            }
            catch (Exception ex) when (CanContinueAfterLocalError(ex))
            {
                hasTransferFailure = true;
                if (entry.IsDirectory) blockedPaths.Add(entry.Path);
                errors.Add($"{entry.Path}: {ex.Message}");
            }
        }
        // Upload first: a local rename is represented by a new path and removal of the old path.
        // If transfer fails, retain cloud sources until the next successful attempt.
        // Parent deletion records the cascade once.
        foreach (var state in states.Where(s => s.RelativePath.Length > 0 && (!existingTypes.TryGetValue(s.RelativePath, out var directory) || directory != s.IsDirectory))
                     .OrderBy(s => s.RelativePath.Count(c => c == '/')).ThenByDescending(s => s.IsDirectory))
        {
            token.ThrowIfCancellationRequested();
            if (hasTransferFailure)
            {
                errors.Add("Local deletions were postponed because a file transfer failed.");
                break;
            }
            if (db.Entry(state).State == EntityState.Detached || IsBlocked(state.RelativePath)) continue;
            if (state.IsDirectory && blockedPaths.Any(path => path.StartsWith(state.RelativePath + "/", StringComparison.OrdinalIgnoreCase)))
            {
                blockedPaths.Add(state.RelativePath);
                errors.Add($"Folder contains an unresolved conflict: {state.RelativePath}");
                continue;
            }
            try
            {
                if (state.ServerResourceId.HasValue)
                    await _apiService.DeleteAsync(state.ServerResourceId.Value, state.IsDirectory, true, token);
                if (state.IsDirectory)
                {
                    var prefix = state.RelativePath + "/";
                    db.Items.RemoveRange(states.Where(s => s.RelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
                }
                db.Items.Remove(state);
                await db.SaveChangesAsync(token);
                processed++;
            }
            catch (Exception ex) when (CanContinueAfterLocalError(ex))
            {
                blockedPaths.Add(state.RelativePath);
                errors.Add($"{state.RelativePath}: {ex.Message}");
            }
        }

        return new SyncResult(errors.Count == 0, processed, null, errors.Count, errors);
    }

    private static bool CanContinueAfterLocalError(Exception exception) =>
        exception is not OperationCanceledException && exception is not DriveApiException { StatusCode: HttpStatusCode.Unauthorized };

    private static List<(string Path, bool IsDirectory)> ScanLocalTree(string rootPath, CancellationToken token)
    {
        var result = new List<(string, bool)>();
        var queue = new Queue<string>();
        queue.Enqueue(rootPath);
        while (queue.TryDequeue(out var folder))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
            {
                token.ThrowIfCancellationRequested();
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException($"Symbolic links cannot be synchronized: {entry}");
                if (Path.GetFileName(entry).StartsWith(".gdrivelight-", StringComparison.OrdinalIgnoreCase) &&
                    entry.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                var isDirectory = (attributes & FileAttributes.Directory) != 0;
                result.Add((Path.GetRelativePath(rootPath, entry).Replace('\\', '/'), isDirectory));
                if (isDirectory) queue.Enqueue(entry);
            }
        }
        return result;
    }

    private static async Task EnsureCloudChangeDoesNotOverwriteLocalAsync(ChangeResponse change, LocalItemState? tracked,
        string rootPath, LocalSyncDbContext db, CancellationToken token)
    {
        if (tracked?.Status == SyncStatus.Conflict)
            throw new IOException("This item has an unresolved synchronization conflict.");

        if (tracked is not null)
        {
            var path = ResolveLocalPath(rootPath, tracked.RelativePath);
            var exists = tracked.IsDirectory ? Directory.Exists(path) : File.Exists(path);
            if (!exists && change.EventType != ChangeType.Deleted)
                throw new IOException("The item was removed locally and changed in the cloud. Neither change was overwritten.");
            if (exists && !tracked.IsDirectory && tracked.LastSyncedHash is not null)
            {
                var hash = await ComputeFileHashAsync(path, token);
                if (!string.Equals(hash, tracked.LastSyncedHash, StringComparison.OrdinalIgnoreCase) &&
                    (change.EventType is ChangeType.Deleted or ChangeType.Moved or ChangeType.Renamed ||
                     !string.Equals(hash, change.ContentHash, StringComparison.OrdinalIgnoreCase)))
                    throw new IOException("The file changed locally and in the cloud. Neither version was overwritten.");
            }
            if (tracked.IsDirectory && change.EventType == ChangeType.Deleted && exists)
            {
                var prefix = tracked.RelativePath + "/";
                foreach (var child in await db.Items.Where(s => !s.IsDirectory && s.RelativePath.StartsWith(prefix)).ToListAsync(token))
                {
                    var childPath = ResolveLocalPath(rootPath, child.RelativePath);
                    if (File.Exists(childPath) && (child.Status == SyncStatus.Conflict || child.LastSyncedHash is null ||
                        !string.Equals(await ComputeFileHashAsync(childPath, token), child.LastSyncedHash, StringComparison.OrdinalIgnoreCase)))
                        throw new IOException("The folder contains local changes and was deleted in the cloud.");
                }
                var knownPaths = (await db.Items.Where(s => s.RelativePath.StartsWith(prefix)).Select(s => s.RelativePath).ToListAsync(token))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (ScanLocalTree(path, token).Any(entry => !knownPaths.Contains(prefix + entry.Path)))
                    throw new IOException("The folder contains new local items and was deleted in the cloud.");
            }
        }

        if (tracked is not null)
        {
            var conflictedFolders = await db.Items.Where(s => s.IsDirectory && s.Status == SyncStatus.Conflict)
                .Select(s => s.RelativePath).ToListAsync(token);
            if (conflictedFolders.Any(path => tracked.RelativePath.StartsWith(path + "/", StringComparison.OrdinalIgnoreCase)))
                throw new IOException("An ancestor folder has an unresolved synchronization conflict.");
        }
        if (change.ParentFolderId.HasValue)
        {
            var parent = await db.Items.SingleOrDefaultAsync(s => s.ServerResourceId == change.ParentFolderId, token);
            if (parent is not null && (parent.Status == SyncStatus.Conflict ||
                (parent.RelativePath.Length > 0 && !Directory.Exists(ResolveLocalPath(rootPath, parent.RelativePath)))))
            {
                parent.Status = SyncStatus.Conflict;
                throw new IOException("The parent folder was changed locally and in the cloud.");
            }
        }
        if (change.EventType == ChangeType.Deleted) return;
        if (tracked is null && change.ResourceType == ResourceType.File && change.Name is not null)
        {
            var relativePath = await BuildRelativePathAsync(change.Name, change.ParentFolderId, db, token);
            var path = ResolveLocalPath(rootPath, relativePath);
            if (File.Exists(path) && !string.Equals(await ComputeFileHashAsync(path, token), change.ContentHash, StringComparison.OrdinalIgnoreCase))
                throw new IOException("A different local file already exists at the cloud file's location.");
        }
    }
}
