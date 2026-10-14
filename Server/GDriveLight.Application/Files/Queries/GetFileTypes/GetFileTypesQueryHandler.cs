using GDriveLight.Application.Abstractions.Repositories;
using GDriveLight.Application.Common.Models;
using MediatR;

namespace GDriveLight.Application.Files.Queries;

public sealed class GetFileTypesQueryHandler(IFileTypeRepository repository)
    : IRequestHandler<GetFileTypesQuery, Result<IEnumerable<FileTypeQuery>>>
{
    public async Task<Result<IEnumerable<FileTypeQuery>>> Handle(GetFileTypesQuery request, CancellationToken cancellationToken)
    {
        var types = await repository.GetAllAsync(cancellationToken);
        return Result<IEnumerable<FileTypeQuery>>.Success(types
            .OrderBy(type => type.Extension, StringComparer.OrdinalIgnoreCase)
            .Select(type => new FileTypeQuery(type.Id, type.Name, type.Extension))
            .ToArray());
    }
}
