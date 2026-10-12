using FluentValidation;

namespace GDriveLight.Application.Sync.Queries;

public class GetChangesQueryValidator : AbstractValidator<GetChangesQuery>
{
    public GetChangesQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID cannot be empty.");

        RuleFor(x => x.Cursor)
            .GreaterThanOrEqualTo(0).WithMessage("Cursor must be greater than or equal to 0.");

        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 1000).WithMessage("Limit must be between 1 and 1000.");
    }
}
