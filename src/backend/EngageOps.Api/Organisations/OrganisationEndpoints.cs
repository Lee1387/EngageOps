using System.Security.Claims;
using EngageOps.Api.Identity;
using EngageOps.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EngageOps.Api.Organisations;

public static class OrganisationEndpoints
{
    public static void MapOrganisationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/organisations", GetOrganisationsAsync)
            .RequireAuthorization();
    }

    private static async Task<IResult> GetOrganisationsAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        EngageOpsDbContext context,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.CacheControl = "no-store";

        if (!Guid.TryParse(userManager.GetUserId(principal), out var userId))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Authentication is required.");
        }

        var organisations = await context.OrganisationMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .Join(
                context.Organisations.AsNoTracking(),
                membership => membership.OrganisationId,
                organisation => organisation.Id,
                (_, organisation) => organisation)
            .OrderBy(organisation => organisation.Name)
            .ThenBy(organisation => organisation.Id)
            .Select(organisation => new OrganisationSummaryResponse(
                organisation.Id,
                organisation.Name))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(organisations);
    }

    public sealed record OrganisationSummaryResponse(Guid Id, string Name);
}
