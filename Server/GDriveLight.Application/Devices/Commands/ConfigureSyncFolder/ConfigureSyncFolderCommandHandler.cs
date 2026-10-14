using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using GDriveLight.Application.Devices.Queries;
using GDriveLight.Domain.Enums;
using GDriveLight.Domain.Models;
using MediatR;

namespace GDriveLight.Application.Devices.Commands.ConfigureSyncFolder;

public sealed class ConfigureSyncFolderCommandHandler(
    IDeviceRepository devices, ISyncFolderRepository syncFolders,
    IDriveFolderRepository folders, IUnitOfWork unitOfWork)
    : IRequestHandler<ConfigureSyncFolderCommand, Result<DeviceInfo>>
{
    public async Task<Result<DeviceInfo>> Handle(ConfigureSyncFolderCommand request, CancellationToken cancellationToken)
    {
        var device = await devices.GetByIdAsync(request.DeviceId, cancellationToken);
        if (device is null || device.UserId != request.UserId)
            return Result<DeviceInfo>.Failure("Device not found.");

        var syncFolder = (await syncFolders.GetByDeviceIdAsync(device.Id, cancellationToken)).FirstOrDefault();
        if (syncFolder is null)
        {
            var root = new DriveFolder($"Sync {device.Id:N}", request.UserId);
            await folders.AddAsync(root, cancellationToken);
            syncFolder = new SyncFolder(root.Id, device.Id, SyncMode.CloudToLocal);
            syncFolder.SetLocalPath(request.LocalPath);
            await syncFolders.AddAsync(syncFolder, cancellationToken);
        }
        else
        {
            syncFolder.SetLocalPath(request.LocalPath);
            syncFolders.Update(syncFolder);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<DeviceInfo>.Success(new DeviceInfo(device.Id, device.Name, syncFolder.DriveFolderId, syncFolder.LocalPath));
    }
}
