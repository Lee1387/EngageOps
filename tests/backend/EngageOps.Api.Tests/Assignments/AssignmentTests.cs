using EngageOps.Api.Assignments;

namespace EngageOps.Api.Tests.Assignments;

public class AssignmentTests
{
    [Fact]
    public void CreateSetsIdentityRelationshipsAndDates()
    {
        var organisationId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var workerId = Guid.CreateVersion7();
        var startDate = new DateOnly(2026, 9, 1);
        var endDate = new DateOnly(2027, 2, 28);

        var assignment = Assignment.Create(
            organisationId,
            clientId,
            workerId,
            startDate,
            endDate);

        Assert.Equal(7, assignment.Id.Version);
        Assert.Equal(organisationId, assignment.OrganisationId);
        Assert.Equal(clientId, assignment.ClientId);
        Assert.Equal(workerId, assignment.WorkerId);
        Assert.Equal(startDate, assignment.StartDate);
        Assert.Equal(endDate, assignment.EndDate);
        Assert.Equal(AssignmentStatus.Confirmed, assignment.Status);
    }

    [Fact]
    public void TryCancelTransitionsConfirmedAssignmentAndRejectsRepeatedCancellation()
    {
        var assignment = Assignment.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new DateOnly(2026, 9, 1));

        Assert.True(assignment.TryCancel());
        Assert.Equal(AssignmentStatus.Cancelled, assignment.Status);
        Assert.False(assignment.TryCancel());
        Assert.Equal(AssignmentStatus.Cancelled, assignment.Status);
    }

    [Fact]
    public void CreateAllowsOpenEndedAssignment()
    {
        var assignment = Assignment.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new DateOnly(2026, 9, 1));

        Assert.Null(assignment.EndDate);
    }

    [Fact]
    public void CreateAllowsEndDateEqualToStartDate()
    {
        var date = new DateOnly(2026, 9, 1);

        var assignment = Assignment.Create(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            date,
            date);

        Assert.Equal(date, assignment.EndDate);
    }

    [Fact]
    public void CreateRejectsEmptyOrganisationIdentity()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Assignment.Create(
                Guid.Empty,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                new DateOnly(2026, 9, 1)));

        Assert.Equal("organisationId", exception.ParamName);
    }

    [Fact]
    public void CreateRejectsEmptyClientIdentity()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Assignment.Create(
                Guid.CreateVersion7(),
                Guid.Empty,
                Guid.CreateVersion7(),
                new DateOnly(2026, 9, 1)));

        Assert.Equal("clientId", exception.ParamName);
    }

    [Fact]
    public void CreateRejectsEmptyWorkerIdentity()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Assignment.Create(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                Guid.Empty,
                new DateOnly(2026, 9, 1)));

        Assert.Equal("workerId", exception.ParamName);
    }

    [Fact]
    public void CreateRejectsEndDateBeforeStartDate()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Assignment.Create(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                new DateOnly(2026, 9, 1),
                new DateOnly(2026, 8, 31)));

        Assert.Equal("endDate", exception.ParamName);
    }
}
