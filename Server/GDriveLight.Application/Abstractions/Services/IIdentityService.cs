using GDriveLight.Application.Common.Models;

namespace GDriveLight.Application.Abstractions.Services;

public interface IIdentityService
{
    Task<Result<string>> RegisterAsync(string email, string password, string firstName, CancellationToken cancellationToken = default);
    Task<Result<string>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
}
