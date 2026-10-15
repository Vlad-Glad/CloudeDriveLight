using GDriveLight.Application.Storage;
using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Common.Models;
using GDriveLight.Domain.Enums;
using GDriveLight.Domain.Models;
using MediatR;

namespace GDriveLight.Application.Files.Commands;

public class UploadFileCommandHandler : IRequestHandler<UploadFileCommand, Result<Guid>>
{
    private readonly IDriveFileRepository _fileRepository;
    private readonly IDriveFolderRepository _folderRepository;
    private readonly IFileTypeRepository _fileTypeRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IChangeLogRepository _changeLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStorageQuotaPolicy _quotaPolicy;

    public UploadFileCommandHandler(
        IDriveFileRepository fileRepository,
        IDriveFolderRepository folderRepository,
        IFileTypeRepository fileTypeRepository,
        IFileStorageService fileStorageService,
        IChangeLogRepository changeLogRepository,
        IUnitOfWork unitOfWork,
        IStorageQuotaPolicy quotaPolicy)
    {
        _fileRepository = fileRepository;
        _folderRepository = folderRepository;
        _fileTypeRepository = fileTypeRepository;
        _fileStorageService = fileStorageService;
        _changeLogRepository = changeLogRepository;
        _unitOfWork = unitOfWork;
        _quotaPolicy = quotaPolicy;
    }

    public async Task<Result<Guid>> Handle(UploadFileCommand request, CancellationToken cancellationToken)
    {
        if (request.FolderId.HasValue)
        {
            var folder = await _folderRepository.GetByIdAsync(request.FolderId.Value, cancellationToken);
            if (folder == null)
            {
                return Result<Guid>.Failure($"Folder with ID {request.FolderId.Value} was not found.");
            }

            if (folder.OwnerId != request.UserId)
            {
                return Result<Guid>.Failure("You do not have permission to upload to this folder.");
            }
        }

        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        var fileType = await _fileTypeRepository.GetByExtensionAsync(extension, cancellationToken);

        if (fileType == null)
        {
            return Result<Guid>.Failure($"File type '{extension}' is not supported.");
        }

        bool exists = await _fileRepository.ExistsAsync(request.FileName, request.FolderId, request.UserId, cancellationToken);
        if (exists)
        {
            return Result<Guid>.Failure($"A file with the name '{request.FileName}' already exists in this location.");
        }


        await using var content = await PreparedFileContent.CreateAsync(request.FileStream, cancellationToken);
        var fileUrl = await _fileStorageService.SaveAsync(content.Stream, request.FileName, cancellationToken);
        var committed = false;
        try
        {
            // Do not hold the user's database lock while hashing or writing physical content.
            await using var transaction = await _unitOfWork.BeginUserWriteAsync(request.UserId, cancellationToken);
            if (request.FolderId.HasValue)
            {
                var folder = await _folderRepository.GetByIdAsync(request.FolderId.Value, cancellationToken);
                if (folder == null || folder.OwnerId != request.UserId)
                    return Result<Guid>.Failure("The destination folder is no longer available.");
            }
            if (await _fileRepository.ExistsAsync(request.FileName, request.FolderId, request.UserId, cancellationToken))
                return Result<Guid>.Failure($"A file with the name '{request.FileName}' already exists in this location.");

            var usage = new StorageUsage(_quotaPolicy.GetLimitBytes(request.UserId),
                await _fileRepository.GetUsedBytesForUserAsync(request.UserId, cancellationToken));
            usage.EnsureCanGrow(content.SizeBytes);

            var driveFile = new DriveFile(
                request.FileName,
                fileUrl,
                fileType.Id,
                content.Hash,
                content.SizeBytes,
                request.UserId,
                request.FolderId
            );

            await _fileRepository.AddAsync(driveFile, cancellationToken);

            var changeLog = ChangeLogEntry.CreateFileChange(
                driveFile.Id,
                ChangeType.Created,
                request.UserId,
                request.FolderId,
                request.FileName,
                content.Hash,
                content.SizeBytes,
                request.OriginDeviceId);
            await _changeLogRepository.AddAsync(changeLog, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            committed = true;

            return Result<Guid>.Success(driveFile.Id);
        }
        finally
        {
            if (!committed)
                await _fileStorageService.DeleteAsync(fileUrl, CancellationToken.None);
        }
    }
}
