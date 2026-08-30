using EngageOps.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EngageOps.Api.Organisations;

public sealed class OrganisationProvisioner(EngageOpsDbContext context)
{
    public async Task<Organisation?> ProvisionAsync(
        Guid userId,
        string name,
        CancellationToken cancellationToken)
    {
        var organisation = Organisation.Create(name);
        var membership = OrganisationMembership.Create(organisation.Id, userId);

        if (!await context.Users.AnyAsync(user => user.Id == userId, cancellationToken))
        {
            return null;
        }

        context.AddRange(organisation, membership);
        await context.SaveChangesAsync(cancellationToken);

        return organisation;
    }
}
