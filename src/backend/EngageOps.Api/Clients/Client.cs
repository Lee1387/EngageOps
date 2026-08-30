namespace EngageOps.Api.Clients;

public sealed class Client
{
    public const int MaxNameLength = 200;

    private Client(Guid id, Guid organisationId, string name)
    {
        Id = id;
        OrganisationId = organisationId;
        Name = name;
    }

    public Guid Id { get; }

    public Guid OrganisationId { get; }

    public string Name { get; }

    public static Client Create(Guid organisationId, string name)
    {
        if (organisationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Organisation identifier cannot be empty.",
                nameof(organisationId));
        }

        ArgumentNullException.ThrowIfNull(name);

        var validationError = GetNameValidationError(name);
        if (validationError is not null)
        {
            throw new ArgumentException(validationError, nameof(name));
        }

        return new Client(Guid.CreateVersion7(), organisationId, name.Trim());
    }

    internal static string? GetNameValidationError(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Client name is required.";
        }

        if (name.Any(char.IsControl))
        {
            return "Client name must not contain control characters.";
        }

        return name.Trim().Length > MaxNameLength
            ? $"Client name must not exceed {MaxNameLength} characters."
            : null;
    }
}
