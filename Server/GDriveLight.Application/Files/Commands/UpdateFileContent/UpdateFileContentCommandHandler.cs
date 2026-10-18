using GDriveLight.Application.Storage;
using Microsoft.Extensions.Logging;
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
    private readonly IStorageQuotaPolicy _quotaPolicy;
    private readonly ILogger<UpdateFileContentCommandHandler> _logger;
    private readonly IFileContentPreparer _contentPreparer;

    public UpdateFileContentCommandHandler(
        IDriveFileRepository fileRepository,
        IFileStorageService fileStorageService,
        IChangeLogRepository changeLogRepository,
        IUnitOfWork unitOfWork,
        IStorageQuotaPolicy quotaPolicy,
        ILogger<UpdateFileContentCommandHandler> logger,
        IFileContentPreparer contentPreparer)
    {
        _fileRepository = fileRepository;
        _fileStorageService = fileStorageService;
        _changeLogRepository = changeLogRepository;
        _unitOfWork = unitOfWork;
        _quotaPolicy = quotaPolicy;
        _logger = logger;
        _contentPreparer = contentPreparer;
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

        await using var content = await _contentPreparer.PrepareAsync(request.FileStream, cancellationToken);
        var newFileUrl = await _fileStorageService.SaveAsync(content.Stream, file.Name, cancellationToken);
        var committed = false;
        string? oldFileUrl = null;
        try
        {
            await using var transaction = await _unitOfWork.BeginUserWriteAsync(request.UserId, cancellationToken);
            // Another client may have updated, moved or deleted this file during staging.
            file = await _fileRepository.GetByIdAsync(request.FileId, cancellationToken);
            if (file == null || file.OwnerId != request.UserId)
                return Result.Failure("The file is no longer available.");

            var usage = new StorageUsage(_quotaPolicy.GetLimitBytes(request.UserId),
                await _fileRepository.GetUsedBytesForUserAsync(request.UserId, cancellationToken));
            usage.EnsureCanGrow(content.SizeBytes, file.SizeBytes);

            oldFileUrl = file.FileUrl;
            file.UpdateContent(newFileUrl, content.Hash, content.SizeBytes, request.UserId);

            _fileRepository.Update(file);

            var changeLog = ChangeLogEntry.CreateFileChange(
                file.Id,
                ChangeType.Modified,
                request.UserId,
                file.FolderId,
                file.Name,
                content.Hash,
                content.SizeBytes,
                request.OriginDeviceId);

            await _changeLogRepository.AddAsync(changeLog, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;
        }
        finally
        {
            if (!committed)
                await _fileStorageService.DeleteAsync(newFileUrl, CancellationToken.None);
        }

        try
        {
            await _fileStorageService.DeleteAsync(oldFileUrl!, CancellationToken.None);
        }
        catch (Exception error)
        {
            // The new file is committed. A cleanup failure must never delete live content.
            _logger.LogWarning(error, "Could not remove superseded content {FileUrl}", oldFileUrl);
        }
        return Result.Success();
    }
}
