using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using GDriveLight.Domain.Enums;
using GDriveLight.Domain.Models;
using MediatR;

namespace GDriveLight.Application.Files.Commands;

public class RenameFileCommandHandler : IRequestHandler<RenameFileCommand, Result>
{
    private readonly IDriveFileRepository _fileRepository;
    private readonly IChangeLogRepository _changeLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RenameFileCommandHandler(
        IDriveFileRepository fileRepository,
        IChangeLogRepository changeLogRepository,
        IUnitOfWork unitOfWork)
    {
        _fileRepository = fileRepository;
        _changeLogRepository = changeLogRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(RenameFileCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await _unitOfWork.BeginUserWriteAsync(request.UserId, cancellationToken);
        var file = await _fileRepository.GetByIdAsync(request.FileId, cancellationToken);
        if (file == null)
        {
            return Result.Failure($"File with ID {request.FileId} was not found.");
        }

        if (file.OwnerId != request.UserId)
        {
            return Result.Failure("You do not have permission to rename this file.");
        }

        if (file.Name == request.NewName)
        {
            return Result.Success();
        }

        bool exists = await _fileRepository.ExistsAsync(request.NewName, file.FolderId, request.UserId, cancellationToken);
        if (exists)
        {
            return Result.Failure($"A file with the name '{request.NewName}' already exists in this folder.");
        }

        file.Rename(request.NewName);
        
        _fileRepository.Update(file);

        var changeLog = ChangeLogEntry.CreateFileChange(
            file.Id,
            ChangeType.Renamed,
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
