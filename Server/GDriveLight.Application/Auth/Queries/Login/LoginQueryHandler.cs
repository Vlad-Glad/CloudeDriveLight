using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Auth.Queries.Login;

public class LoginQueryHandler : IRequestHandler<LoginQuery, Result<string>>
{
    private readonly IIdentityService _identityService;

    public LoginQueryHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public Task<Result<string>> Handle(LoginQuery request, CancellationToken cancellationToken)
    {
        return _identityService.LoginAsync(request.Email, request.Password, cancellationToken);
    }
}
