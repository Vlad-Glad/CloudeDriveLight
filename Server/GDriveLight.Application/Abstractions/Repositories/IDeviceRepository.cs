using GDriveLight.Domain.Models;

namespace GDriveLight.Application.Abstractions.Repositories;

public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<Device>> GetDevicesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    
    Task AddAsync(Device device, CancellationToken cancellationToken = default);
    
    void Update(Device device);
    
    void Delete(Device device);
}
