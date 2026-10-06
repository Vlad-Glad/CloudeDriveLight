using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Folders.Queries;

public class GetFolderByIdQueryHandler : IRequestHandler<GetFolderByIdQuery, Result<FolderQuery>>
{
    private readonly IDriveFolderRepository _folderRepository;

    public GetFolderByIdQueryHandler(IDriveFolderRepository folderRepository)
    {
        _folderRepository = folderRepository;
    }

    public async Task<Result<FolderQuery>> Handle(GetFolderByIdQuery request, CancellationToken cancellationToken)
    {
        var folder = await _folderRepository.GetByIdAsync(request.Id, cancellationToken);

        if (folder == null)
        {
            return Result<FolderQuery>.Failure($"Folder with ID {request.Id} was not found.");
        }

        if (folder.OwnerId != request.UserId)
        {
            return Result<FolderQuery>.Failure("You do not have permission to access this folder.");
        }

        var dto = new FolderQuery(folder.Id, folder.Name, folder.ParentFolderId, folder.OwnerId);
        
        return Result<FolderQuery>.Success(dto);
    }
}
