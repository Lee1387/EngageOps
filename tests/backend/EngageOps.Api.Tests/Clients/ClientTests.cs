using EngageOps.Api.Clients;

namespace EngageOps.Api.Tests.Clients;

public class ClientTests
{
    [Fact]
    public void CreateSetsVersionSevenIdentityOrganisationAndName()
    {
        var organisationId = Guid.CreateVersion7();

        var client = Client.Create(organisationId, "Northstar Logistics");

        Assert.Equal(7, client.Id.Version);
        Assert.Equal(organisationId, client.OrganisationId);
        Assert.Equal("Northstar Logistics", client.Name);
    }

    [Fact]
    public void CreateRejectsEmptyOrganisationIdentity()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Client.Create(Guid.Empty, "Northstar Logistics"));

        Assert.Equal("organisationId", exception.ParamName);
    }

    [Fact]
    public void CreateTrimsSurroundingWhitespaceFromName()
    {
        var client = Client.Create(Guid.CreateVersion7(), "  Northstar Logistics  ");

        Assert.Equal("Northstar Logistics", client.Name);
    }

    [Fact]
    public void CreateRejectsNullName()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Client.Create(Guid.CreateVersion7(), null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void CreateRejectsEmptyOrWhitespaceName(string name)
    {
        Assert.Throws<ArgumentException>(() =>
            Client.Create(Guid.CreateVersion7(), name));
    }

    [Theory]
    [InlineData("Northstar\0Logistics")]
    [InlineData("Northstar\nLogistics")]
    [InlineData("Northstar\tLogistics")]
    public void CreateRejectsNameContainingControlCharacters(string name)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Client.Create(Guid.CreateVersion7(), name));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void CreateAcceptsNameAtMaximumLength()
    {
        var name = new string('a', Client.MaxNameLength);

        var client = Client.Create(Guid.CreateVersion7(), name);

        Assert.Equal(name, client.Name);
    }

    [Fact]
    public void CreateRejectsNameOverMaximumLength()
    {
        var name = new string('a', Client.MaxNameLength + 1);

        var exception = Assert.Throws<ArgumentException>(() =>
            Client.Create(Guid.CreateVersion7(), name));

        Assert.Equal("name", exception.ParamName);
    }
}
