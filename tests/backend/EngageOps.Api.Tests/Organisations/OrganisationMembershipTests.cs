using EngageOps.Api.Organisations;

namespace EngageOps.Api.Tests.Organisations;

public class OrganisationMembershipTests
{
    [Fact]
    public void CreateSetsOrganisationAndUserIdentities()
    {
        var organisationId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var membership = OrganisationMembership.Create(organisationId, userId);

        Assert.Equal(organisationId, membership.OrganisationId);
        Assert.Equal(userId, membership.UserId);
    }

    [Fact]
    public void CreateRejectsEmptyOrganisationIdentity()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            OrganisationMembership.Create(Guid.Empty, Guid.CreateVersion7()));

        Assert.Equal("organisationId", exception.ParamName);
    }

    [Fact]
    public void CreateRejectsEmptyUserIdentity()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            OrganisationMembership.Create(Guid.CreateVersion7(), Guid.Empty));

        Assert.Equal("userId", exception.ParamName);
    }
}
