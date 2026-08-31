using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace EngageOps.Api.Identity;

internal static class AuthenticatedUser
{
    public static bool TryGetId(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        out Guid userId) =>
        Guid.TryParse(userManager.GetUserId(principal), out userId);

    public static ProblemHttpResult CreateRequiredProblem() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Authentication is required.");
}
