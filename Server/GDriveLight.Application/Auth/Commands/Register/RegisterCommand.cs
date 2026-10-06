using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Auth.Commands.Register;

public record RegisterCommand(string Email, string Password, string FirstName) : IRequest<Result<string>>;
