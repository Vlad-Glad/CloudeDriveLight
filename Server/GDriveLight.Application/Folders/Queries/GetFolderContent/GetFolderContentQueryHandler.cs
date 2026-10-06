using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Folders.Queries;

public class GetFolderContentQueryHandler : IRequestHandler<GetFolderContentQuery, Result<IEnumerable<FolderQuery>>>
{
    private readonly IDriveFolderRepository _folderRepository;

    public GetFolderContentQueryHandler(IDriveFolderRepository folderRepository)
    {
        _folderRepository = folderRepository;
    }

    public async Task<Result<IEnumerable<FolderQuery>>> Handle(GetFolderContentQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<Domain.Models.DriveFolder> folders;

        if (request.ParentFolderId.HasValue)
        {
            var parent = await _folderRepository.GetByIdAsync(request.ParentFolderId.Value, cancellationToken);
            if (parent == null)
            {
                 return Result<IEnumerable<FolderQuery>>.Failure($"Folder with ID {request.ParentFolderId.Value} was not found.");
            }
            if (parent.OwnerId != request.UserId)
            {
                 return Result<IEnumerable<FolderQuery>>.Failure($"You do not have permission to access this folder.");
            }

            folders = await _folderRepository.GetSubFoldersAsync(request.ParentFolderId.Value, cancellationToken);
        }
        else
        {
            folders = await _folderRepository.GetRootFoldersForUserAsync(request.UserId, cancellationToken);
        }

        var dtos = folders.Select(f => new FolderQuery(f.Id, f.Name, f.ParentFolderId, f.OwnerId));

        return Result<IEnumerable<FolderQuery>>.Success(dtos);
    }
}
