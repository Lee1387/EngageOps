namespace EngageOps.Api.Assignments;

public abstract record AssignmentListResult
{
    private AssignmentListResult()
    {
    }

    public sealed record Found(
        IReadOnlyList<AssignmentListItem> Items,
        int TotalCount) : AssignmentListResult;

    public sealed record OrganisationNotFound : AssignmentListResult;
}

public sealed record AssignmentListItem(
    Guid Id,
    Guid OrganisationId,
    Guid ClientId,
    string ClientName,
    Guid WorkerId,
    string WorkerName,
    DateOnly StartDate,
    DateOnly? EndDate);
