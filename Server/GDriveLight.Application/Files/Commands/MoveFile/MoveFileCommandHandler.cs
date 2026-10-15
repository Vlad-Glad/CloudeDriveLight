using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using GDriveLight.Domain.Enums;
using GDriveLight.Domain.Models;
using MediatR;

namespace GDriveLight.Application.Files.Commands;

public class MoveFileCommandHandler : IRequestHandler<MoveFileCommand, Result>
{
    private readonly IDriveFileRepository _fileRepository;
    private readonly IDriveFolderRepository _folderRepository;
    private readonly IChangeLogRepository _changeLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MoveFileCommandHandler(
        IDriveFileRepository fileRepository,
        IDriveFolderRepository folderRepository,
        IChangeLogRepository changeLogRepository,
        IUnitOfWork unitOfWork)
    {
        _fileRepository = fileRepository;
        _folderRepository = folderRepository;
        _changeLogRepository = changeLogRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(MoveFileCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginUserWriteAsync(request.UserId, cancellationToken);
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

        var changeLog = ChangeLogEntry.CreateFileChange(
            file.Id,
            ChangeType.Moved,
            request.UserId,
            file.FolderId,
            file.Name,
            file.ContentHash,
            file.SizeBytes,
            request.OriginDeviceId);
        await _changeLogRepository.AddAsync(changeLog, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
