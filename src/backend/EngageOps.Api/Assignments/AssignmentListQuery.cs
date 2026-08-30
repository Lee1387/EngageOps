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

        var pageQuery = assignmentQuery
            .OrderByDescending(assignment => assignment.StartDate)
            .ThenBy(assignment => assignment.Id)
            .Skip(offset)
            .Take(pageSize);
        var items = await AssignmentSummaryQuery.Project(context, pageQuery)
            .ToListAsync(cancellationToken);

        return new AssignmentListResult.Found(items, totalCount);
    }
}
