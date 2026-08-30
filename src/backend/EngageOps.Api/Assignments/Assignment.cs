namespace EngageOps.Api.Assignments;

public sealed class Assignment
{
    private Assignment(
        Guid id,
        Guid organisationId,
        Guid clientId,
        Guid workerId,
        DateOnly startDate,
        DateOnly? endDate,
        AssignmentStatus status)
    {
        Id = id;
        OrganisationId = organisationId;
        ClientId = clientId;
        WorkerId = workerId;
        StartDate = startDate;
        EndDate = endDate;
        Status = status;
    }

    public Guid Id { get; }

    public Guid OrganisationId { get; }

    public Guid ClientId { get; }

    public Guid WorkerId { get; }

    public DateOnly StartDate { get; }

    public DateOnly? EndDate { get; }

    public AssignmentStatus Status { get; private set; }

    public static Assignment Create(
        Guid organisationId,
        Guid clientId,
        Guid workerId,
        DateOnly startDate,
        DateOnly? endDate = null)
    {
        if (organisationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Organisation identifier cannot be empty.",
                nameof(organisationId));
        }

        if (clientId == Guid.Empty)
        {
            throw new ArgumentException(
                "Client identifier cannot be empty.",
                nameof(clientId));
        }

        if (workerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Worker identifier cannot be empty.",
                nameof(workerId));
        }

        var dateValidationError = GetDateValidationError(startDate, endDate);
        if (dateValidationError is not null)
        {
            throw new ArgumentException(
                dateValidationError,
                nameof(endDate));
        }

        return new Assignment(
            Guid.CreateVersion7(),
            organisationId,
            clientId,
            workerId,
            startDate,
            endDate,
            AssignmentStatus.Confirmed);
    }

    public bool TryCancel()
    {
        if (Status != AssignmentStatus.Confirmed)
        {
            return false;
        }

        Status = AssignmentStatus.Cancelled;
        return true;
    }

    internal static string? GetDateValidationError(DateOnly startDate, DateOnly? endDate) =>
        endDate is not null && endDate < startDate
            ? "Assignment end date cannot be before its start date."
            : null;
}
