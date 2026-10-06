using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Commands;

public class RenameFileCommandHandler : IRequestHandler<RenameFileCommand, Result>
{
    private readonly IDriveFileRepository _fileRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RenameFileCommandHandler(IDriveFileRepository fileRepository, IUnitOfWork unitOfWork)
    {
        _fileRepository = fileRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(RenameFileCommand request, CancellationToken cancellationToken)
    {
        var file = await _fileRepository.GetByIdAsync(request.FileId, cancellationToken);
        if (file == null)
        {
            return Result.Failure($"File with ID {request.FileId} was not found.");
        }

        if (file.OwnerId != request.UserId)
        {
            return Result.Failure("You do not have permission to rename this file.");
        }

        if (file.Name == request.NewName)
        {
            return Result.Success();
        }

        bool exists = await _fileRepository.ExistsAsync(request.NewName, file.FolderId, request.UserId, cancellationToken);
        if (exists)
        {
            return Result.Failure($"A file with the name '{request.NewName}' already exists in this folder.");
        }

        file.Rename(request.NewName);
        
        _fileRepository.Update(file);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
