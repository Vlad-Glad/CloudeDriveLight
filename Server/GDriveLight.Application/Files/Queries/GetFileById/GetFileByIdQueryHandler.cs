using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Queries;

public class GetFileByIdQueryHandler : IRequestHandler<GetFileByIdQuery, Result<FileQuery>>
{
    private readonly IDriveFileRepository _fileRepository;

    public GetFileByIdQueryHandler(IDriveFileRepository fileRepository)
    {
        _fileRepository = fileRepository;
    }

    public async Task<Result<FileQuery>> Handle(GetFileByIdQuery request, CancellationToken cancellationToken)
    {
        var file = await _fileRepository.GetByIdAsync(request.FileId, cancellationToken);

        if (file == null)
        {
            return Result<FileQuery>.Failure($"File with ID {request.FileId} was not found.");
        }

        if (file.OwnerId != request.UserId)
        {
            return Result<FileQuery>.Failure("You do not have permission to access this file.");
        }

        var dto = new FileQuery(
            file.Id,
            file.Name,
            file.FileTypeId,
            file.ContentHash,
            file.FolderId,
            file.OwnerId,
            file.UploadedAtUtc,
            file.EditedAtUtc,
            file.FileUrl
        );

        return Result<FileQuery>.Success(dto);
    }
}
