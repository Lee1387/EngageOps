using EngageOps.Api.Assignments;
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
        Guid otherOrganisationId;

        using (var scope = factory.Services.CreateScope())
        {
            var database = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await database.Database.MigrateAsync(cancellationToken);
            var seeder = CreateSeeder(scope.ServiceProvider);

            var firstSeed = await seeder.SeedAsync(cancellationToken);
            var secondSeed = await seeder.SeedAsync(cancellationToken);

            Assert.Equal(45, firstSeed.AddedClientCount);
            Assert.Equal(45, firstSeed.TotalClientCount);
            Assert.Equal(0, secondSeed.AddedClientCount);
            Assert.Equal(45, secondSeed.TotalClientCount);
            Assert.Equal(firstSeed.UserId, secondSeed.UserId);
            Assert.Equal(firstSeed.OrganisationId, secondSeed.OrganisationId);
            userId = firstSeed.UserId;
            organisationId = firstSeed.OrganisationId;

            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(Email);
            Assert.NotNull(user);
            Assert.True(await userManager.CheckPasswordAsync(user, Password));
            Assert.True(await database.OrganisationMemberships.AnyAsync(
                membership =>
                    membership.UserId == userId &&
                    membership.OrganisationId == organisationId,
                cancellationToken));
            Assert.Equal(
                45,
                await database.Clients.CountAsync(
                    client => client.OrganisationId == organisationId,
                    cancellationToken));

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

            Assert.Equal(1, reset.OrganisationCount);
            Assert.Equal(45, reset.ClientCount);
            Assert.Equal(1, reset.WorkerCount);
            Assert.Equal(1, reset.AssignmentCount);
        }

        using (var verificationScope = factory.Services.CreateScope())
        {
            var database = verificationScope.ServiceProvider
                .GetRequiredService<EngageOpsDbContext>();
            Assert.False(await database.Organisations.AnyAsync(
                organisation => organisation.Id == organisationId,
                cancellationToken));
            Assert.False(await database.Clients.AnyAsync(
                client => client.OrganisationId == organisationId,
                cancellationToken));
            Assert.False(await database.Workers.AnyAsync(
                worker => worker.OrganisationId == organisationId,
                cancellationToken));
            Assert.False(await database.Assignments.AnyAsync(
                assignment => assignment.OrganisationId == organisationId,
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

    private static DevelopmentDataSeeder CreateSeeder(IServiceProvider services) =>
        new(
            services.GetRequiredService<EngageOpsDbContext>(),
            services.GetRequiredService<UserManager<ApplicationUser>>(),
            services.GetRequiredService<AccountProvisioner>(),
            services.GetRequiredService<OrganisationProvisioner>(),
            Options.Create(new DevelopmentDataOptions
            {
                Email = Email,
                OrganisationName = OrganisationName,
                Password = Password,
            }));
}
