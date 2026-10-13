using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using GDriveLight.Desktop.Data;
using GDriveLight.Desktop.Models.Api;
using GDriveLight.Desktop.Models.Sync;
using GDriveLight.Desktop.Services.Api;
using Microsoft.EntityFrameworkCore;

namespace GDriveLight.Desktop.Services.Sync;

public class SyncEngine : ISyncEngine
{
    private readonly IDriveApiService _apiService;
    private readonly ILocalSyncDbContextFactory _dbContextFactory;
    private readonly IDeviceIdentifierService _deviceIdentifierService;

    public SyncEngine(
        IDriveApiService apiService,
        ILocalSyncDbContextFactory dbContextFactory,
        IDeviceIdentifierService deviceIdentifierService)
    {
        _apiService = apiService;
        _dbContextFactory = dbContextFactory;
        _deviceIdentifierService = deviceIdentifierService;
    }

    public async Task<SyncResult> SynchronizeAsync(Guid userId, string localFolderPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localFolderPath);

        try
        {
            if (!Directory.Exists(localFolderPath))
            {
                Directory.CreateDirectory(localFolderPath);
            }

            using var dbContext = _dbContextFactory.CreateDbContext(userId);

            var cursor = await dbContext.Cursors.FindAsync(new object[] { 1 }, cancellationToken);
            if (cursor == null)
            {
                cursor = new SyncCursor { Id = 1, LastProcessedChangeId = 0 };
                dbContext.Cursors.Add(cursor);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var myDeviceId = _deviceIdentifierService.GetDeviceId();
            var hasExistingItems = await dbContext.Items.AnyAsync(cancellationToken);

            if (cursor.LastProcessedChangeId == 0 && !hasExistingItems)
            {
                return await RunInitialSyncAsync(userId, localFolderPath, dbContext, cursor, cancellationToken);
            }

            return await RunDeltaSyncAsync(userId, localFolderPath, dbContext, cursor, myDeviceId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DriveApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            return new SyncResult(false, 0, "Network connection error during sync. Please check your connection.", FailedCount: 1, Errors: new[] { ex.Message });
        }
        catch (Exception ex)
        {
            return new SyncResult(false, 0, $"Sync failed: {ex.Message}", FailedCount: 1, Errors: new[] { ex.Message });
        }
    }

    private async Task<SyncResult> RunInitialSyncAsync(
        Guid userId,
        string localFolderPath,
        LocalSyncDbContext dbContext,
        SyncCursor cursor,
        CancellationToken cancellationToken)
    {
        long latestCursor = 0;
        long currentCursor = 0;
        while (true)
        {
            var changesResponse = await _apiService.GetChangesAsync(currentCursor, 1000, cancellationToken);
            latestCursor = changesResponse.NextCursor;

            if (!changesResponse.HasMore || changesResponse.NextCursor == currentCursor)
            {
                break;
            }

            currentCursor = changesResponse.NextCursor;
        }

        int count = 0;
        int failedCount = 0;
        var errors = new List<string>();
        var folderQueue = new Queue<(Guid? ServerFolderId, string ParentRelativePath)>();
        folderQueue.Enqueue((null, string.Empty));

        while (folderQueue.Count > 0)
        {
            var (folderId, parentRelativePath) = folderQueue.Dequeue();

            IEnumerable<FolderResponse> folders;
            try
            {
                folders = await _apiService.GetFolderContentAsync(folderId, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (DriveApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized) { throw; }
            catch (Exception ex)
            {
                failedCount++;
                errors.Add($"Failed to retrieve folder contents: {ex.Message}");
                continue;
            }

            foreach (var subFolder in folders)
            {
                try
                {
                    var subRelPath = string.IsNullOrEmpty(parentRelativePath)
                        ? subFolder.Name
                        : $"{parentRelativePath}/{subFolder.Name}";

                    var localDirPath = Path.Combine(localFolderPath, subRelPath.Replace('/', Path.DirectorySeparatorChar));
                    if (!Directory.Exists(localDirPath))
                    {
                        Directory.CreateDirectory(localDirPath);
                    }

                    dbContext.Items.Add(new LocalItemState
                    {
                        ServerResourceId = subFolder.Id,
                        IsDirectory = true,
                        RelativePath = subRelPath,
                        Status = SyncStatus.Synced,
                        LocalModifiedAtUtc = Directory.GetLastWriteTimeUtc(localDirPath)
                    });

                    count++;
                    folderQueue.Enqueue((subFolder.Id, subRelPath));
                }
                catch (OperationCanceledException) { throw; }
                catch (DriveApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized) { throw; }
                catch (Exception ex)
                {
                    failedCount++;
                    errors.Add($"Failed to sync folder '{subFolder.Name}': {ex.Message}");
                }
            }

            IEnumerable<FileResponse> files;
            try
            {
                files = await _apiService.GetFilesAsync(folderId, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (DriveApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized) { throw; }
            catch (Exception ex)
            {
                failedCount++;
                errors.Add($"Failed to retrieve files: {ex.Message}");
                continue;
            }

            foreach (var file in files)
            {
                try
                {
                    var fileRelPath = string.IsNullOrEmpty(parentRelativePath)
                        ? file.Name
                        : $"{parentRelativePath}/{file.Name}";

                    var localFilePath = Path.Combine(localFolderPath, fileRelPath.Replace('/', Path.DirectorySeparatorChar));
                    var needsDownload = true;

                    if (File.Exists(localFilePath))
                    {
                        var localHash = await ComputeFileHashAsync(localFilePath, cancellationToken);
                        if (string.Equals(localHash, file.ContentHash, StringComparison.OrdinalIgnoreCase))
                        {
                            needsDownload = false;
                        }
                    }

                    if (needsDownload)
                    {
                        var dir = Path.GetDirectoryName(localFilePath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        await _apiService.DownloadFileAsync(file.Id, localFilePath, cancellationToken);
                    }

                    var fileInfo = new FileInfo(localFilePath);
                    dbContext.Items.Add(new LocalItemState
                    {
                        ServerResourceId = file.Id,
                        IsDirectory = false,
                        RelativePath = fileRelPath,
                        LocalSize = fileInfo.Length,
                        LastSyncedHash = file.ContentHash,
                        LocalModifiedAtUtc = fileInfo.LastWriteTimeUtc,
                        Status = SyncStatus.Synced
                    });

                    count++;
                }
                catch (OperationCanceledException) { throw; }
                catch (DriveApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized) { throw; }
                catch (Exception ex)
                {
                    failedCount++;
                    errors.Add($"Failed to sync file '{file.Name}': {ex.Message}");
                }
            }
        }

        cursor.LastProcessedChangeId = latestCursor;
        await dbContext.SaveChangesAsync(cancellationToken);

        var isSuccess = failedCount == 0;
        var message = isSuccess
            ? $"Initial sync completed. Processed {count} items."
            : $"Initial sync completed with {failedCount} errors. Processed {count} items.";

        return new SyncResult(isSuccess, count, message, failedCount, errors);
    }

    private async Task<SyncResult> RunDeltaSyncAsync(
        Guid userId,
        string localFolderPath,
        LocalSyncDbContext dbContext,
        SyncCursor cursor,
        Guid myDeviceId,
        CancellationToken cancellationToken)
    {
        int processedCount = 0;
        int failedCount = 0;
        var errors = new List<string>();

        while (true)
        {
            var response = await _apiService.GetChangesAsync(cursor.LastProcessedChangeId, 100, cancellationToken);
            if (response.Changes.Count == 0)
            {
                break;
            }

            foreach (var change in response.Changes)
            {
                try
                {
                    await ApplyChangeAsync(change, localFolderPath, dbContext, myDeviceId, cancellationToken);
                    processedCount++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (DriveApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failedCount++;
                    errors.Add($"Failed to apply change for '{change.Name ?? change.ResourceId.ToString()}': {ex.Message}");
                    await MarkItemConflictAsync(change, dbContext, cancellationToken);
                }
            }

            cursor.LastProcessedChangeId = response.NextCursor;
            await dbContext.SaveChangesAsync(cancellationToken);

            if (!response.HasMore)
            {
                break;
            }
        }

        var isSuccess = failedCount == 0;
        var message = isSuccess
            ? $"Delta sync completed. Processed {processedCount} changes."
            : $"Delta sync completed with {failedCount} errors. Processed {processedCount} changes.";

        return new SyncResult(isSuccess, processedCount, message, failedCount, errors);
    }

    private static async Task MarkItemConflictAsync(
        ChangeResponse change,
        LocalSyncDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await dbContext.Items.FirstOrDefaultAsync(x => x.ServerResourceId == change.ResourceId, cancellationToken);
            if (item is not null)
            {
                item.Status = SyncStatus.Conflict;
            }
            else
            {
                var relPath = await BuildRelativePathAsync(change.Name, change.ParentFolderId, dbContext, cancellationToken);
                dbContext.Items.Add(new LocalItemState
                {
                    ServerResourceId = change.ResourceId,
                    IsDirectory = change.ResourceType == ResourceType.Folder,
                    RelativePath = relPath,
                    LocalSize = change.SizeBytes ?? 0,
                    LastSyncedHash = change.ContentHash,
                    LocalModifiedAtUtc = DateTime.UtcNow,
                    Status = SyncStatus.Conflict
                });
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Do not fail the whole sync process if conflict recording fails
        }
    }

    private async Task ApplyChangeAsync(
        ChangeResponse change,
        string localFolderPath,
        LocalSyncDbContext dbContext,
        Guid myDeviceId,
        CancellationToken cancellationToken)
    {
        switch (change.EventType)
        {
            case ChangeType.Deleted:
                await ApplyDeleteChangeAsync(change, localFolderPath, dbContext, myDeviceId, cancellationToken);
                break;

            case ChangeType.Created:
            case ChangeType.Modified:
                await ApplyUpsertChangeAsync(change, localFolderPath, dbContext, myDeviceId, cancellationToken);
                break;

            case ChangeType.Renamed:
            case ChangeType.Moved:
                await ApplyMoveChangeAsync(change, localFolderPath, dbContext, myDeviceId, cancellationToken);
                break;
        }
    }

    private static async Task ApplyDeleteChangeAsync(
        ChangeResponse change,
        string localFolderPath,
        LocalSyncDbContext dbContext,
        Guid myDeviceId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Items.FirstOrDefaultAsync(x => x.ServerResourceId == change.ResourceId, cancellationToken);
        if (item is null)
        {
            return;
        }

        if (change.OriginDeviceId != myDeviceId)
        {
            var path = Path.Combine(localFolderPath, item.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (item.IsDirectory)
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            else
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        dbContext.Items.Remove(item);
    }

    private async Task ApplyUpsertChangeAsync(
        ChangeResponse change,
        string localFolderPath,
        LocalSyncDbContext dbContext,
        Guid myDeviceId,
        CancellationToken cancellationToken)
    {
        if (change.ResourceType == ResourceType.Folder)
        {
            var folderRelPath = await BuildRelativePathAsync(change.Name, change.ParentFolderId, dbContext, cancellationToken);
            if (change.OriginDeviceId != myDeviceId)
            {
                var localDirPath = Path.Combine(localFolderPath, folderRelPath.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(localDirPath))
                {
                    Directory.CreateDirectory(localDirPath);
                }
            }

            var item = await dbContext.Items.FirstOrDefaultAsync(x => x.ServerResourceId == change.ResourceId, cancellationToken);
            if (item is null)
            {
                dbContext.Items.Add(new LocalItemState
                {
                    ServerResourceId = change.ResourceId,
                    IsDirectory = true,
                    RelativePath = folderRelPath,
                    Status = SyncStatus.Synced,
                    LocalModifiedAtUtc = DateTime.UtcNow
                });
            }
            else
            {
                item.RelativePath = folderRelPath;
                item.Status = SyncStatus.Synced;
            }
        }
        else
        {
            var fileRelPath = await BuildRelativePathAsync(change.Name, change.ParentFolderId, dbContext, cancellationToken);
            var localFilePath = Path.Combine(localFolderPath, fileRelPath.Replace('/', Path.DirectorySeparatorChar));

            if (change.OriginDeviceId == myDeviceId)
            {
                // Echo protection: Do not download or overwrite local file
                var item = await dbContext.Items.FirstOrDefaultAsync(x => x.ServerResourceId == change.ResourceId, cancellationToken)
                    ?? await dbContext.Items.FirstOrDefaultAsync(x => x.RelativePath == fileRelPath, cancellationToken);

                if (item is null)
                {
                    dbContext.Items.Add(new LocalItemState
                    {
                        ServerResourceId = change.ResourceId,
                        IsDirectory = false,
                        RelativePath = fileRelPath,
                        LocalSize = change.SizeBytes,
                        LastSyncedHash = change.ContentHash,
                        LocalModifiedAtUtc = File.Exists(localFilePath) ? File.GetLastWriteTimeUtc(localFilePath) : DateTime.UtcNow,
                        Status = SyncStatus.Synced
                    });
                }
                else
                {
                    item.ServerResourceId = change.ResourceId;
                    item.RelativePath = fileRelPath;
                    item.LocalSize = change.SizeBytes;
                    item.LastSyncedHash = change.ContentHash;
                    item.Status = SyncStatus.Synced;
                    if (File.Exists(localFilePath))
                    {
                        item.LocalModifiedAtUtc = File.GetLastWriteTimeUtc(localFilePath);
                    }
                }
            }
            else
            {
                // Download file from server
                var dir = Path.GetDirectoryName(localFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                await _apiService.DownloadFileAsync(change.ResourceId, localFilePath, cancellationToken);
                var fi = new FileInfo(localFilePath);

                var item = await dbContext.Items.FirstOrDefaultAsync(x => x.ServerResourceId == change.ResourceId, cancellationToken)
                    ?? await dbContext.Items.FirstOrDefaultAsync(x => x.RelativePath == fileRelPath, cancellationToken);

                if (item is null)
                {
                    dbContext.Items.Add(new LocalItemState
                    {
                        ServerResourceId = change.ResourceId,
                        IsDirectory = false,
                        RelativePath = fileRelPath,
                        LocalSize = fi.Length,
                        LastSyncedHash = change.ContentHash,
                        LocalModifiedAtUtc = fi.LastWriteTimeUtc,
                        Status = SyncStatus.Synced
                    });
                }
                else
                {
                    item.ServerResourceId = change.ResourceId;
                    item.RelativePath = fileRelPath;
                    item.LocalSize = fi.Length;
                    item.LastSyncedHash = change.ContentHash;
                    item.LocalModifiedAtUtc = fi.LastWriteTimeUtc;
                    item.Status = SyncStatus.Synced;
                }
            }
        }
    }

    private static async Task ApplyMoveChangeAsync(
        ChangeResponse change,
        string localFolderPath,
        LocalSyncDbContext dbContext,
        Guid myDeviceId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Items.FirstOrDefaultAsync(x => x.ServerResourceId == change.ResourceId, cancellationToken);
        if (item is null)
        {
            return;
        }

        var newName = change.Name ?? Path.GetFileName(item.RelativePath);
        var newRelPath = await BuildRelativePathAsync(newName, change.ParentFolderId, dbContext, cancellationToken);

        if (change.OriginDeviceId != myDeviceId)
        {
            var oldDiskPath = Path.Combine(localFolderPath, item.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            var newDiskPath = Path.Combine(localFolderPath, newRelPath.Replace('/', Path.DirectorySeparatorChar));

            if (item.IsDirectory)
            {
                if (Directory.Exists(oldDiskPath))
                {
                    var parentDir = Path.GetDirectoryName(newDiskPath);
                    if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                    {
                        Directory.CreateDirectory(parentDir);
                    }

                    Directory.Move(oldDiskPath, newDiskPath);
                }
            }
            else
            {
                if (File.Exists(oldDiskPath))
                {
                    var parentDir = Path.GetDirectoryName(newDiskPath);
                    if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                    {
                        Directory.CreateDirectory(parentDir);
                    }

                    File.Move(oldDiskPath, newDiskPath, overwrite: true);
                }
            }
        }

        item.RelativePath = newRelPath;
        item.Status = SyncStatus.Synced;
    }

    private static async Task<string> BuildRelativePathAsync(
        string? name,
        Guid? parentFolderId,
        LocalSyncDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var itemName = name ?? string.Empty;
        if (!parentFolderId.HasValue)
        {
            return itemName;
        }

        var parent = await dbContext.Items.FirstOrDefaultAsync(x => x.ServerResourceId == parentFolderId.Value, cancellationToken);
        if (parent is null)
        {
            return itemName;
        }

        return string.IsNullOrEmpty(parent.RelativePath)
            ? itemName
            : $"{parent.RelativePath}/{itemName}";
    }

    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        return BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
    }
}
