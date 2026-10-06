using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Queries;

public class GetFilesByFolderQueryHandler : IRequestHandler<GetFilesByFolderQuery, Result<IEnumerable<FileQuery>>>
{
    private readonly IDriveFileRepository _fileRepository;
    private readonly IDriveFolderRepository _folderRepository;

    public GetFilesByFolderQueryHandler(
        IDriveFileRepository fileRepository,
        IDriveFolderRepository folderRepository)
    {
        _fileRepository = fileRepository;
        _folderRepository = folderRepository;
    }

    public async Task<Result<IEnumerable<FileQuery>>> Handle(GetFilesByFolderQuery request, CancellationToken cancellationToken)
    {
        if (request.FolderId.HasValue)
        {
            var folder = await _folderRepository.GetByIdAsync(request.FolderId.Value, cancellationToken);
            if (folder == null)
            {
                return Result<IEnumerable<FileQuery>>.Failure($"Folder with ID {request.FolderId.Value} was not found.");
            }

            if (folder.OwnerId != request.UserId)
            {
                return Result<IEnumerable<FileQuery>>.Failure("You do not have permission to access this folder.");
            }
        }

        var files = request.FolderId.HasValue
            ? await _fileRepository.GetByFolderIdAsync(request.FolderId.Value, cancellationToken)
            : await _fileRepository.GetRootFilesForUserAsync(request.UserId, cancellationToken);

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
