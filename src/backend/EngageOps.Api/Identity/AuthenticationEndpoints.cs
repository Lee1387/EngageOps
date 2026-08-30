using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace EngageOps.Api.Identity;

public static class AuthenticationEndpoints
{
    public const string AntiforgeryHeaderName = "X-CSRF-TOKEN";

    private const int MaxEmailLength = 256;
    private const int MaxPasswordLength = 256;

    private static readonly EmailAddressAttribute EmailAddressValidator = new();

    public static void MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");

        group.MapGet("/csrf", GetAntiforgeryToken);
        group.MapPost("/sign-in", SignInAsync);
        group.MapGet("/session", GetSessionAsync)
            .RequireAuthorization();
        group.MapPost("/sign-out", SignOutAsync)
            .RequireAuthorization();
    }

    private static Ok<AntiforgeryTokenResponse> GetAntiforgeryToken(
        HttpContext context,
        IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);

        return TypedResults.Ok(new AntiforgeryTokenResponse(tokens.RequestToken!));
    }

    private static async Task<IResult> SignInAsync(
        SignInRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        SignInManager<ApplicationUser> signInManager)
    {
        context.Response.Headers.CacheControl = "no-store";

        var antiforgeryError = await AntiforgeryValidation.ValidateAsync(context, antiforgery);
        if (antiforgeryError is not null)
        {
            return antiforgeryError;
        }

        var validationErrors = Validate(request);
        if (validationErrors.Count > 0)
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var result = await signInManager.PasswordSignInAsync(
            request.Email!.Trim(),
            request.Password!,
            isPersistent: false,
            lockoutOnFailure: true);

        // Keep account existence, password failure, and lockout state indistinguishable to callers.
        return result.Succeeded
            ? TypedResults.NoContent()
            : TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid email or password.");
    }

    private static async Task<IResult> GetSessionAsync(
        HttpContext context,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager)
    {
        context.Response.Headers.CacheControl = "no-store";
        var user = await userManager.GetUserAsync(principal);

        return user is null
            ? TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Authentication is required.")
            : TypedResults.Ok(new SessionResponse(user.Id, user.Email));
    }

    private static async Task<IResult> SignOutAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        SignInManager<ApplicationUser> signInManager)
    {
        context.Response.Headers.CacheControl = "no-store";

        var antiforgeryError = await AntiforgeryValidation.ValidateAsync(context, antiforgery);
        if (antiforgeryError is not null)
        {
            return antiforgeryError;
        }

        await signInManager.SignOutAsync();

        return TypedResults.NoContent();
    }

    private static Dictionary<string, string[]> Validate(SignInRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var email = request.Email?.Trim();

        if (string.IsNullOrEmpty(email))
        {
            errors["email"] = ["Email is required."];
        }
        else if (email.Length > MaxEmailLength)
        {
            errors["email"] = [$"Email must not exceed {MaxEmailLength} characters."];
        }
        else if (email.Any(char.IsControl) || !EmailAddressValidator.IsValid(email))
        {
            errors["email"] = ["Email must be a valid email address."];
        }

        if (string.IsNullOrEmpty(request.Password))
        {
            errors["password"] = ["Password is required."];
        }
        else if (request.Password.Length > MaxPasswordLength)
        {
            errors["password"] = [$"Password must not exceed {MaxPasswordLength} characters."];
        }

        return errors;
    }

    public sealed record SignInRequest(string? Email, string? Password);

    public sealed record AntiforgeryTokenResponse(string Token);

    public sealed record SessionResponse(Guid UserId, string? Email);
}
