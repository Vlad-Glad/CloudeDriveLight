using GDriveLight.Domain.Models;

namespace GDriveLight.Application.Abstractions.Repositories;

public interface IFileTypeRepository
{
    Task<FileType?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    
    Task<FileType?> GetByExtensionAsync(string extension, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<FileType>> GetAllAsync(CancellationToken cancellationToken = default);
}
