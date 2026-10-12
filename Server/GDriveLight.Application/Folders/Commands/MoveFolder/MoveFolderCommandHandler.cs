using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using GDriveLight.Domain.Enums;
using GDriveLight.Domain.Models;
using MediatR;

namespace GDriveLight.Application.Folders.Commands;

public class MoveFolderCommandHandler : IRequestHandler<MoveFolderCommand, Result>
{
    private readonly IDriveFolderRepository _folderRepository;
    private readonly IChangeLogRepository _changeLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MoveFolderCommandHandler(
        IDriveFolderRepository folderRepository,
        IChangeLogRepository changeLogRepository,
        IUnitOfWork unitOfWork)
    {
        _folderRepository = folderRepository;
        _changeLogRepository = changeLogRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(MoveFolderCommand request, CancellationToken cancellationToken)
    {
        var folder = await _folderRepository.GetByIdAsync(request.FolderId, cancellationToken);

        if (folder == null)
        {
            return Result.Failure($"Folder with ID '{request.FolderId}' was not found.");
        }

        if (folder.OwnerId != request.UserId)
        {
            return Result.Failure("You do not have permission to modify this folder.");
        }

        if (folder.ParentFolderId == request.NewParentFolderId)
        {
            return Result.Success();
        }

        if (request.NewParentFolderId.HasValue)
        {
            var newParent = await _folderRepository.GetByIdAsync(request.NewParentFolderId.Value, cancellationToken);
            if (newParent == null)
            {
                return Result.Failure($"Target parent folder with ID '{request.NewParentFolderId.Value}' was not found.");
            }

            if (newParent.OwnerId != request.UserId)
            {
                return Result.Failure("You do not have permission to access the target parent folder.");
            }

            var currentAncestor = newParent;
            while (currentAncestor != null)
            {
                if (currentAncestor.Id == request.FolderId)
                {
                    return Result.Failure("Cannot move a folder into its own descendant.");
                }

                if (currentAncestor.ParentFolderId.HasValue)
                {
                    currentAncestor = await _folderRepository.GetByIdAsync(currentAncestor.ParentFolderId.Value, cancellationToken);
                }
                else
                {
                    currentAncestor = null;
                }
            }

            bool exists = await _folderRepository.ExistsAsync(folder.Name, request.NewParentFolderId.Value, request.UserId, cancellationToken);
            if (exists)
            {
                return Result.Failure($"A folder with the name '{folder.Name}' already exists in the target location.");
            }
        }
        else
        {
            bool exists = await _folderRepository.ExistsAsync(folder.Name, null, request.UserId, cancellationToken);
            if (exists)
            {
                return Result.Failure($"A folder with the name '{folder.Name}' already exists in the root location.");
            }
        }

        try
        {
            folder.MoveTo(request.NewParentFolderId);
            _folderRepository.Update(folder);

            var changeLog = ChangeLogEntry.CreateFolderChange(
                folder.Id,
                ChangeType.Moved,
                request.UserId,
                folder.ParentFolderId,
                folder.Name,
                request.OriginDeviceId);
            await _changeLogRepository.AddAsync(changeLog, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
