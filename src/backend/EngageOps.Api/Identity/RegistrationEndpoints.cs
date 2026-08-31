using EngageOps.Api.Organisations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;

namespace EngageOps.Api.Identity;

public static class RegistrationEndpoints
{
    public static void MapRegistrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/auth/register", RegisterAsync);
    }

    private static async Task<IResult> RegisterAsync(
        RegistrationRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        AccountProvisioner accountProvisioner,
        SignInManager<ApplicationUser> signInManager,
        CancellationToken cancellationToken)
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

        var result = await accountProvisioner.ProvisionAsync(
            request.Email!.Trim(),
            request.Password!,
            request.OrganisationName!,
            cancellationToken);

        return result switch
        {
            AccountProvisioningResult.Created created => await CompleteRegistrationAsync(
                created,
                signInManager),
            AccountProvisioningResult.Rejected rejected => TypedResults.ValidationProblem(
                ToValidationErrors(rejected.Errors)),
            _ => throw new InvalidOperationException("Unknown account provisioning result."),
        };
    }

    private static async Task<IResult> CompleteRegistrationAsync(
        AccountProvisioningResult.Created created,
        SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignInAsync(created.User, isPersistent: false);

        return TypedResults.Json(
            new RegistrationResponse(
                created.User.Id,
                created.User.Email
                    ?? throw new InvalidOperationException("The created account has no email address."),
                created.Organisation.Id,
                created.Organisation.Name),
            statusCode: StatusCodes.Status201Created);
    }

    private static Dictionary<string, string[]> Validate(RegistrationRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var emailError = AuthenticationInputValidation.GetEmailValidationError(request.Email);
        if (emailError is not null)
        {
            errors["email"] = [emailError];
        }

        var passwordError = AuthenticationInputValidation.GetPasswordBoundaryValidationError(
            request.Password);
        if (passwordError is not null)
        {
            errors["password"] = [passwordError];
        }

        var organisationNameError = Organisation.GetNameValidationError(
            request.OrganisationName);
        if (organisationNameError is not null)
        {
            errors["organisationName"] = [organisationNameError];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ToValidationErrors(
        IReadOnlyList<IdentityError> identityErrors)
    {
        var errors = new Dictionary<string, string[]>();
        var passwordErrors = identityErrors
            .Where(error => error.Code.StartsWith("Password", StringComparison.Ordinal))
            .Select(error => error.Description)
            .ToArray();
        if (passwordErrors.Length > 0)
        {
            errors["password"] = passwordErrors;
        }

        var hasEmailError = identityErrors.Any(
            error => !error.Code.StartsWith("Password", StringComparison.Ordinal));
        if (hasEmailError)
        {
            // Keep duplicate and other Identity account failures indistinguishable at the API boundary.
            errors["email"] =
                ["Registration could not be completed with the supplied email address."];
        }

        return errors;
    }

    public sealed record RegistrationRequest(
        string? Email,
        string? Password,
        string? OrganisationName);

    public sealed record RegistrationResponse(
        Guid UserId,
        string Email,
        Guid OrganisationId,
        string OrganisationName);
}
