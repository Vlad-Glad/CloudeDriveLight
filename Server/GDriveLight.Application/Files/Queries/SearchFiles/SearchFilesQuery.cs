using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Queries;

public record SearchFilesQuery(string Name, Guid UserId) : IRequest<Result<IEnumerable<FileQuery>>>;
