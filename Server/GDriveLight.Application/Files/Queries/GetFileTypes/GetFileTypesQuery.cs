using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Queries;

public record GetFileTypesQuery : IRequest<Result<IEnumerable<FileTypeQuery>>>;

public record FileTypeQuery(int Id, string Name, string Extension);
