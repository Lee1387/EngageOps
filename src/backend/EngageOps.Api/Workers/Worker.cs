namespace EngageOps.Api.Workers;

public sealed class Worker
{
    public const int MaxNameLength = 200;

    private Worker(Guid id, Guid organisationId, string name)
    {
        Id = id;
        OrganisationId = organisationId;
        Name = name;
    }

    public Guid Id { get; }

    public Guid OrganisationId { get; }

    public string Name { get; }

    public static Worker Create(Guid organisationId, string name)
    {
        if (organisationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Organisation identifier cannot be empty.",
                nameof(organisationId));
        }

        ArgumentNullException.ThrowIfNull(name);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Worker name is required.", nameof(name));
        }

        if (name.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Worker name must not contain control characters.",
                nameof(name));
        }

        var trimmedName = name.Trim();

        if (trimmedName.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Worker name must not exceed {MaxNameLength} characters.",
                nameof(name));
        }

        return new Worker(Guid.CreateVersion7(), organisationId, trimmedName);
    }
}
