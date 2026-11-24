using System.Net;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.UnitTests.Api;

public sealed class ApiPipelineTests
{
    private const string ValidationProblem = """
        {"type":"https://oneloaf.app/problems/validation","title":"One or more validation errors occurred.","status":400,
         "errors":{"email":["'Email' is not a valid e-mail address."]},"correlationId":"6bb80fced45949ac9c7a0859fce4e297"}
        """;

    [Fact]
    public async Task PostAsync_Success_DeserializesBodyAndSendsCorrelationId()
    {
        var stub = StubHttpHandler.Json(HttpStatusCode.Created, """{"userId":"99cf6d64-ee83-4a4b-9f20-8cb359c15b6a"}""");
        var (client, _) = PipelineFactory.Create(stub);

        var result = await client.PostAsync<RegisterRequest, RegisterResponse>(
            "api/v1/auth/register", new RegisterRequest("a@b.c", "Rye-Bread-2026!", "Baker"), Xunit.TestContext.Current.CancellationToken);

        var success = Assert.IsType<ApiResult<RegisterResponse>.Success>(result);
        Assert.Equal(Guid.Parse("99cf6d64-ee83-4a4b-9f20-8cb359c15b6a"), success.Value.UserId);
        Assert.True(stub.LastRequest!.Headers.Contains("X-Correlation-ID"));
        Assert.Equal("http://localhost:5080/api/v1/auth/register", stub.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task PostAsync_ValidationProblem_ReturnsValidationErrorWithFields()
    {
        var stub = StubHttpHandler.Json(HttpStatusCode.BadRequest, ValidationProblem, "application/problem+json");
        var (client, _) = PipelineFactory.Create(stub);

        var result = await client.PostAsync<LoginRequest, LoginResponse>(
            "api/v1/auth/login", new LoginRequest("x", "y"), Xunit.TestContext.Current.CancellationToken);

        var error = Assert.IsType<ApiResult<LoginResponse>.Failure>(result).Error;
        Assert.Equal(ApiErrorKind.Validation, error.Kind);
        Assert.Equal(400, error.Status);
        Assert.Equal("'Email' is not a valid e-mail address.", Assert.Single(error.FieldErrors["Email"]));
        Assert.Equal("6bb80fced45949ac9c7a0859fce4e297", error.CorrelationId);
    }

    [Theory]
    [InlineData("token", true)]
    [InlineData(null, false)]
    public async Task SendAsync_Unauthorized_RaisesSessionEndOnlyForAuthenticatedRequests(string? bearer, bool expected)
    {
        var stub = StubHttpHandler.Json(HttpStatusCode.Unauthorized,
            """{"type":"https://oneloaf.app/problems/unauthorized","title":"Invalid e-mail or password.","status":401}""",
            "application/problem+json");
        var (client, events) = PipelineFactory.Create(stub, bearer);
        var raised = false;
        events.Unauthorized += (_, _) => raised = true;

        var result = await client.PostAsync("api/v1/auth/logout", Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(ApiErrorKind.Unauthorized, Assert.IsType<ApiResult.Failure>(result).Error.Kind);
        Assert.Equal(expected, raised);
    }

    [Fact]
    public async Task GetAsync_RateLimitedWithoutBody_UsesStatusAndRetryAfter()
    {
        var stub = new StubHttpHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(42));
            return response;
        });
        var (client, _) = PipelineFactory.Create(stub);

        var result = await client.GetAsync<UserDto>("api/v1/me", Xunit.TestContext.Current.CancellationToken);

        var error = Assert.IsType<ApiResult<UserDto>.Failure>(result).Error;
        Assert.Equal(ApiErrorKind.RateLimited, error.Kind);
        Assert.Equal(TimeSpan.FromSeconds(42), error.RetryAfter);
        Assert.NotNull(error.CorrelationId);
    }

    [Fact]
    public async Task DeleteAsync_NoConnection_ReturnsNetworkError()
    {
        var (client, _) = PipelineFactory.Create(StubHttpHandler.Throws(new HttpRequestException("offline")));

        var result = await client.DeleteAsync("api/v1/me", Xunit.TestContext.Current.CancellationToken);

        var error = Assert.IsType<ApiResult.Failure>(result).Error;
        Assert.Equal(ApiErrorKind.Network, error.Kind);
        Assert.Null(error.Status);
    }

    [Theory]
    [InlineData("https://oneloaf.app/problems/conflict", 409, ApiErrorKind.Conflict)]
    [InlineData("https://oneloaf.app/problems/business-rule", 422, ApiErrorKind.BusinessRule)]
    [InlineData("https://oneloaf.app/problems/service-unavailable", 503, ApiErrorKind.Unavailable)]
    [InlineData(null, 404, ApiErrorKind.NotFound)]
    [InlineData(null, 500, ApiErrorKind.Server)]
    public void FromProblem_TypeOrStatus_MapsToKind(string? type, int status, ApiErrorKind expected)
    {
        var error = ApiError.FromProblem(new ProblemDetailsDto(type, "Title", status, "Detail", null, null, null, null), status, "c", null);

        Assert.Equal(expected, error.Kind);
        Assert.Equal("Detail", error.Message);
    }
}
