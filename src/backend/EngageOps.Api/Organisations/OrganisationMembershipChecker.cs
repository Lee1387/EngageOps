using EngageOps.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EngageOps.Api.Organisations;

public sealed class OrganisationMembershipChecker(EngageOpsDbContext context)
{
    public Task<bool> IsMemberAsync(
        Guid userId,
        Guid organisationId,
        CancellationToken cancellationToken) =>
        context.OrganisationMemberships.AnyAsync(
            membership =>
                membership.OrganisationId == organisationId && membership.UserId == userId,
            cancellationToken);
}
