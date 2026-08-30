using EngageOps.Api.Identity;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EngageOps.Api.Tests.Organisations;

public class OrganisationMembershipMappingTests
{
    [Fact]
    public void ModelMapsMembershipWithTenantRelationshipsAndUserLookupIndex()
    {
        var options = new DbContextOptionsBuilder<EngageOpsDbContext>()
            .UseNpgsql("Host=localhost;Database=engageops_model_tests;Username=unused;Password=unused")
            .Options;

        using var context = new EngageOpsDbContext(options);
        var entity = context.Model.FindEntityType(typeof(OrganisationMembership));
        var table = StoreObjectIdentifier.Table("organisation_memberships", schema: null);

        Assert.NotNull(entity);
        Assert.Equal("organisation_memberships", entity.GetTableName());

        var organisationId = entity.FindProperty(nameof(OrganisationMembership.OrganisationId))!;
        var userId = entity.FindProperty(nameof(OrganisationMembership.UserId))!;

        Assert.Equal("organisation_id", organisationId.GetColumnName(table));
        Assert.Equal("user_id", userId.GetColumnName(table));
        Assert.Equal(ValueGenerated.Never, organisationId.ValueGenerated);
        Assert.Equal(ValueGenerated.Never, userId.ValueGenerated);
        Assert.Equal(
            new[] { organisationId, userId },
            entity.FindPrimaryKey()!.Properties);

        var organisationForeignKey = entity.GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Organisation));
        var userForeignKey = entity.GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(ApplicationUser));

        Assert.Equal(DeleteBehavior.Cascade, organisationForeignKey.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, userForeignKey.DeleteBehavior);
        Assert.Contains(
            entity.GetIndexes(),
            index => index.Properties.Count == 1 && index.Properties[0] == userId);
    }
}
