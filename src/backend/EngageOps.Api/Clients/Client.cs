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

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmedName = name.Trim();

        if (trimmedName.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Client name must not exceed {MaxNameLength} characters.",
                nameof(name));
        }

        return new Client(Guid.CreateVersion7(), organisationId, trimmedName);
    }
}
