using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;

namespace EngageOps.Api.Workers;

public sealed class WorkerCreator(
    EngageOpsDbContext context,
    OrganisationMembershipChecker membershipChecker)
{
    public async Task<Worker?> CreateAsync(
        Guid userId,
        Guid organisationId,
        string name,
        CancellationToken cancellationToken)
    {
        var worker = Worker.Create(organisationId, name);

        // Missing and inaccessible organisations deliberately share one result to prevent tenant probing.
        if (!await membershipChecker.IsMemberAsync(userId, organisationId, cancellationToken))
        {
            return null;
        }

        context.Workers.Add(worker);
        await context.SaveChangesAsync(cancellationToken);

        return worker;
    }
}
