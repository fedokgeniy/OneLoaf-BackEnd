using FluentValidation;

namespace OneLoaf.Notification.Application.Push;

/// <summary>Limits of the push requests.</summary>
public static class PushLimits
{
    /// <summary>Longest endpoint URL.</summary>
    public const int MaxEndpointLength = 1024;

    /// <summary>Longest key.</summary>
    public const int MaxKeyLength = 256;
}

/// <summary>Validates <see cref="SubscribeRequest"/>.</summary>
public sealed class SubscribeRequestValidator : AbstractValidator<SubscribeRequest>
{
    private const string Base64UrlPattern = "^[A-Za-z0-9_-]+={0,2}$";

    /// <summary>Creates the rules.</summary>
    public SubscribeRequestValidator()
    {
        RuleFor(x => x.Endpoint).NotEmpty().MaximumLength(PushLimits.MaxEndpointLength)
            .Must(e => Uri.TryCreate(e, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            .WithMessage("endpoint must be an absolute https URL.");
        RuleFor(x => x.Keys).NotNull();
        RuleFor(x => x.Keys.P256dh).NotEmpty().MaximumLength(PushLimits.MaxKeyLength).Matches(Base64UrlPattern).When(x => x.Keys is not null);
        RuleFor(x => x.Keys.Auth).NotEmpty().MaximumLength(PushLimits.MaxKeyLength).Matches(Base64UrlPattern).When(x => x.Keys is not null);
    }
}
