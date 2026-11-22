using Microsoft.Extensions.Logging.Abstractions;
using OneLoaf.CoreApi.Application.Account;
using OneLoaf.CoreApi.UnitTests.Fakes;
using OneLoaf.SharedKernel.Exceptions;

namespace OneLoaf.CoreApi.UnitTests.Account;

public sealed class AccountServiceTests
{
    private readonly FakeUserRepository _users = new();
    private readonly FakePasswordHasher _hasher = new();

    [Fact]
    public async Task GetMeAsync_ExistingUser_ReturnsOwnProfileOnly()
    {
        var anna = _users.Seed("anna@example.com", FakePasswordHasher.Prefix + "secret-pass");
        _users.Seed("boris@example.com", FakePasswordHasher.Prefix + "other-pass");

        var profile = await Service(anna.Id).GetMeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(anna.Id, profile.Id);
        Assert.Equal("anna@example.com", profile.Email);
        Assert.Equal(FakeUserRepository.CreatedAt, profile.CreatedAt);
    }

    [Fact]
    public async Task GetMeAsync_DeletedUser_ThrowsNotFound()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => Service(Guid.NewGuid()).GetMeAsync(TestContext.Current.CancellationToken));

        Assert.Equal(AccountService.AccountNotFoundMessage, exception.Message);
    }

    [Fact]
    public async Task US04_DeleteMeWithCorrectPassword_DeletesOnlyTheCurrentUser()
    {
        var anna = _users.Seed("anna@example.com", FakePasswordHasher.Prefix + "secret-pass");
        var boris = _users.Seed("boris@example.com", FakePasswordHasher.Prefix + "other-pass");

        await Service(anna.Id).DeleteMeAsync(new DeleteAccountRequest("secret-pass"), TestContext.Current.CancellationToken);

        Assert.Equal(boris.Id, Assert.Single(_users.Users).Id);
    }

    [Fact]
    public async Task DeleteMeAsync_WrongPassword_ThrowsForbiddenAndKeepsAccount()
    {
        var anna = _users.Seed("anna@example.com", FakePasswordHasher.Prefix + "secret-pass");

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => Service(anna.Id)
            .DeleteMeAsync(new DeleteAccountRequest("wrong-pass"), TestContext.Current.CancellationToken));

        Assert.Equal(AccountService.WrongPasswordMessage, exception.Message);
        Assert.Single(_users.Users);
    }

    [Fact]
    public async Task DeleteMeAsync_AlreadyDeleted_ThrowsNotFound() =>
        await Assert.ThrowsAsync<NotFoundException>(() => Service(Guid.NewGuid())
            .DeleteMeAsync(new DeleteAccountRequest("secret-pass"), TestContext.Current.CancellationToken));

    [Fact]
    public async Task DeleteMeAsync_DeletedConcurrently_ThrowsNotFound()
    {
        var anna = _users.Seed("anna@example.com", FakePasswordHasher.Prefix + "secret-pass");
        var repository = new DeletingBetweenCallsRepository(_users);
        var service = new AccountService(repository, _hasher, new FakeCurrentUser(anna.Id),
            NullLogger<AccountService>.Instance);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.DeleteMeAsync(new DeleteAccountRequest("secret-pass"), TestContext.Current.CancellationToken));
    }

    private AccountService Service(Guid currentUserId) =>
        new(_users, _hasher, new FakeCurrentUser(currentUserId), NullLogger<AccountService>.Instance);

    /// <summary>Simulates a second request deleting the account between the password check and the delete.</summary>
    private sealed class DeletingBetweenCallsRepository(FakeUserRepository inner) : IUserRepository
    {
        public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            inner.EmailExistsAsync(normalizedEmail, cancellationToken);

        public Task<Guid> AddAsync(OneLoaf.Domain.Entities.User user, CancellationToken cancellationToken) =>
            inner.AddAsync(user, cancellationToken);

        public Task<UserCredentials?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            inner.FindByEmailAsync(normalizedEmail, cancellationToken);

        public Task<UserCredentials?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) =>
            inner.FindByIdAsync(userId, cancellationToken);

        public Task RecordLoginAsync(Guid userId, DateTimeOffset loggedInAt, string? newPasswordHash,
            CancellationToken cancellationToken) =>
            inner.RecordLoginAsync(userId, loggedInAt, newPasswordHash, cancellationToken);

        public Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken) =>
            inner.GetProfileAsync(userId, cancellationToken);

        public Task<bool> DeleteAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
