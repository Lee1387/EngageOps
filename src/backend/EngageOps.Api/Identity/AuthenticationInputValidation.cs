using System.ComponentModel.DataAnnotations;

namespace EngageOps.Api.Identity;

internal static class AuthenticationInputValidation
{
    private const int MaxEmailLength = 256;
    private const int MaxPasswordLength = 256;

    private static readonly EmailAddressAttribute EmailAddressValidator = new();

    public static string? GetEmailValidationError(string? email)
    {
        var trimmedEmail = email?.Trim();

        if (string.IsNullOrEmpty(trimmedEmail))
        {
            return "Email is required.";
        }

        if (trimmedEmail.Length > MaxEmailLength)
        {
            return $"Email must not exceed {MaxEmailLength} characters.";
        }

        return trimmedEmail.Any(char.IsControl) || !EmailAddressValidator.IsValid(trimmedEmail)
            ? "Email must be a valid email address."
            : null;
    }

    public static string? GetPasswordBoundaryValidationError(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return "Password is required.";
        }

        return password.Length > MaxPasswordLength
            ? $"Password must not exceed {MaxPasswordLength} characters."
            : null;
    }
}
