namespace EngageOps.Api.Assignments;

public abstract record AssignmentCancellationResult
{
    private AssignmentCancellationResult()
    {
    }

    public sealed record Cancelled : AssignmentCancellationResult;

    public sealed record AlreadyCancelled : AssignmentCancellationResult;

    public sealed record OrganisationNotFound : AssignmentCancellationResult;

    public sealed record AssignmentNotFound : AssignmentCancellationResult;
}
