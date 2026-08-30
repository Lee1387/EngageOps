using EngageOps.Api.Workers;

namespace EngageOps.Api.Tests.Workers;

public class WorkerTests
{
    [Fact]
    public void CreateSetsVersionSevenIdentityOrganisationAndName()
    {
        var organisationId = Guid.CreateVersion7();

        var worker = Worker.Create(organisationId, "Alex Morgan");

        Assert.Equal(7, worker.Id.Version);
        Assert.Equal(organisationId, worker.OrganisationId);
        Assert.Equal("Alex Morgan", worker.Name);
    }

    [Fact]
    public void CreateRejectsEmptyOrganisationIdentity()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Worker.Create(Guid.Empty, "Alex Morgan"));

        Assert.Equal("organisationId", exception.ParamName);
    }

    [Fact]
    public void CreateTrimsSurroundingWhitespaceFromName()
    {
        var worker = Worker.Create(Guid.CreateVersion7(), "  Alex Morgan  ");

        Assert.Equal("Alex Morgan", worker.Name);
    }

    [Fact]
    public void CreatePreservesUnicodeAndPunctuationInName()
    {
        var worker = Worker.Create(Guid.CreateVersion7(), "José O'Connor-Smith");

        Assert.Equal("José O'Connor-Smith", worker.Name);
    }

    [Fact]
    public void CreateRejectsNullName()
    {
        Assert.Throws<ArgumentNullException>(() =>
            Worker.Create(Guid.CreateVersion7(), null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\r\n")]
    public void CreateRejectsEmptyOrWhitespaceName(string name)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Worker.Create(Guid.CreateVersion7(), name));

        Assert.Equal("name", exception.ParamName);
    }

    [Theory]
    [InlineData("Alex\0Morgan")]
    [InlineData("Alex\nMorgan")]
    [InlineData("Alex\tMorgan")]
    public void CreateRejectsNameContainingControlCharacters(string name)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Worker.Create(Guid.CreateVersion7(), name));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void CreateAcceptsNameAtMaximumLength()
    {
        var name = new string('a', Worker.MaxNameLength);

        var worker = Worker.Create(Guid.CreateVersion7(), name);

        Assert.Equal(name, worker.Name);
    }

    [Fact]
    public void CreateRejectsNameOverMaximumLength()
    {
        var name = new string('a', Worker.MaxNameLength + 1);

        var exception = Assert.Throws<ArgumentException>(() =>
            Worker.Create(Guid.CreateVersion7(), name));

        Assert.Equal("name", exception.ParamName);
    }
}
