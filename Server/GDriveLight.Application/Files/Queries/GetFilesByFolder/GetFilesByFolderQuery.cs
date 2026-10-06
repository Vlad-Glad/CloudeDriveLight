using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Queries;

public record GetFilesByFolderQuery(Guid? FolderId, Guid UserId) : IRequest<Result<IEnumerable<FileQuery>>>;
