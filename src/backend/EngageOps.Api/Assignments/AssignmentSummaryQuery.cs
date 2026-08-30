using EngageOps.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EngageOps.Api.Assignments;

internal static class AssignmentSummaryQuery
{
    // EF cannot translate filtering or ordering through this positional record projection.
    public static IQueryable<AssignmentSummary> Project(
        EngageOpsDbContext context,
        IQueryable<Assignment> assignments) =>
        from assignment in assignments
        join client in context.Clients.AsNoTracking()
            on new { assignment.OrganisationId, Id = assignment.ClientId }
            equals new { client.OrganisationId, client.Id }
        join worker in context.Workers.AsNoTracking()
            on new { assignment.OrganisationId, Id = assignment.WorkerId }
            equals new { worker.OrganisationId, worker.Id }
        select new AssignmentSummary(
            assignment.Id,
            assignment.OrganisationId,
            assignment.ClientId,
            client.Name,
            assignment.WorkerId,
            worker.Name,
            assignment.StartDate,
            assignment.EndDate);
}
