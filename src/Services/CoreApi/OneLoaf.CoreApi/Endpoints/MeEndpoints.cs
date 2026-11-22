using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OneLoaf.CoreApi.Application.Account;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Validation;

namespace OneLoaf.CoreApi.Endpoints;

/// <summary>The current user's account (<c>/me</c>). The user always comes from the token (US-03).</summary>
public static class MeEndpoints
{
    /// <summary>Route of the group.</summary>
    public const string GroupRoute = Routes.ApiV1 + "/me";

    /// <summary>Maps the endpoints.</summary>
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(GroupRoute)
            .WithTags(ApiTags.Account)
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/", GetMeAsync)
            .WithName("GetMe")
            .WithSummary("Get the current user's profile")
            .WithDescription("Profile of the user identified by the access token.");

        group.MapDelete("/", DeleteMeAsync)
            .WithValidation<DeleteAccountRequest>()
            .WithName("DeleteMe")
            .WithSummary("Delete the current account and all its data")
            .WithDescription("US-04. Requires the current password; removes the whole workspace by cascade.")
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<Ok<UserProfileDto>> GetMeAsync(AccountService account, CancellationToken cancellationToken) =>
        TypedResults.Ok(await account.GetMeAsync(cancellationToken));

    private static async Task<NoContent> DeleteMeAsync(
        [FromBody] DeleteAccountRequest request, AccountService account, CancellationToken cancellationToken)
    {
        await account.DeleteMeAsync(request, cancellationToken);
        return TypedResults.NoContent();
    }
}
