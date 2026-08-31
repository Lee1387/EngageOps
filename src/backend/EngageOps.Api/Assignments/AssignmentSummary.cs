namespace EngageOps.Api.Assignments;

public sealed record AssignmentSummary(
    Guid Id,
    Guid OrganisationId,
    Guid ClientId,
    string ClientName,
    Guid WorkerId,
    string WorkerName,
    DateOnly StartDate,
    DateOnly? EndDate,
    AssignmentStatus Status);
