using System.Security.Claims;
using EngageOps.Api.Http;
using EngageOps.Api.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace EngageOps.Api.Assignments;

public static class AssignmentEndpoints
{
    public static void MapAssignmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/organisations/{organisationId:guid}/assignments",
                GetAssignmentsAsync)
            .RequireAuthorization();

        endpoints.MapPost(
                "/api/organisations/{organisationId:guid}/assignments",
                CreateAssignmentAsync)
            .RequireAuthorization();
    }

    private static async Task<IResult> GetAssignmentsAsync(
        Guid organisationId,
        int? page,
        int? pageSize,
        HttpContext context,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        AssignmentListQuery query,
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

        var result = await query.ExecuteAsync(
            userId,
            organisationId,
            offset,
            requestedPageSize,
            cancellationToken);

        return result switch
        {
            AssignmentListResult.Found found => TypedResults.Ok(new AssignmentPageResponse(
                found.Items
                    .Select(item => new AssignmentListItemResponse(
                        item.Id,
                        item.OrganisationId,
                        item.ClientId,
                        item.ClientName,
                        item.WorkerId,
                        item.WorkerName,
                        item.StartDate,
                        item.EndDate))
                    .ToList(),
                requestedPage,
                requestedPageSize,
                found.TotalCount)),
            AssignmentListResult.OrganisationNotFound => OrganisationNotFound(),
            _ => throw new InvalidOperationException("Unknown assignment list result."),
        };
    }

    private static async Task<IResult> CreateAssignmentAsync(
        Guid organisationId,
        CreateAssignmentRequest request,
        HttpContext context,
        ClaimsPrincipal principal,
        IAntiforgery antiforgery,
        UserManager<ApplicationUser> userManager,
        AssignmentCreator creator,
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

        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var result = await creator.CreateAsync(
            userId,
            organisationId,
            request.ClientId.GetValueOrDefault(),
            request.WorkerId.GetValueOrDefault(),
            request.StartDate.GetValueOrDefault(),
            request.EndDate,
            cancellationToken);

        return result switch
        {
            AssignmentCreationResult.Created created => TypedResults.Json(
                new AssignmentResponse(
                    created.Assignment.Id,
                    created.Assignment.OrganisationId,
                    created.Assignment.ClientId,
                    created.Assignment.WorkerId,
                    created.Assignment.StartDate,
                    created.Assignment.EndDate),
                statusCode: StatusCodes.Status201Created),
            AssignmentCreationResult.OrganisationNotFound => OrganisationNotFound(),
            AssignmentCreationResult.ClientNotFound => RelationshipNotFound(
                "clientId",
                "Client was not found in this organisation."),
            AssignmentCreationResult.WorkerNotFound => RelationshipNotFound(
                "workerId",
                "Worker was not found in this organisation."),
            _ => throw new InvalidOperationException("Unknown assignment creation result."),
        };
    }

    private static Dictionary<string, string[]> Validate(CreateAssignmentRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.ClientId is null || request.ClientId == Guid.Empty)
        {
            errors["clientId"] = ["Client identifier is required."];
        }

        if (request.WorkerId is null || request.WorkerId == Guid.Empty)
        {
            errors["workerId"] = ["Worker identifier is required."];
        }

        if (request.StartDate is null)
        {
            errors["startDate"] = ["Assignment start date is required."];
        }
        else
        {
            var dateValidationError = Assignment.GetDateValidationError(
                request.StartDate.Value,
                request.EndDate);
            if (dateValidationError is not null)
            {
                errors["endDate"] = [dateValidationError];
            }
        }

        return errors;
    }

    private static ProblemHttpResult OrganisationNotFound() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Organisation was not found.");

    private static ValidationProblem RelationshipNotFound(string key, string error) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            [key] = [error],
        });

    public sealed record CreateAssignmentRequest(
        Guid? ClientId,
        Guid? WorkerId,
        DateOnly? StartDate,
        DateOnly? EndDate);

    public sealed record AssignmentResponse(
        Guid Id,
        Guid OrganisationId,
        Guid ClientId,
        Guid WorkerId,
        DateOnly StartDate,
        DateOnly? EndDate);

    public sealed record AssignmentListItemResponse(
        Guid Id,
        Guid OrganisationId,
        Guid ClientId,
        string ClientName,
        Guid WorkerId,
        string WorkerName,
        DateOnly StartDate,
        DateOnly? EndDate);

    public sealed record AssignmentPageResponse(
        IReadOnlyList<AssignmentListItemResponse> Items,
        int Page,
        int PageSize,
        int TotalCount);
}
