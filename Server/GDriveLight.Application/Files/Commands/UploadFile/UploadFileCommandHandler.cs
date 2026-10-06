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

        var fileUrl = await _fileStorageService.SaveAsync(request.FileStream, request.FileName, cancellationToken);

        try
        {
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
        catch
        {
            await _fileStorageService.DeleteAsync(fileUrl, cancellationToken);
            throw;
        }
    }
}
