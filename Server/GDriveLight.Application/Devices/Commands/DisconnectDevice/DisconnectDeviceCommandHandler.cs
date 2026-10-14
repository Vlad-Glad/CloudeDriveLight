using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using GDriveLight.Domain.Enums;
using GDriveLight.Domain.Models;
using MediatR;

namespace GDriveLight.Application.Devices.Commands.DisconnectDevice;

public sealed class DisconnectDeviceCommandHandler(
    IDeviceRepository devices, ISyncFolderRepository syncFolders,
    IDriveFolderRepository folders, IDriveFileRepository files,
    IChangeLogRepository changes, IUnitOfWork unitOfWork)
    : IRequestHandler<DisconnectDeviceCommand, Result>
{
    public async Task<Result> Handle(DisconnectDeviceCommand request, CancellationToken cancellationToken)
    {
        var device = await devices.GetByIdAsync(request.DeviceId, cancellationToken);
        // Retrying a completed disconnect must not archive the tree a second time.
        if (device is null) return Result.Success();
        if (device.UserId != request.UserId) return Result.Failure("Device not found.");

        var usedNames = (await folders.GetRootFoldersForUserAsync(request.UserId, cancellationToken))
            .Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        var entries = new List<ChangeLogEntry>();
        foreach (var binding in await syncFolders.GetByDeviceIdAsync(device.Id, cancellationToken))
        {
            var root = await folders.GetByIdAsync(binding.DriveFolderId, cancellationToken);
            if (root is null || root.OwnerId != request.UserId)
                return Result.Failure("The synchronized folder could not be found.");

            var name = root.Name;
            for (var suffix = 2; !usedNames.Add(name); suffix++)
            {
                var ending = $" ({suffix})";
                name = root.Name[..Math.Min(root.Name.Length, 255 - ending.Length)] + ending;
            }
            root.Rename(name);
            root.MoveTo(null);
            folders.Update(root);
            syncFolders.Delete(binding);

            // Publish the complete tree in parent-first order for clients whose cursors
            // already passed the original, device-only creation events.
            var queue = new Queue<DriveFolder>();
            var visited = new HashSet<Guid>();
            queue.Enqueue(root);
            while (queue.TryDequeue(out var folder))
            {
                if (!visited.Add(folder.Id)) continue;
                if (folder.OwnerId != request.UserId) return Result.Failure("Folder ownership is invalid.");
                entries.Add(ChangeLogEntry.CreateFolderChange(folder.Id, ChangeType.Created,
                    request.UserId, folder.ParentFolderId, folder.Name));
                foreach (var file in await files.GetByFolderIdAsync(folder.Id, cancellationToken))
                {
                    if (file.OwnerId != request.UserId) return Result.Failure("File ownership is invalid.");
                    entries.Add(ChangeLogEntry.CreateFileChange(file.Id, ChangeType.Created,
                        request.UserId, file.FolderId, file.Name, file.ContentHash, file.SizeBytes));
                }
                foreach (var child in await folders.GetSubFoldersAsync(folder.Id, cancellationToken)) queue.Enqueue(child);
            }
        }

        await changes.AddRangeAsync(entries, cancellationToken);
        devices.Delete(device);
        // One SaveChanges transaction preserves files and commits binding removal and events together.
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
