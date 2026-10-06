using FluentValidation;

namespace GDriveLight.Application.Auth.Queries.Login;

public class LoginQueryValidator : AbstractValidator<LoginQuery>
{
    public LoginQueryValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email is required.");
            
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
