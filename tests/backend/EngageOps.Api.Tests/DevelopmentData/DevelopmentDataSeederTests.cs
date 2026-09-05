using EngageOps.Api.Assignments;
using EngageOps.Api.Clients;
using EngageOps.Api.DevelopmentData;
using EngageOps.Api.Identity;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using EngageOps.Api.Tests.Persistence;
using EngageOps.Api.Workers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using static EngageOps.Api.Tests.Identity.IdentityTestData;

namespace EngageOps.Api.Tests.DevelopmentData;

public class DevelopmentDataSeederTests
{
    private const string Email = "demo@engageops.local";
    private const string OrganisationName = "Northstar Demo Workforce";
    private const string Password = "LocalDevelopment1!";

    [Fact]
    public async Task SeedAndResetAreIdempotentAndScopedToTheDevelopmentDataset()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        Guid userId;
        Guid organisationId;
        Guid[] demoOrganisationIds;
        Guid otherOrganisationId;

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await database.Database.MigrateAsync(cancellationToken);
            var seeder = CreateSeeder(scope.ServiceProvider);

            var firstSeed = await seeder.SeedAsync(cancellationToken);
            var secondSeed = await seeder.SeedAsync(cancellationToken);

            Assert.Collection(firstSeed.Organisations,
                organisation =>
                {
                    Assert.Equal(OrganisationName, organisation.Name);
                    Assert.Equal(45, organisation.AddedClientCount);
                    Assert.Equal(45, organisation.TotalClientCount);
                },
                organisation =>
                {
                    Assert.Equal("Cedar Demo Workforce", organisation.Name);
                    Assert.Equal(3, organisation.AddedClientCount);
                    Assert.Equal(3, organisation.TotalClientCount);
                },
                organisation =>
                {
                    Assert.Equal("Newhaven Demo Workforce", organisation.Name);
                    Assert.Equal(0, organisation.AddedClientCount);
                    Assert.Equal(0, organisation.TotalClientCount);
                });
            Assert.All(secondSeed.Organisations, organisation => Assert.Equal(0, organisation.AddedClientCount));
            Assert.Equal(
                firstSeed.Organisations.Select(organisation => organisation.TotalClientCount),
                secondSeed.Organisations.Select(organisation => organisation.TotalClientCount));
            Assert.Equal(firstSeed.UserId, secondSeed.UserId);
            demoOrganisationIds = firstSeed.Organisations.Select(organisation => organisation.OrganisationId).ToArray();
            Assert.Equal(demoOrganisationIds, secondSeed.Organisations.Select(organisation => organisation.OrganisationId));
            userId = firstSeed.UserId;
            organisationId = firstSeed.Organisations[0].OrganisationId;

            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(Email);
            Assert.NotNull(user);
            Assert.True(await userManager.CheckPasswordAsync(user, Password));
            Assert.Equal(1, await database.Users.CountAsync(cancellationToken));
            Assert.Equal(3, await database.OrganisationMemberships.CountAsync(
                membership => membership.UserId == userId, cancellationToken));
            foreach (var organisation in firstSeed.Organisations)
            {
                Assert.Equal(organisation.TotalClientCount, await database.Clients.CountAsync(
                    client => client.OrganisationId == organisation.OrganisationId, cancellationToken));
            }

            var client = await database.Clients
                .FirstAsync(candidate => candidate.OrganisationId == organisationId,
                    cancellationToken);
            var worker = Worker.Create(organisationId, "Alex Morgan");
            var assignment = Assignment.Create(
                organisationId,
                client.Id,
                worker.Id,
                new DateOnly(2026, 9, 1));
            var otherOrganisation = Organisation.Create("Independent Workspace");
            var otherMembership = OrganisationMembership.Create(
                otherOrganisation.Id,
                userId);
            otherOrganisationId = otherOrganisation.Id;
            database.AddRange(worker, assignment, otherOrganisation, otherMembership);
            await database.SaveChangesAsync(cancellationToken);

