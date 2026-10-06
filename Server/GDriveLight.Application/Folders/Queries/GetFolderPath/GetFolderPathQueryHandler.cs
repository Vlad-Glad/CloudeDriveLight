using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Folders.Queries;

public class GetFolderPathQueryHandler : IRequestHandler<GetFolderPathQuery, Result<IEnumerable<FolderQuery>>>
{
    private readonly IDriveFolderRepository _folderRepository;

    public GetFolderPathQueryHandler(IDriveFolderRepository folderRepository)
    {
        _folderRepository = folderRepository;
    }

    public async Task<Result<IEnumerable<FolderQuery>>> Handle(GetFolderPathQuery request, CancellationToken cancellationToken)
    {
        var folder = await _folderRepository.GetByIdAsync(request.FolderId, cancellationToken);
        if (folder == null)
        {
            return Result<IEnumerable<FolderQuery>>.Failure($"Folder with ID {request.FolderId} was not found.");
        }

        if (folder.OwnerId != request.UserId)
        {
            return Result<IEnumerable<FolderQuery>>.Failure("You do not have permission to access this folder.");
        }

        var path = new List<FolderQuery>();
        var current = folder;

        int maxDepth = 50;
        int depth = 0;

        while (current != null && depth < maxDepth)
        {
            path.Add(new FolderQuery(current.Id, current.Name, current.ParentFolderId, current.OwnerId));

            if (current.ParentFolderId.HasValue)
            {
                current = await _folderRepository.GetByIdAsync(current.ParentFolderId.Value, cancellationToken);
            }
            else
            {
                current = null;
            }
            depth++;
        }

        path.Reverse();

        return Result<IEnumerable<FolderQuery>>.Success(path);
    }
}
