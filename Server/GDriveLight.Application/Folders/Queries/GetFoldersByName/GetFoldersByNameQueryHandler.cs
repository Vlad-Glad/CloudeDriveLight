using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Folders.Queries;

public class GetFoldersByNameQueryHandler : IRequestHandler<GetFoldersByNameQuery, Result<IEnumerable<FolderQuery>>>
{
    private readonly IDriveFolderRepository _folderRepository;

    public GetFoldersByNameQueryHandler(IDriveFolderRepository folderRepository)
    {
        _folderRepository = folderRepository;
    }

    public async Task<Result<IEnumerable<FolderQuery>>> Handle(GetFoldersByNameQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<IEnumerable<FolderQuery>>.Success(new List<FolderQuery>());
        }

        var folders = await _folderRepository.SearchByNameAsync(request.Name, request.UserId, cancellationToken);

        var dtos = folders.Select(f => new FolderQuery(f.Id, f.Name, f.ParentFolderId, f.OwnerId));

        return Result<IEnumerable<FolderQuery>>.Success(dtos);
    }
}
