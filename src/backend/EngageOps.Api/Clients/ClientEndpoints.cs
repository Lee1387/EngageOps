using System.Security.Claims;
using EngageOps.Api.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace EngageOps.Api.Clients;

public static class ClientEndpoints
{
    public static void MapClientEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/organisations/{organisationId:guid}/clients",
                CreateClientAsync)
            .RequireAuthorization();
    }

    private static async Task<IResult> CreateClientAsync(
        Guid organisationId,
        CreateClientRequest request,
        HttpContext context,
        ClaimsPrincipal principal,
        IAntiforgery antiforgery,
        UserManager<ApplicationUser> userManager,
        ClientCreator creator,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";

        var antiforgeryError = await AntiforgeryValidation.ValidateAsync(context, antiforgery);
        if (antiforgeryError is not null)
        {
            return antiforgeryError;
        }

        if (!Guid.TryParse(userManager.GetUserId(principal), out var userId))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Authentication is required.");
        }

        if (organisationId == Guid.Empty)
        {
            return OrganisationNotFound();
        }

        var nameValidationError = Client.GetNameValidationError(request.Name);
        if (nameValidationError is not null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = [nameValidationError],
            });
        }

        var client = await creator.CreateAsync(
            userId,
            organisationId,
            request.Name!,
            cancellationToken);

        return client is null
            ? OrganisationNotFound()
            : TypedResults.Json(
                new ClientResponse(client.Id, client.OrganisationId, client.Name),
                statusCode: StatusCodes.Status201Created);
    }

    private static ProblemHttpResult OrganisationNotFound() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Organisation was not found.");

    public sealed record CreateClientRequest(string? Name);

    public sealed record ClientResponse(Guid Id, Guid OrganisationId, string Name);
}
