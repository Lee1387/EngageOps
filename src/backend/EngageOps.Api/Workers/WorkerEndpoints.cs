using System.Security.Claims;
using EngageOps.Api.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace EngageOps.Api.Workers;

public static class WorkerEndpoints
{
    public static void MapWorkerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/organisations/{organisationId:guid}/workers",
                CreateWorkerAsync)
            .RequireAuthorization();
    }

    private static async Task<IResult> CreateWorkerAsync(
        Guid organisationId,
        CreateWorkerRequest request,
        HttpContext context,
        ClaimsPrincipal principal,
        IAntiforgery antiforgery,
        UserManager<ApplicationUser> userManager,
        WorkerCreator creator,
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

        var nameValidationError = Worker.GetNameValidationError(request.Name);
        if (nameValidationError is not null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["name"] = [nameValidationError],
            });
        }

        var worker = await creator.CreateAsync(
            userId,
            organisationId,
            request.Name!,
            cancellationToken);

        return worker is null
            ? OrganisationNotFound()
            : TypedResults.Json(
                new WorkerResponse(worker.Id, worker.OrganisationId, worker.Name),
                statusCode: StatusCodes.Status201Created);
    }

    private static ProblemHttpResult OrganisationNotFound() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Organisation was not found.");

    public sealed record CreateWorkerRequest(string? Name);

    public sealed record WorkerResponse(Guid Id, Guid OrganisationId, string Name);
}
