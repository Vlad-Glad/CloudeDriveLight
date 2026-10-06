using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Queries;

public class DownloadFileQueryHandler : IRequestHandler<DownloadFileQuery, Result<DownloadFileResult>>
{
    private readonly IDriveFileRepository _fileRepository;
    private readonly IFileTypeRepository _fileTypeRepository;
    private readonly IFileStorageService _fileStorageService;

    public DownloadFileQueryHandler(
        IDriveFileRepository fileRepository,
        IFileTypeRepository fileTypeRepository,
        IFileStorageService fileStorageService)
    {
        _fileRepository = fileRepository;
        _fileTypeRepository = fileTypeRepository;
        _fileStorageService = fileStorageService;
    }

    public async Task<Result<DownloadFileResult>> Handle(DownloadFileQuery request, CancellationToken cancellationToken)
    {
        var file = await _fileRepository.GetByIdAsync(request.FileId, cancellationToken);
        if (file == null)
        {
            return Result<DownloadFileResult>.Failure($"File with ID {request.FileId} was not found.");
        }

        if (file.OwnerId != request.UserId)
        {
            return Result<DownloadFileResult>.Failure("You do not have permission to download this file.");
        }

        var fileType = await _fileTypeRepository.GetByIdAsync(file.FileTypeId, cancellationToken);
        var mimeType = fileType?.MimeType ?? "application/octet-stream";

        try
        {
            var stream = await _fileStorageService.GetAsync(file.FileUrl, cancellationToken);
            var result = new DownloadFileResult(stream, file.Name, mimeType);
            return Result<DownloadFileResult>.Success(result);
        }
        catch (FileNotFoundException)
        {
            return Result<DownloadFileResult>.Failure("The physical file was not found on the server.");
        }
    }
}
