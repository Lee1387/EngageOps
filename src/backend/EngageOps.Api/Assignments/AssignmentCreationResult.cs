namespace EngageOps.Api.Assignments;

public abstract record AssignmentCreationResult
{
    private AssignmentCreationResult()
    {
    }

    public sealed record Created(Assignment Assignment) : AssignmentCreationResult;

    public sealed record OrganisationNotFound : AssignmentCreationResult;

    public sealed record ClientNotFound : AssignmentCreationResult;

    public sealed record WorkerNotFound : AssignmentCreationResult;
}
