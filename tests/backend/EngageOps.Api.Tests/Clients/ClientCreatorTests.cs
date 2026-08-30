using EngageOps.Api.Clients;
using EngageOps.Api.Identity;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using EngageOps.Api.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EngageOps.Api.Tests.Clients;

public class ClientCreatorTests
{
    [Fact]
    public async Task CreateAsyncPersistsClientForOrganisationMember()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        var organisation = Organisation.Create("Northstar Workforce");
        var user = new ApplicationUser { UserName = "owner@northstar.example" };

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            context.AddRange(
                organisation,
                user,
                OrganisationMembership.Create(organisation.Id, user.Id));
            await context.SaveChangesAsync(cancellationToken);

            var creator = scope.ServiceProvider.GetRequiredService<ClientCreator>();
            var client = await creator.CreateAsync(
                user.Id,
                organisation.Id,
                "  Northstar Logistics  ",
                cancellationToken);

            Assert.NotNull(client);
            Assert.Equal("Northstar Logistics", client.Name);
        }

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EngageOpsDbContext>();
        var persistedClient = await verificationContext.Clients
            .AsNoTracking()
            .SingleAsync(cancellationToken);

        Assert.Equal(organisation.Id, persistedClient.OrganisationId);
        Assert.Equal("Northstar Logistics", persistedClient.Name);
    }

    [Fact]
    public async Task CreateAsyncReturnsNullAndCreatesNothingWithoutOrganisationMembership()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        var options = PostgreSqlTestDatabase.CreateContextOptions(postgreSql);
        await using var context = new EngageOpsDbContext(options);
        await context.Database.MigrateAsync(cancellationToken);

        var usersOrganisation = Organisation.Create("Northstar Workforce");
        var otherOrganisation = Organisation.Create("Summit Staffing");
        var user = new ApplicationUser { UserName = "owner@northstar.example" };
        var otherUser = new ApplicationUser { UserName = "owner@summit.example" };
        context.AddRange(
            usersOrganisation,
            otherOrganisation,
            user,
            otherUser,
            OrganisationMembership.Create(usersOrganisation.Id, user.Id),
            OrganisationMembership.Create(otherOrganisation.Id, otherUser.Id));
        await context.SaveChangesAsync(cancellationToken);

        var creator = new ClientCreator(context);
        var otherOrganisationsClient = await creator.CreateAsync(
            user.Id,
            otherOrganisation.Id,
            "Summit Distribution",
            cancellationToken);
        var missingOrganisationsClient = await creator.CreateAsync(
            user.Id,
            Guid.CreateVersion7(),
            "Unknown Client",
            cancellationToken);

        Assert.Null(otherOrganisationsClient);
        Assert.Null(missingOrganisationsClient);
        Assert.Empty(await context.Clients.ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task CreateAsyncUsesClientNameValidation()
    {
        var options = new DbContextOptionsBuilder<EngageOpsDbContext>()
            .UseNpgsql("Host=localhost;Database=engageops_model_tests;Username=unused;Password=unused")
            .Options;
        await using var context = new EngageOpsDbContext(options);
        var creator = new ClientCreator(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            creator.CreateAsync(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                " ",
                TestContext.Current.CancellationToken));

        Assert.Equal("name", exception.ParamName);
    }
}
