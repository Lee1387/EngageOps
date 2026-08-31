using System.Security.Claims;
using EngageOps.Api.Http;
using EngageOps.Api.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace EngageOps.Api.Assignments;

public static class AssignmentEndpoints
{
    private const string GetAssignmentRouteName = "GetAssignment";

    public static void MapAssignmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/organisations/{organisationId:guid}/assignments",
                GetAssignmentsAsync)
            .RequireAuthorization();

        endpoints.MapGet(
                "/api/organisations/{organisationId:guid}/assignments/{assignmentId:guid}",
                GetAssignmentAsync)
            .WithName(GetAssignmentRouteName)
            .RequireAuthorization();

        endpoints.MapPost(
                "/api/organisations/{organisationId:guid}/assignments",
                CreateAssignmentAsync)
            .RequireAuthorization();

        endpoints.MapPost(
                "/api/organisations/{organisationId:guid}/assignments/{assignmentId:guid}/cancel",
                CancelAssignmentAsync)
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
                    .Select(ToResponse)
                    .ToList(),
                requestedPage,
                requestedPageSize,
                found.TotalCount)),
            AssignmentListResult.OrganisationNotFound => OrganisationNotFound(),
            _ => throw new InvalidOperationException("Unknown assignment list result."),
        };
    }

    private static async Task<IResult> GetAssignmentAsync(
        Guid organisationId,
        Guid assignmentId,
        HttpContext context,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        AssignmentDetailQuery query,
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

        var result = await query.ExecuteAsync(
            userId,
            organisationId,
            assignmentId,
            cancellationToken);

        return result switch
        {
            AssignmentDetailResult.Found found => TypedResults.Ok(ToResponse(found.Assignment)),
            AssignmentDetailResult.OrganisationNotFound => OrganisationNotFound(),
            AssignmentDetailResult.AssignmentNotFound => AssignmentNotFound(),
            _ => throw new InvalidOperationException("Unknown assignment detail result."),
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
            AssignmentCreationResult.Created created => TypedResults.CreatedAtRoute(
                new AssignmentResponse(
                    created.Assignment.Id,
                    created.Assignment.OrganisationId,
                    created.Assignment.ClientId,
                    created.Assignment.WorkerId,
                    created.Assignment.StartDate,
                    created.Assignment.EndDate,
                    created.Assignment.Status),
                GetAssignmentRouteName,
                new
                {
                    organisationId = created.Assignment.OrganisationId,
                    assignmentId = created.Assignment.Id,
                }),
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

    private static async Task<IResult> CancelAssignmentAsync(
        Guid organisationId,
        Guid assignmentId,
        HttpContext context,
        ClaimsPrincipal principal,
        IAntiforgery antiforgery,
        UserManager<ApplicationUser> userManager,
        AssignmentCanceller canceller,
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

        var result = await canceller.CancelAsync(
            userId,
            organisationId,
            assignmentId,
            cancellationToken);

        return result switch
        {
            AssignmentCancellationResult.Cancelled => TypedResults.NoContent(),
            AssignmentCancellationResult.AlreadyCancelled => TypedResults.NoContent(),
            AssignmentCancellationResult.OrganisationNotFound => OrganisationNotFound(),
            AssignmentCancellationResult.AssignmentNotFound => AssignmentNotFound(),
            _ => throw new InvalidOperationException("Unknown assignment cancellation result."),
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

    private static AssignmentSummaryResponse ToResponse(AssignmentSummary assignment) =>
        new(
            assignment.Id,
            assignment.OrganisationId,
            assignment.ClientId,
            assignment.ClientName,
            assignment.WorkerId,
            assignment.WorkerName,
            assignment.StartDate,
            assignment.EndDate,
            assignment.Status);

    private static ProblemHttpResult OrganisationNotFound() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Organisation was not found.");

    private static ProblemHttpResult AssignmentNotFound() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Assignment was not found.");

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
        DateOnly? EndDate,
        AssignmentStatus Status);

    public sealed record AssignmentSummaryResponse(
        Guid Id,
        Guid OrganisationId,
        Guid ClientId,
        string ClientName,
        Guid WorkerId,
        string WorkerName,
        DateOnly StartDate,
        DateOnly? EndDate,
        AssignmentStatus Status);

    public sealed record AssignmentPageResponse(
        IReadOnlyList<AssignmentSummaryResponse> Items,
        int Page,
        int PageSize,
        int TotalCount);
}
