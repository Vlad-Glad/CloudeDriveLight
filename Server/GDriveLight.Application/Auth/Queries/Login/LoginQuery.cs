using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Auth.Queries.Login;

public record LoginQuery(string Email, string Password) : IRequest<Result<string>>;
