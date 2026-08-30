using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EngageOps.Api.Assignments;

public sealed class AssignmentCreator(
    EngageOpsDbContext context,
    OrganisationMembershipChecker membershipChecker)
{
    public async Task<AssignmentCreationResult> CreateAsync(
        Guid userId,
        Guid organisationId,
        Guid clientId,
        Guid workerId,
        DateOnly startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken)
    {
        var assignment = Assignment.Create(
            organisationId,
            clientId,
            workerId,
            startDate,
            endDate);

        // Missing and inaccessible organisations deliberately share one result to prevent tenant probing.
        if (!await membershipChecker.IsMemberAsync(userId, organisationId, cancellationToken))
        {
            return new AssignmentCreationResult.OrganisationNotFound();
        }

        if (!await context.Clients.AnyAsync(
                client => client.Id == clientId && client.OrganisationId == organisationId,
                cancellationToken))
        {
            return new AssignmentCreationResult.ClientNotFound();
        }

        if (!await context.Workers.AnyAsync(
                worker => worker.Id == workerId && worker.OrganisationId == organisationId,
                cancellationToken))
        {
            return new AssignmentCreationResult.WorkerNotFound();
        }

        context.Assignments.Add(assignment);
        await context.SaveChangesAsync(cancellationToken);

        return new AssignmentCreationResult.Created(assignment);
    }
}
