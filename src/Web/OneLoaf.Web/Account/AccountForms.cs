using System.ComponentModel.DataAnnotations;

namespace OneLoaf.Web.Account;

/// <summary>Sign-in form (US-02). Only presence is checked: a wrong password is a 401, not a validation error.</summary>
public sealed class LoginForm
{
    /// <summary>E-mail.</summary>
    [Required(ErrorMessage = "Enter your e-mail.")]
    [StringLength(256, ErrorMessage = "The e-mail is too long.")]
    public string? Email { get; set; }

    /// <summary>Password.</summary>
    [Required(ErrorMessage = "Enter your password.")]
    [StringLength(128, ErrorMessage = "The password is too long.")]
    public string? Password { get; set; }
}

/// <summary>Registration form (US-01) with the same limits as the Core API validator.</summary>
public sealed class RegisterForm
{
    /// <summary>Name shown in the public library.</summary>
    [Required(ErrorMessage = "Enter a display name.")]
    [StringLength(100, ErrorMessage = "Use at most 100 characters.")]
    [RegularExpression(@".*\S.*", ErrorMessage = "Enter a display name.")]
    public string? DisplayName { get; set; }

    /// <summary>Login e-mail.</summary>
    [Required(ErrorMessage = "Enter your e-mail.")]
    [EmailAddress(ErrorMessage = "Enter a valid e-mail address.")]
    [StringLength(256, ErrorMessage = "The e-mail is too long.")]
    public string? Email { get; set; }

    /// <summary>Password, 8–128 characters.</summary>
    [Required(ErrorMessage = "Choose a password.")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Use 8 to 128 characters.")]
    public string? Password { get; set; }

    /// <summary>Terms and privacy notice accepted (LR-01/02).</summary>
    [Range(typeof(bool), "true", "true", ErrorMessage = "Accept the terms and the privacy notice to continue.")]
    public bool AcceptTerms { get; set; }
}
