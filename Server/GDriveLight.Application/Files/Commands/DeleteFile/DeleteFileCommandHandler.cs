using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Common.Models;
using GDriveLight.Domain.Enums;
using GDriveLight.Domain.Models;
using MediatR;

namespace GDriveLight.Application.Files.Commands;

public class DeleteFileCommandHandler : IRequestHandler<DeleteFileCommand, Result>
{
    private readonly IDriveFileRepository _fileRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IChangeLogRepository _changeLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteFileCommandHandler(
        IDriveFileRepository fileRepository,
        IFileStorageService fileStorageService,
        IChangeLogRepository changeLogRepository,
        IUnitOfWork unitOfWork)
    {
        _fileRepository = fileRepository;
        _fileStorageService = fileStorageService;
        _changeLogRepository = changeLogRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeleteFileCommand request, CancellationToken cancellationToken)
    {
        var file = await _fileRepository.GetByIdAsync(request.FileId, cancellationToken);
        if (file == null)
        {
            return Result.Failure($"File with ID {request.FileId} was not found.");
        }

        if (file.OwnerId != request.UserId)
        {
            return Result.Failure("You do not have permission to delete this file.");
        }

        _fileRepository.Delete(file);

        var changeLog = ChangeLogEntry.CreateFileChange(
            file.Id,
            ChangeType.Deleted,
            request.UserId,
            file.FolderId,
            file.Name,
            file.ContentHash,
            file.SizeBytes,
            request.OriginDeviceId);
        await _changeLogRepository.AddAsync(changeLog, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _fileStorageService.DeleteAsync(file.FileUrl, cancellationToken);

        return Result.Success();
    }
}
