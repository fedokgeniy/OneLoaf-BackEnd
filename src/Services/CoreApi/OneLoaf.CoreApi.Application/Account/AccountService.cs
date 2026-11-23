using Microsoft.Extensions.Logging;
using OneLoaf.CoreApi.Application.Auth;
using OneLoaf.Messaging;
using OneLoaf.Messaging.Contracts;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.CoreApi.Application.Account;

/// <summary>The current user's own account: profile and deletion (US-04). Always scoped to <see cref="ICurrentUser"/>.</summary>
public sealed partial class AccountService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser,
    IEventPublisher events,
    TimeProvider timeProvider,
    ILogger<AccountService> logger)
{
    /// <summary>Message when the user of a valid token no longer exists.</summary>
    public const string AccountNotFoundMessage = "The account was not found.";

    /// <summary>Message when the confirmation password is wrong.</summary>
    public const string WrongPasswordMessage = "The password is incorrect.";

    /// <summary>Profile of the current user.</summary>
    /// <exception cref="NotFoundException">The account was deleted after the token was issued.</exception>
    public async Task<UserProfileDto> GetMeAsync(CancellationToken cancellationToken) =>
        await users.GetProfileAsync(currentUser.Id, cancellationToken)
        ?? throw new NotFoundException(AccountNotFoundMessage);

    /// <summary>
    /// Deletes the current user after re-checking the password. The database removes the whole workspace by cascade
    /// (US-04); other services are told with <c>account.deleted</c>.
    /// </summary>
    /// <exception cref="NotFoundException">The account no longer exists.</exception>
    /// <exception cref="ForbiddenException">The password is wrong.</exception>
    public async Task DeleteMeAsync(DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = currentUser.Id;
        var credentials = await users.FindByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(AccountNotFoundMessage);

        if (passwordHasher.Verify(credentials.PasswordHash, request.Password) == PasswordCheck.Failed)
        {
            throw new ForbiddenException(WrongPasswordMessage);
        }

        if (!await users.DeleteAsync(userId, cancellationToken))
        {
            throw new NotFoundException(AccountNotFoundMessage);
        }

        LogDeleted(logger, userId);
        await events.PublishAsync(new AccountDeleted(userId, timeProvider.GetUtcNow()), cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} deleted the account")]
    private static partial void LogDeleted(ILogger logger, Guid userId);
}
