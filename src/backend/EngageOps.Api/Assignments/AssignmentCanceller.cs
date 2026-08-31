using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EngageOps.Api.Assignments;

public sealed class AssignmentCanceller(
    EngageOpsDbContext context,
    OrganisationMembershipChecker membershipChecker)
{
    public async Task<AssignmentCancellationResult> CancelAsync(
        Guid userId,
        Guid organisationId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        // Missing and inaccessible organisations deliberately share one result to prevent tenant probing.
        if (!await membershipChecker.IsMemberAsync(userId, organisationId, cancellationToken))
        {
            return new AssignmentCancellationResult.OrganisationNotFound();
        }

        var assignment = await context.Assignments.SingleOrDefaultAsync(
            candidate =>
                candidate.Id == assignmentId && candidate.OrganisationId == organisationId,
            cancellationToken);
        if (assignment is null)
        {
            return new AssignmentCancellationResult.AssignmentNotFound();
        }

        if (!assignment.TryCancel())
        {
            return new AssignmentCancellationResult.AlreadyCancelled();
        }

        await context.SaveChangesAsync(cancellationToken);

        return new AssignmentCancellationResult.Cancelled();
    }
}
