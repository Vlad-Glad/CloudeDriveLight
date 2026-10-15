using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Common.Models;
using GDriveLight.Domain.Enums;
using GDriveLight.Domain.Models;
using MediatR;

namespace GDriveLight.Application.Folders.Commands;

public class DeleteFolderCommandHandler : IRequestHandler<DeleteFolderCommand, Result>
{
    private readonly IDriveFolderRepository _folderRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IChangeLogRepository _changeLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteFolderCommandHandler(
        IDriveFolderRepository folderRepository,
        IFileStorageService fileStorageService,
        IChangeLogRepository changeLogRepository,
        IUnitOfWork unitOfWork)
    {
        _folderRepository = folderRepository;
        _fileStorageService = fileStorageService;
        _changeLogRepository = changeLogRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeleteFolderCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginUserWriteAsync(request.OwnerId, cancellationToken);
        var folder = await _folderRepository.GetByIdAsync(request.FolderId, cancellationToken);

        if (folder == null || folder.OwnerId != request.OwnerId)
        {
            return Result.Failure("Folder not found or you do not have permission to access it.");
        }

        var cascadeResult = await _folderRepository.DeleteRecursivelyAsync(folder.Id, cancellationToken);

        var changeLogs = new List<ChangeLogEntry>();

        foreach (var deletedFolder in cascadeResult.DeletedFolders)
        {
            changeLogs.Add(ChangeLogEntry.CreateFolderChange(
                deletedFolder.Id,
                ChangeType.Deleted,
                request.OwnerId,
                deletedFolder.ParentFolderId,
                deletedFolder.Name,
                request.OriginDeviceId));
        }

        foreach (var deletedFile in cascadeResult.DeletedFiles)
        {
            changeLogs.Add(ChangeLogEntry.CreateFileChange(
                deletedFile.Id,
                ChangeType.Deleted,
                request.OwnerId,
                deletedFile.FolderId,
                deletedFile.Name,
                deletedFile.ContentHash,
                deletedFile.SizeBytes,
                request.OriginDeviceId));
        }

        if (changeLogs.Count > 0)
        {
            await _changeLogRepository.AddRangeAsync(changeLogs, cancellationToken);
        }
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var url in cascadeResult.FileUrls)
        {
            await _fileStorageService.DeleteAsync(url, cancellationToken);
        }

        return Result.Success();
    }
}
