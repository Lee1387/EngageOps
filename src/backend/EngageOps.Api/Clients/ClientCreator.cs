using EngageOps.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EngageOps.Api.Clients;

public sealed class ClientCreator(EngageOpsDbContext context)
{
    public async Task<Client?> CreateAsync(
        Guid userId,
        Guid organisationId,
        string name,
        CancellationToken cancellationToken)
    {
        var client = Client.Create(organisationId, name);

        var isMember = await context.OrganisationMemberships.AnyAsync(
            membership =>
                membership.OrganisationId == organisationId && membership.UserId == userId,
            cancellationToken);

        // Missing and inaccessible organisations deliberately share one result to prevent tenant probing.
        if (!isMember)
        {
            return null;
        }

        context.Clients.Add(client);
        await context.SaveChangesAsync(cancellationToken);

        return client;
    }
}
