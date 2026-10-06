using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Commands;

public class DeleteFileCommandHandler : IRequestHandler<DeleteFileCommand, Result>
{
    private readonly IDriveFileRepository _fileRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteFileCommandHandler(
        IDriveFileRepository fileRepository,
        IFileStorageService fileStorageService,
        IUnitOfWork unitOfWork)
    {
        _fileRepository = fileRepository;
        _fileStorageService = fileStorageService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(DeleteFileCommand request, CancellationToken cancellationToken)
    {
        var file = await _fileRepository.GetByIdAsync(request.FileId, cancellationToken);
        if (file == null)
        {
            return Result.Failure($"File with ID {request.FileId} was not found.");
        }

        if (file.OwnerId != request.UserId)
        {
            return Result.Failure("You do not have permission to delete this file.");
        }

        // Delete from physical storage
        await _fileStorageService.DeleteAsync(file.FileUrl, cancellationToken);

        // Delete from database
        _fileRepository.Delete(file);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
