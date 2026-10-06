using System.Security.Cryptography;
using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Common.Models;
using GDriveLight.Domain.Models;
using MediatR;

namespace GDriveLight.Application.Files.Commands;

public class UploadFileCommandHandler : IRequestHandler<UploadFileCommand, Result<Guid>>
{
    private readonly IDriveFileRepository _fileRepository;
    private readonly IDriveFolderRepository _folderRepository;
    private readonly IFileTypeRepository _fileTypeRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IUnitOfWork _unitOfWork;

    public UploadFileCommandHandler(
        IDriveFileRepository fileRepository,
        IDriveFolderRepository folderRepository,
        IFileTypeRepository fileTypeRepository,
        IFileStorageService fileStorageService,
        IUnitOfWork unitOfWork)
    {
        _fileRepository = fileRepository;
        _folderRepository = folderRepository;
        _fileTypeRepository = fileTypeRepository;
        _fileStorageService = fileStorageService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(UploadFileCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify folder exists and belongs to user (if folderId is provided)
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

        // 2. Determine FileType
        var extension = Path.GetExtension(request.FileName).ToLowerInvariant();
        var fileType = await _fileTypeRepository.GetByExtensionAsync(extension, cancellationToken);
        
        if (fileType == null)
        {
            return Result<Guid>.Failure($"File type '{extension}' is not supported.");
        }

        // 3. Check for name collisions
        bool exists = await _fileRepository.ExistsAsync(request.FileName, request.FolderId, request.UserId, cancellationToken);
        if (exists)
        {
            return Result<Guid>.Failure($"A file with the name '{request.FileName}' already exists in this location.");
        }

        // 4. Calculate Content Hash
        // Ensure stream is at the beginning
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

        // Reset stream position for saving
        if (request.FileStream.CanSeek)
        {
            request.FileStream.Position = 0;
        }

        // 5. Save the file
        var fileUrl = await _fileStorageService.SaveAsync(request.FileStream, request.FileName, cancellationToken);

        // 6. Create domain entity and save to DB
        var driveFile = new DriveFile(
            request.FileName,
            fileUrl,
            fileType.Id,
            contentHash,
            request.UserId,
            request.FolderId
        );

        await _fileRepository.AddAsync(driveFile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(driveFile.Id);
    }
}
