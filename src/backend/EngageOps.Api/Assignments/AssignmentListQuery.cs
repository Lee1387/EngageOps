using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EngageOps.Api.Assignments;

public sealed class AssignmentListQuery(
    EngageOpsDbContext context,
    OrganisationMembershipChecker membershipChecker)
{
    public async Task<AssignmentListResult> ExecuteAsync(
        Guid userId,
        Guid organisationId,
        int offset,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        if (!await membershipChecker.IsMemberAsync(userId, organisationId, cancellationToken))
        {
            return new AssignmentListResult.OrganisationNotFound();
        }

        var assignmentQuery = context.Assignments
            .AsNoTracking()
            .Where(assignment => assignment.OrganisationId == organisationId);
        var totalCount = await assignmentQuery.CountAsync(cancellationToken);
        if (offset >= totalCount)
        {
            return new AssignmentListResult.Found([], totalCount);
        }

        var items = await (
                from assignment in assignmentQuery
                join client in context.Clients.AsNoTracking()
                    on new { assignment.OrganisationId, Id = assignment.ClientId }
                    equals new { client.OrganisationId, client.Id }
                join worker in context.Workers.AsNoTracking()
                    on new { assignment.OrganisationId, Id = assignment.WorkerId }
                    equals new { worker.OrganisationId, worker.Id }
                orderby assignment.StartDate descending, assignment.Id
                select new AssignmentListItem(
                    assignment.Id,
                    assignment.OrganisationId,
                    assignment.ClientId,
                    client.Name,
                    assignment.WorkerId,
                    worker.Name,
                    assignment.StartDate,
                    assignment.EndDate))
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new AssignmentListResult.Found(items, totalCount);
    }
}
