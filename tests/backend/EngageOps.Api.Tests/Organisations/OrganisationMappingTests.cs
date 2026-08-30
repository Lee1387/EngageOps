using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EngageOps.Api.Tests.Organisations;

public class OrganisationMappingTests
{
    [Fact]
    public void ModelMapsOrganisationToExpectedTable()
    {
        var options = new DbContextOptionsBuilder<EngageOpsDbContext>()
            .UseNpgsql("Host=localhost;Database=engageops_model_tests;Username=unused;Password=unused")
            .Options;

        using var context = new EngageOpsDbContext(options);
        var entity = context.Model.FindEntityType(typeof(Organisation))!;
        var table = StoreObjectIdentifier.Table("organisations", schema: null);

        Assert.NotNull(entity);
        Assert.Equal("organisations", entity.GetTableName());

        var id = entity.FindProperty(nameof(Organisation.Id))!;
        Assert.NotNull(id);
        Assert.Equal("id", id.GetColumnName(table));
        Assert.Equal("uuid", id.GetColumnType());
        Assert.Equal(ValueGenerated.Never, id.ValueGenerated);
        Assert.Contains(id, entity.FindPrimaryKey()!.Properties);

        var name = entity.FindProperty(nameof(Organisation.Name))!;
        Assert.NotNull(name);
        Assert.Equal("name", name.GetColumnName(table));
        Assert.Equal("character varying(200)", name.GetColumnType());
        Assert.Equal(Organisation.MaxNameLength, name.GetMaxLength());
        Assert.False(name.IsNullable);
    }
}
