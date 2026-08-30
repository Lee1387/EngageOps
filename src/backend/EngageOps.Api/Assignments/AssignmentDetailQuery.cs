using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EngageOps.Api.Assignments;

public sealed class AssignmentDetailQuery(
    EngageOpsDbContext context,
    OrganisationMembershipChecker membershipChecker)
{
    public async Task<AssignmentDetailResult> ExecuteAsync(
        Guid userId,
        Guid organisationId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        if (!await membershipChecker.IsMemberAsync(userId, organisationId, cancellationToken))
        {
            return new AssignmentDetailResult.OrganisationNotFound();
        }

        var assignmentQuery = context.Assignments
            .AsNoTracking()
            .Where(candidate =>
                candidate.OrganisationId == organisationId && candidate.Id == assignmentId);
        var assignment = await AssignmentSummaryQuery.Project(context, assignmentQuery)
            .SingleOrDefaultAsync(cancellationToken);

        return assignment is null
            ? new AssignmentDetailResult.AssignmentNotFound()
            : new AssignmentDetailResult.Found(assignment);
    }
}
