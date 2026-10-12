using System.Security.Cryptography;
using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Common.Models;
using GDriveLight.Domain.Enums;
using GDriveLight.Domain.Models;
using MediatR;

namespace GDriveLight.Application.Files.Commands;

public class UpdateFileContentCommandHandler : IRequestHandler<UpdateFileContentCommand, Result>
{
    private readonly IDriveFileRepository _fileRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IChangeLogRepository _changeLogRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateFileContentCommandHandler(
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

    public async Task<Result> Handle(UpdateFileContentCommand request, CancellationToken cancellationToken)
    {
        var file = await _fileRepository.GetByIdAsync(request.FileId, cancellationToken);
        if (file == null)
        {
            return Result.Failure($"File with ID {request.FileId} was not found.");
        }

        if (file.OwnerId != request.UserId)
        {
            return Result.Failure("You do not have permission to update this file.");
        }

        if (request.FileStream.CanSeek)
        {
            request.FileStream.Position = 0;
        }

        string contentHash;
        using (var sha256 = SHA256.Create())
        {
            var hashBytes = await sha256.ComputeHashAsync(request.FileStream, cancellationToken);
            contentHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        if (request.FileStream.CanSeek)
        {
            request.FileStream.Position = 0;
        }

        var sizeBytes = request.FileStream.CanSeek ? request.FileStream.Length : 0L;
        var newFileUrl = await _fileStorageService.SaveAsync(request.FileStream, file.Name, cancellationToken);

        try
        {
            var oldFileUrl = file.FileUrl;
            file.UpdateContent(newFileUrl, contentHash, sizeBytes, request.UserId);

            _fileRepository.Update(file);

            var changeLog = ChangeLogEntry.CreateFileChange(
                file.Id,
                ChangeType.Modified,
                request.UserId,
                file.FolderId,
                file.Name,
                contentHash,
                sizeBytes,
                request.OriginDeviceId);

            await _changeLogRepository.AddAsync(changeLog, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _fileStorageService.DeleteAsync(oldFileUrl, cancellationToken);

            return Result.Success();
        }
        catch
        {
            await _fileStorageService.DeleteAsync(newFileUrl, cancellationToken);
            throw;
        }
    }
}
