using System.Security.Claims;
using EngageOps.Api.Http;
using EngageOps.Api.Identity;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EngageOps.Api.Workers;

public static class WorkerEndpoints
{
    public static void MapWorkerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/organisations/{organisationId:guid}/workers",
                GetWorkersAsync)
            .RequireAuthorization();

        endpoints.MapPost(
                "/api/organisations/{organisationId:guid}/workers",
                CreateWorkerAsync)
            .RequireAuthorization();
    }

    private static async Task<IResult> GetWorkersAsync(
        Guid organisationId,
        int? page,
        int? pageSize,
        HttpContext context,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        OrganisationMembershipChecker membershipChecker,
        EngageOpsDbContext database,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";

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

        var requestedPage = page ?? 1;
        var requestedPageSize = pageSize ?? Pagination.DefaultPageSize;
        var paginationErrors = Pagination.Validate(
            requestedPage,
            requestedPageSize,
            out var offset);
        if (paginationErrors.Count > 0)
        {
            return TypedResults.ValidationProblem(paginationErrors);
        }

        if (!await membershipChecker.IsMemberAsync(userId, organisationId, cancellationToken))
        {
            return OrganisationNotFound();
        }

        var workerQuery = database.Workers
            .AsNoTracking()
            .Where(worker => worker.OrganisationId == organisationId);
        var totalCount = await workerQuery.CountAsync(cancellationToken);
        IReadOnlyList<WorkerResponse> workers = offset >= totalCount
            ? []
            : await workerQuery
                .OrderBy(worker => worker.Name)
                .ThenBy(worker => worker.Id)
                .Skip(offset)
                .Take(requestedPageSize)
                .Select(worker => new WorkerResponse(
                    worker.Id,
                    worker.OrganisationId,
                    worker.Name))
                .ToListAsync(cancellationToken);

        return TypedResults.Ok(new WorkerPageResponse(
            workers,
            requestedPage,
            requestedPageSize,
            totalCount));
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

    public sealed record WorkerPageResponse(
        IReadOnlyList<WorkerResponse> Items,
        int Page,
        int PageSize,
        int TotalCount);
}
