using FluentValidation;
using OneLoaf.CoreApi.Application.Account;
using OneLoaf.CoreApi.Application.Common;

namespace OneLoaf.CoreApi.Application.Auth;

/// <summary>Validates <see cref="RegisterRequest"/> with the same limits as the database.</summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    /// <summary>Creates the rules.</summary>
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(ValidationLimits.EmailMaxLength)
            .Matches(ValidationLimits.EmailPattern).WithMessage("'Email' is not a valid e-mail address.");
        RuleFor(x => x.Password)
            .NotEmpty()
            .Length(ValidationLimits.PasswordMinLength, ValidationLimits.PasswordMaxLength);
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("'Display Name' must not be blank.")
            .MaximumLength(ValidationLimits.DisplayNameMaxLength);
    }
}

/// <summary>Validates <see cref="LoginRequest"/>. No password policy here: a wrong password is a 401, not a 400.</summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    /// <summary>Creates the rules.</summary>
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(ValidationLimits.EmailMaxLength);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(ValidationLimits.PasswordMaxLength);
    }
}

/// <summary>Validates <see cref="DeleteAccountRequest"/>.</summary>
public sealed class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    /// <summary>Creates the rules.</summary>
    public DeleteAccountRequestValidator()
    {
        RuleFor(x => x.Password).NotEmpty().MaximumLength(ValidationLimits.PasswordMaxLength);
    }
}
