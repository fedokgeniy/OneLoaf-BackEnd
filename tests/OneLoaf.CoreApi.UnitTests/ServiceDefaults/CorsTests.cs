using Microsoft.Extensions.Configuration;
using OneLoaf.ServiceDefaults.Cors;

namespace OneLoaf.CoreApi.UnitTests.ServiceDefaults;

public sealed class CorsTests
{
    [Fact]
    public void ReadOrigins_ArrayFromAppSettings_ReturnsOrigins()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "http://localhost:5090/",
            ["Cors:AllowedOrigins:1"] = "",
        });

        Assert.Equal(["http://localhost:5090"], CorsExtensions.ReadOrigins(configuration));
    }

    [Fact]
    public void ReadOrigins_CommaSeparatedEnvironmentValue_SplitsAndDeduplicates()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins"] = "http://localhost:5090, https://staging.example; http://localhost:5090",
        });

        Assert.Equal(["http://localhost:5090", "https://staging.example"], CorsExtensions.ReadOrigins(configuration));
    }

    [Fact]
    public void ReadOrigins_ArrayAndEnvironmentValue_ReturnsBoth()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "http://localhost:5090",
            ["Cors:AllowedOrigins"] = "http://localhost:5091",
        });

        Assert.Equal(["http://localhost:5090", "http://localhost:5091"], CorsExtensions.ReadOrigins(configuration));
    }

    [Fact]
    public void ReadOrigins_NotConfigured_IsEmpty() =>
        Assert.Empty(CorsExtensions.ReadOrigins(Build([])));

    private static IConfiguration Build(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
