using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Folders.Commands;

public class DeleteFolderCommandHandler : IRequestHandler<DeleteFolderCommand, Result>
{
    private readonly IDriveFolderRepository _folderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteFolderCommandHandler(
        IDriveFolderRepository folderRepository,
        IUnitOfWork unitOfWork)
    {
        _folderRepository = folderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeleteFolderCommand request, CancellationToken cancellationToken)
    {
        var folder = await _folderRepository.GetByIdAsync(request.FolderId, cancellationToken);

        if (folder == null || folder.OwnerId != request.OwnerId)
        {
            return Result.Failure("Folder not found or you do not have permission to access it.");
        }

        await _folderRepository.DeleteRecursivelyAsync(folder.Id, cancellationToken);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
