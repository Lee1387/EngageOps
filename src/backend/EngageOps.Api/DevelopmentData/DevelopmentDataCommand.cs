namespace EngageOps.Api.DevelopmentData;

internal enum DevelopmentDataAction
{
    Seed,
    Reset,
}

internal static class DevelopmentDataCommand
{
    public static DevelopmentDataAction? Parse(string[] args)
    {
        if (args.Length == 0 || !string.Equals(
                args[0],
                "development-data",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (args.Length != 2)
        {
            throw CreateUsageException();
        }

        return args[1].ToLowerInvariant() switch
        {
            "seed" => DevelopmentDataAction.Seed,
            "reset" => DevelopmentDataAction.Reset,
            _ => throw CreateUsageException(),
        };
    }

    private static ArgumentException CreateUsageException() =>
        new("Use 'development-data seed' or 'development-data reset'.", "args");
}

internal static partial class DevelopmentDataLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Development data is ready for {Email} in {OrganisationName}. " +
            "Added {AddedClientCount} clients; {TotalClientCount} are available.")]
    public static partial void SeedReady(
        ILogger logger,
        string email,
        string organisationName,
        int addedClientCount,
        int totalClientCount);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Development data reset completed. Removed {OrganisationCount} organisations, " +
            "{ClientCount} clients, {WorkerCount} workers, and {AssignmentCount} assignments.")]
    public static partial void ResetCompleted(
        ILogger logger,
        int organisationCount,
        int clientCount,
        int workerCount,
        int assignmentCount);
}
