namespace EngageOps.Api.Assignments;

public abstract record AssignmentDetailResult
{
    private AssignmentDetailResult()
    {
    }

    public sealed record Found(AssignmentSummary Assignment) : AssignmentDetailResult;

    public sealed record OrganisationNotFound : AssignmentDetailResult;

    public sealed record AssignmentNotFound : AssignmentDetailResult;
}
