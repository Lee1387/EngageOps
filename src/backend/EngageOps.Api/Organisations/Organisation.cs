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
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmedName = name.Trim();

        if (trimmedName.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Organisation name must not exceed {MaxNameLength} characters.",
                nameof(name));
        }

        return new Organisation(Guid.CreateVersion7(), trimmedName);
    }
}
