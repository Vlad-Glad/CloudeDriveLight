using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Folders.Commands;

public class RenameFolderCommandHandler : IRequestHandler<RenameFolderCommand, Result>
{
    private readonly IDriveFolderRepository _folderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RenameFolderCommandHandler(
        IDriveFolderRepository folderRepository,
        IUnitOfWork unitOfWork)
    {
        _folderRepository = folderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(RenameFolderCommand request, CancellationToken cancellationToken)
    {
        var folder = await _folderRepository.GetByIdAsync(request.FolderId, cancellationToken);

        if (folder == null || folder.OwnerId != request.OwnerId)
        {
            return Result.Failure("Folder not found or you do not have permission to access it.");
        }

        bool exists = await _folderRepository.ExistsAsync(request.NewName, folder.ParentFolderId, request.OwnerId, cancellationToken);
        if (exists)
        {
            return Result.Failure($"A folder with the name '{request.NewName}' already exists in this location.");
        }

        folder.Rename(request.NewName);

        _folderRepository.Update(folder);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
