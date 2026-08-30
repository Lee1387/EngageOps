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

        var validationError = GetNameValidationError(name);
        if (validationError is not null)
        {
            throw new ArgumentException(validationError, nameof(name));
        }

        return new Worker(Guid.CreateVersion7(), organisationId, name.Trim());
    }

    internal static string? GetNameValidationError(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Worker name is required.";
        }

        if (name.Any(char.IsControl))
        {
            return "Worker name must not contain control characters.";
        }

        return name.Trim().Length > MaxNameLength
            ? $"Worker name must not exceed {MaxNameLength} characters."
            : null;
    }
}
