using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Folders.Queries;

public record GetFoldersByNameQuery(string Name, Guid UserId) : IRequest<Result<IEnumerable<FolderQuery>>>;
