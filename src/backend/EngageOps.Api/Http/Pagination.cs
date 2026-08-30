namespace EngageOps.Api.Http;

internal static class Pagination
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;

    public static Dictionary<string, string[]> Validate(
        int page,
        int pageSize,
        out int offset)
    {
        var errors = new Dictionary<string, string[]>();
        offset = 0;

        if (page < 1)
        {
            errors["page"] = ["Page must be at least 1."];
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            errors["pageSize"] = [$"Page size must be between 1 and {MaxPageSize}."];
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var requestedOffset = (long)(page - 1) * pageSize;
        if (requestedOffset > int.MaxValue)
        {
            errors["page"] = ["Page is too large."];
            return errors;
        }

        offset = (int)requestedOffset;
        return errors;
    }
}
