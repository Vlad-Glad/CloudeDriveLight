using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using GDriveLight.Domain.Models;
using MediatR;

namespace GDriveLight.Application.Folders.Commands.CreateFolder;

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
        var folder = new DriveFolder(request.Name, request.OwnerId, request.ParentFolderId);

        await _folderRepository.AddAsync(folder, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(folder.Id);
    }
}
