using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Auth.Commands.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<string>>
{
    private readonly IIdentityService _identityService;

    public RegisterCommandHandler(IIdentityService identityService)
    {
        _identityService = identityService;
    }

    public Task<Result<string>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        return _identityService.RegisterAsync(request.Email, request.Password, request.FirstName, cancellationToken);
    }
}
