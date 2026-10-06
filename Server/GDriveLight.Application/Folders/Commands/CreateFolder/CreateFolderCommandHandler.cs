using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using GDriveLight.Domain.Models;
using MediatR;

namespace GDriveLight.Application.Folders.Commands;

public class CreateFolderCommandHandler : IRequestHandler<CreateFolderCommand, Result<Guid>>
{
    private readonly IDriveFolderRepository _folderRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateFolderCommandHandler(
        IDriveFolderRepository folderRepository,
        IUnitOfWork unitOfWork)
    {
        _folderRepository = folderRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Guid>> Handle(CreateFolderCommand request, CancellationToken cancellationToken)
    {
        bool exists = await _folderRepository.ExistsAsync(request.Name, request.ParentFolderId, request.OwnerId, cancellationToken);
        if (exists)
        {
            return Result<Guid>.Failure($"A folder with the name '{request.Name}' already exists in this location.");
        }

        var folder = new DriveFolder(request.Name, request.OwnerId, request.ParentFolderId);

        await _folderRepository.AddAsync(folder, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(folder.Id);
    }
}
