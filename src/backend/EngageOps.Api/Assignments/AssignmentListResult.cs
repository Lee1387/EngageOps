namespace EngageOps.Api.Assignments;

public abstract record AssignmentListResult
{
    private AssignmentListResult()
    {
    }

    public sealed record Found(
        IReadOnlyList<AssignmentSummary> Items,
        int TotalCount) : AssignmentListResult;

    public sealed record OrganisationNotFound : AssignmentListResult;
}
