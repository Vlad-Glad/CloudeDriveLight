using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Queries;

public class SearchFilesQueryHandler : IRequestHandler<SearchFilesQuery, Result<IEnumerable<FileQuery>>>
{
    private readonly IDriveFileRepository _fileRepository;

    public SearchFilesQueryHandler(IDriveFileRepository fileRepository)
    {
        _fileRepository = fileRepository;
    }

    public async Task<Result<IEnumerable<FileQuery>>> Handle(SearchFilesQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<IEnumerable<FileQuery>>.Success(Enumerable.Empty<FileQuery>());
        }

        var files = await _fileRepository.SearchByNameAsync(request.Name, request.UserId, cancellationToken);

        var dtos = files.Select(file => new FileQuery(
            file.Id,
            file.Name,
            file.FileTypeId,
            file.ContentHash,
            file.FolderId,
            file.OwnerId,
            file.UploadedAtUtc,
            file.EditedAtUtc,
            file.FileUrl
        ));

        return Result<IEnumerable<FileQuery>>.Success(dtos);
    }
}
