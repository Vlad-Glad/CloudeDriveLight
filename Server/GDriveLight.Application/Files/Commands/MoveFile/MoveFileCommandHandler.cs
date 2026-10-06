using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Commands;

public class MoveFileCommandHandler : IRequestHandler<MoveFileCommand, Result>
{
    private readonly IDriveFileRepository _fileRepository;
    private readonly IDriveFolderRepository _folderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MoveFileCommandHandler(
        IDriveFileRepository fileRepository,
        IDriveFolderRepository folderRepository,
        IUnitOfWork unitOfWork)
    {
        _fileRepository = fileRepository;
        _folderRepository = folderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(MoveFileCommand request, CancellationToken cancellationToken)
    {
        var file = await _fileRepository.GetByIdAsync(request.FileId, cancellationToken);
        if (file == null)
        {
            return Result.Failure($"File with ID {request.FileId} was not found.");
        }

        if (file.OwnerId != request.UserId)
        {
            return Result.Failure("You do not have permission to move this file.");
        }

        if (file.FolderId == request.NewFolderId)
        {
            return Result.Success();
        }

        if (request.NewFolderId.HasValue)
        {
            var folder = await _folderRepository.GetByIdAsync(request.NewFolderId.Value, cancellationToken);
            if (folder == null)
            {
                return Result.Failure($"Destination folder with ID {request.NewFolderId.Value} was not found.");
            }

            if (folder.OwnerId != request.UserId)
            {
                return Result.Failure("You do not have permission to access the destination folder.");
            }
        }

        bool exists = await _fileRepository.ExistsAsync(file.Name, request.NewFolderId, request.UserId, cancellationToken);
        if (exists)
        {
            return Result.Failure($"A file with the name '{file.Name}' already exists in the destination folder.");
        }

        file.MoveTo(request.NewFolderId);

        _fileRepository.Update(file);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
