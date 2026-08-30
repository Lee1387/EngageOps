using EngageOps.Api.Organisations;

namespace EngageOps.Api.Tests.Organisations;

public class OrganisationTests
{
    [Fact]
    public void CreateSetsVersionSevenIdentityAndName()
    {
        var organisation = Organisation.Create("Northstar Workforce");

        Assert.Equal(7, organisation.Id.Version);
        Assert.Equal("Northstar Workforce", organisation.Name);
    }

    [Fact]
    public void CreateTrimsSurroundingWhitespaceFromName()
    {
        var organisation = Organisation.Create("  Northstar Workforce  ");

        Assert.Equal("Northstar Workforce", organisation.Name);
    }

    [Fact]
    public void CreateRejectsNullName()
    {
        Assert.Throws<ArgumentNullException>(() => Organisation.Create(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void CreateRejectsEmptyOrWhitespaceName(string name)
    {
        Assert.Throws<ArgumentException>(() => Organisation.Create(name));
    }

    [Fact]
    public void CreateAcceptsNameAtMaximumLength()
    {
        var name = new string('a', Organisation.MaxNameLength);

        var organisation = Organisation.Create(name);

        Assert.Equal(name, organisation.Name);
    }

    [Fact]
    public void CreateRejectsNameOverMaximumLength()
    {
        var name = new string('a', Organisation.MaxNameLength + 1);

        var exception = Assert.Throws<ArgumentException>(() => Organisation.Create(name));

        Assert.Equal("name", exception.ParamName);
    }
}
