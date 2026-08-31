namespace EngageOps.Api.Organisations;

public sealed class Organisation
{
    public const int MaxNameLength = 200;

    private Organisation(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; }

    public string Name { get; }

    public static Organisation Create(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var validationError = GetNameValidationError(name);
        if (validationError is not null)
        {
            throw new ArgumentException(validationError, nameof(name));
        }

        return new Organisation(Guid.CreateVersion7(), name.Trim());
    }

    internal static string? GetNameValidationError(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Organisation name is required.";
        }

        if (name.Any(char.IsControl))
        {
            return "Organisation name must not contain control characters.";
        }

        return name.Trim().Length > MaxNameLength
            ? $"Organisation name must not exceed {MaxNameLength} characters."
            : null;
    }
}
