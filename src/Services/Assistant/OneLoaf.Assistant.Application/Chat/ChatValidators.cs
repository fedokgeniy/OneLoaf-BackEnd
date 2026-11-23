using FluentValidation;

namespace OneLoaf.Assistant.Application.Chat;

/// <summary>Limits of chat requests.</summary>
public static class ChatLimits
{
    /// <summary>Longest message of the baker.</summary>
    public const int MaxMessageLength = 2000;

    /// <summary>Longest conversation title.</summary>
    public const int MaxTitleLength = 200;
}

/// <summary>Validates <see cref="SendMessageRequest"/>.</summary>
public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    /// <summary>Creates the rules.</summary>
    public SendMessageRequestValidator() =>
        RuleFor(x => x.Content).NotEmpty().Must(c => !string.IsNullOrWhiteSpace(c)).MaximumLength(ChatLimits.MaxMessageLength);
}

/// <summary>Validates <see cref="CreateConversationRequest"/>.</summary>
public sealed class CreateConversationRequestValidator : AbstractValidator<CreateConversationRequest>
{
    /// <summary>Creates the rules.</summary>
    public CreateConversationRequestValidator() => RuleFor(x => x.Title).MaximumLength(ChatLimits.MaxTitleLength);
}
