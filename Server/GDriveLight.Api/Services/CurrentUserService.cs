using System.Security.Claims;
using GDriveLight.Application.Abstractions.Services;

namespace GDriveLight.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid UserId
    {
        get
        {
            var userIdString = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // In case standard NameIdentifier is not mapped, check "sub"
            if (string.IsNullOrEmpty(userIdString))
            {
                userIdString = _httpContextAccessor.HttpContext?.User?.FindFirstValue("sub");
            }

            if (Guid.TryParse(userIdString, out var userId))
            {
                return userId;
            }

            return Guid.Empty;
        }
    }

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
