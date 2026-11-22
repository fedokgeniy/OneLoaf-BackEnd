using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.CoreApi.Application;
using OneLoaf.CoreApi.Application.Account;
using OneLoaf.CoreApi.Application.Auth;

namespace OneLoaf.CoreApi.UnitTests.Account;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddCoreApiApplication_RegistersServicesAndValidators()
    {
        var services = new ServiceCollection().AddCoreApiApplication();

        Assert.Contains(services, d => d.ServiceType == typeof(AuthService) && d.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, d => d.ServiceType == typeof(AccountService) && d.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, d => d.ServiceType == typeof(IValidator<RegisterRequest>));
        Assert.Contains(services, d => d.ServiceType == typeof(IValidator<LoginRequest>));
        Assert.Contains(services, d => d.ServiceType == typeof(IValidator<DeleteAccountRequest>));
    }
}
