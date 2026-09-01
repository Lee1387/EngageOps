namespace EngageOps.Api.DevelopmentData;

public sealed class DevelopmentDataOptions
{
    public const string SectionName = "DevelopmentData";

    public string Email { get; init; } = string.Empty;

    public string OrganisationName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
