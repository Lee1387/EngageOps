namespace EngageOps.Api.Organisations;

public sealed class OrganisationMembership
{
    private OrganisationMembership(Guid organisationId, Guid userId)
    {
        OrganisationId = organisationId;
        UserId = userId;
    }

    public Guid OrganisationId { get; }

    public Guid UserId { get; }

    public static OrganisationMembership Create(Guid organisationId, Guid userId)
    {
        if (organisationId == Guid.Empty)
        {
            throw new ArgumentException("Organisation identifier cannot be empty.", nameof(organisationId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier cannot be empty.", nameof(userId));
        }

        return new OrganisationMembership(organisationId, userId);
    }
}
