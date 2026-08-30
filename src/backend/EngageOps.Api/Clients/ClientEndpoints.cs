using System.Security.Claims;
using EngageOps.Api.Identity;
using EngageOps.Api.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EngageOps.Api.Clients;

public static class ClientEndpoints
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;

    public static void MapClientEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/organisations/{organisationId:guid}/clients",
                GetClientsAsync)
            .RequireAuthorization();

        endpoints.MapPost(
                "/api/organisations/{organisationId:guid}/clients",
                CreateClientAsync)
            .RequireAuthorization();
    }

    private static async Task<IResult> GetClientsAsync(
        Guid organisationId,
        int? page,
        int? pageSize,
        HttpContext context,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
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
        var requestedPageSize = pageSize ?? DefaultPageSize;
        var paginationErrors = ValidatePagination(requestedPage, requestedPageSize);
        if (paginationErrors.Count > 0)
        {
            return TypedResults.ValidationProblem(paginationErrors);
        }

        var offset = (long)(requestedPage - 1) * requestedPageSize;
        if (offset > int.MaxValue)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["page"] = ["Page is too large."],
            });
        }

        var isMember = await database.OrganisationMemberships.AnyAsync(
            membership =>
                membership.OrganisationId == organisationId && membership.UserId == userId,
            cancellationToken);

        if (!isMember)
        {
            return OrganisationNotFound();
        }

        var clientQuery = database.Clients
            .AsNoTracking()
            .Where(client => client.OrganisationId == organisationId);
        var totalCount = await clientQuery.CountAsync(cancellationToken);
        IReadOnlyList<ClientResponse> clients = offset >= totalCount
            ? []
            : await clientQuery
                .OrderBy(client => client.Name)
                .ThenBy(client => client.Id)
                .Skip((int)offset)
                .Take(requestedPageSize)
                .Select(client => new ClientResponse(
                    client.Id,
                    client.OrganisationId,
                    client.Name))
                .ToListAsync(cancellationToken);

        return TypedResults.Ok(new ClientPageResponse(
            clients,
            requestedPage,
            requestedPageSize,
            totalCount));
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

    private static Dictionary<string, string[]> ValidatePagination(int page, int pageSize)
    {
        var errors = new Dictionary<string, string[]>();

        if (page < 1)
        {
            errors["page"] = ["Page must be at least 1."];
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            errors["pageSize"] = [$"Page size must be between 1 and {MaxPageSize}."];
        }

        return errors;
    }

    public sealed record CreateClientRequest(string? Name);

    public sealed record ClientResponse(Guid Id, Guid OrganisationId, string Name);

    public sealed record ClientPageResponse(
        IReadOnlyList<ClientResponse> Items,
        int Page,
        int PageSize,
        int TotalCount);
}