            using var resetScope = factory.Services.CreateScope();
            var reset = await CreateSeeder(resetScope.ServiceProvider)
                .ResetAsync(cancellationToken);

            Assert.Equal(3, reset.OrganisationCount);
            Assert.Equal(48, reset.ClientCount);
            Assert.Equal(1, reset.WorkerCount);
            Assert.Equal(1, reset.AssignmentCount);
        }

        using (var verificationScope = factory.Services.CreateScope())
        {
            var database = verificationScope.ServiceProvider
                .GetRequiredService<EngageOpsDbContext>();
            Assert.False(await database.Organisations.AnyAsync(
                organisation => demoOrganisationIds.Contains(organisation.Id),
                cancellationToken));
            Assert.False(await database.Clients.AnyAsync(
                client => demoOrganisationIds.Contains(client.OrganisationId),
                cancellationToken));
            Assert.False(await database.Workers.AnyAsync(
                worker => demoOrganisationIds.Contains(worker.OrganisationId),
                cancellationToken));
            Assert.False(await database.Assignments.AnyAsync(
                assignment => demoOrganisationIds.Contains(assignment.OrganisationId),
                cancellationToken));
            Assert.True(await database.Users.AnyAsync(
                user => user.Id == userId,
                cancellationToken));
            Assert.True(await database.Organisations.AnyAsync(
                organisation => organisation.Id == otherOrganisationId,
                cancellationToken));

            var otherMembership = await database.OrganisationMemberships.SingleAsync(
                membership => membership.OrganisationId == otherOrganisationId,
                cancellationToken);
            database.OrganisationMemberships.Remove(otherMembership);
            database.Organisations.Remove(await database.Organisations.SingleAsync(
                organisation => organisation.Id == otherOrganisationId,
                cancellationToken));
            await database.SaveChangesAsync(cancellationToken);

            var reset = await CreateSeeder(verificationScope.ServiceProvider)
                .ResetAsync(cancellationToken);
            Assert.Equal(DevelopmentDataResetResult.Empty, reset);
        }

        using var finalScope = factory.Services.CreateScope();
        var finalDatabase = finalScope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
        Assert.False(await finalDatabase.Users.AnyAsync(
            user => user.Id == userId,
            cancellationToken));
        Assert.Equal(
            DevelopmentDataResetResult.Empty,
            await CreateSeeder(finalScope.ServiceProvider).ResetAsync(cancellationToken));
    }

    [Fact]
    public async Task SeedPreservesExistingDataAndResetLeavesOtherAccountsWithMatchingOrganisationNamesAlone()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);
        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        Guid userId;
        Guid otherUserId;
        Guid existingOrganisationId;
        Guid otherOrganisationId;
        Guid otherClientId;

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await database.Database.MigrateAsync(cancellationToken);
            var user = await CreateUserAsync(scope.ServiceProvider, Email);
            var otherUser = await CreateUserAsync(scope.ServiceProvider, "independent@example.test");
            userId = user.Id;
            otherUserId = otherUser.Id;
            var provisioner = scope.ServiceProvider.GetRequiredService<OrganisationProvisioner>();
            var existingOrganisation = await provisioner.ProvisionAsync(user.Id, OrganisationName, cancellationToken);
            var otherOrganisation = await provisioner.ProvisionAsync(otherUser.Id, OrganisationName, cancellationToken);
            Assert.NotNull(existingOrganisation);
            Assert.NotNull(otherOrganisation);
            existingOrganisationId = existingOrganisation.Id;
            otherOrganisationId = otherOrganisation.Id;
            var existingClient = Client.Create(existingOrganisationId, "ALDERBROOK ADVISORY");
            var manualClient = Client.Create(existingOrganisationId, "Manually added client");
            var otherClient = Client.Create(otherOrganisationId, "Independent client");
            otherClientId = otherClient.Id;
            database.AddRange(existingClient, manualClient, otherClient);
            await database.SaveChangesAsync(cancellationToken);

            var seed = await CreateSeeder(scope.ServiceProvider).SeedAsync(cancellationToken);
            var largeOrganisation = Assert.Single(seed.Organisations, organisation => organisation.Name == OrganisationName);
            Assert.Equal(userId, seed.UserId);
            Assert.Equal(existingOrganisationId, largeOrganisation.OrganisationId);
            Assert.Equal(44, largeOrganisation.AddedClientCount);
            Assert.Equal(46, largeOrganisation.TotalClientCount);
            Assert.True(await database.Clients.AnyAsync(client => client.Id == existingClient.Id, cancellationToken));
            Assert.True(await database.Clients.AnyAsync(client => client.Id == manualClient.Id, cancellationToken));
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True(await userManager.CheckPasswordAsync(user, ValidPassword));
            Assert.False(await userManager.CheckPasswordAsync(user, Password));
        }

        using (var resetScope = factory.Services.CreateScope())
        {
            var reset = await CreateSeeder(resetScope.ServiceProvider).ResetAsync(cancellationToken);
            Assert.Equal(3, reset.OrganisationCount);
            Assert.Equal(49, reset.ClientCount);
        }

        using var verificationScope = factory.Services.CreateScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
        Assert.False(await verificationDatabase.Users.AnyAsync(user => user.Id == userId, cancellationToken));
        Assert.Equal(otherUserId, (await verificationDatabase.Users.SingleAsync(cancellationToken)).Id);
        Assert.Equal(otherOrganisationId, (await verificationDatabase.Organisations.SingleAsync(cancellationToken)).Id);
        Assert.Equal(otherClientId, (await verificationDatabase.Clients.SingleAsync(cancellationToken)).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResetRejectsAmbiguousOrSharedOrganisationsBeforeDeletingAnything(bool sharedOrganisation)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);
        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await database.Database.MigrateAsync(cancellationToken);
            var seed = await CreateSeeder(scope.ServiceProvider).SeedAsync(cancellationToken);
            if (sharedOrganisation)
            {
                var otherUser = await CreateUserAsync(scope.ServiceProvider, "member@example.test");
                database.OrganisationMemberships.Add(OrganisationMembership.Create(
                    seed.Organisations[0].OrganisationId, otherUser.Id));
            }
            else
            {
                var duplicate = Organisation.Create(OrganisationName);
                database.AddRange(duplicate, OrganisationMembership.Create(duplicate.Id, seed.UserId));
            }

            await database.SaveChangesAsync(cancellationToken);
        }

        using (var operationScope = factory.Services.CreateScope())
        {
            var seeder = CreateSeeder(operationScope.ServiceProvider);
            await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.ResetAsync(cancellationToken));
            if (!sharedOrganisation)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync(cancellationToken));
            }
        }

        using var verificationScope = factory.Services.CreateScope();
        var verificationDatabase = verificationScope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
        Assert.Equal(48, await verificationDatabase.Clients.CountAsync(cancellationToken));
        Assert.Equal(sharedOrganisation ? 3 : 4, await verificationDatabase.Organisations.CountAsync(cancellationToken));
        Assert.Equal(sharedOrganisation ? 2 : 1, await verificationDatabase.Users.CountAsync(cancellationToken));
        Assert.Equal(4, await verificationDatabase.OrganisationMemberships.CountAsync(cancellationToken));
    }

    private static DevelopmentDataSeeder CreateSeeder(IServiceProvider services) =>
        new(
            services.GetRequiredService<EngageOpsDbContext>(),
            services.GetRequiredService<UserManager<ApplicationUser>>(),
            services.GetRequiredService<AccountProvisioner>(),
            services.GetRequiredService<OrganisationProvisioner>(),
            Options.Create(new DevelopmentDataOptions
            {
                Email = Email,
                Password = Password,
            }));
}
