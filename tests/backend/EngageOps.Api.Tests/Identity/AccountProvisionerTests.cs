using EngageOps.Api.Identity;
using EngageOps.Api.Persistence;
using EngageOps.Api.Tests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EngageOps.Api.Tests.Identity;

public class AccountProvisionerTests
{
    [Fact]
    public async Task ProvisionAsyncCreatesAccountOrganisationAndInitialMembership()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());

        Guid userId;
        Guid organisationId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            var provisioner = scope.ServiceProvider.GetRequiredService<AccountProvisioner>();

            var result = await provisioner.ProvisionAsync(
                "  owner@northstar.example  ",
                IdentityTestData.ValidPassword,
                "  Northstar Workforce  ",
                cancellationToken);

            var created = Assert.IsType<AccountProvisioningResult.Created>(result);
            userId = created.User.Id;
            organisationId = created.Organisation.Id;
            Assert.Equal("Northstar Workforce", created.Organisation.Name);
        }

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EngageOpsDbContext>();
        var userManager = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        var organisation = await verificationContext.Organisations
            .AsNoTracking()
            .SingleAsync(cancellationToken);
        var membership = await verificationContext.OrganisationMemberships
            .AsNoTracking()
            .SingleAsync(cancellationToken);

        Assert.NotNull(user);
        Assert.Equal("owner@northstar.example", user.Email);
        Assert.True(await userManager.CheckPasswordAsync(user, IdentityTestData.ValidPassword));
        Assert.Equal(organisationId, organisation.Id);
        Assert.Equal((organisationId, userId), (membership.OrganisationId, membership.UserId));
    }

    [Fact]
    public async Task ProvisionAsyncRejectsDuplicateAccountWithoutCreatingAnotherOrganisation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            var provisioner = scope.ServiceProvider.GetRequiredService<AccountProvisioner>();

            var firstResult = await provisioner.ProvisionAsync(
                "owner@northstar.example",
                IdentityTestData.ValidPassword,
                "Northstar Workforce",
                cancellationToken);
            var duplicateResult = await provisioner.ProvisionAsync(
                "OWNER@NORTHSTAR.EXAMPLE",
                IdentityTestData.ValidPassword,
                "Duplicate Organisation",
                cancellationToken);

            Assert.IsType<AccountProvisioningResult.Created>(firstResult);
            var rejected = Assert.IsType<AccountProvisioningResult.Rejected>(duplicateResult);
            Assert.Contains(
                rejected.Errors,
                error => error.Code is "DuplicateUserName" or "DuplicateEmail");
        }

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EngageOpsDbContext>();

        Assert.Equal(1, await verificationContext.Users.CountAsync(cancellationToken));
        Assert.Equal(1, await verificationContext.Organisations.CountAsync(cancellationToken));
        Assert.Equal(
            1,
            await verificationContext.OrganisationMemberships.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task ProvisionAsyncReturnsIdentityErrorsWithoutCreatingTenantData()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            var provisioner = scope.ServiceProvider.GetRequiredService<AccountProvisioner>();

            var result = await provisioner.ProvisionAsync(
                "owner@northstar.example",
                "password",
                "Northstar Workforce",
                cancellationToken);

            var rejected = Assert.IsType<AccountProvisioningResult.Rejected>(result);
            Assert.Contains(rejected.Errors, error => error.Code == "PasswordRequiresDigit");
            Assert.Contains(rejected.Errors, error => error.Code == "PasswordRequiresUpper");
            Assert.Contains(rejected.Errors, error => error.Code == "PasswordRequiresNonAlphanumeric");
        }

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EngageOpsDbContext>();

        Assert.Empty(await verificationContext.Users.ToListAsync(cancellationToken));
        Assert.Empty(await verificationContext.Organisations.ToListAsync(cancellationToken));
        Assert.Empty(await verificationContext.OrganisationMemberships.ToListAsync(cancellationToken));
    }

    [Fact]
    public async Task ProvisionAsyncRollsBackAccountWhenOrganisationProvisioningFails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var postgreSql = PostgreSqlTestDatabase.CreateContainer();
        await postgreSql.StartAsync(cancellationToken);

        using var factory = new EngageOpsApiFactory(postgreSql.GetConnectionString());
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EngageOpsDbContext>();
            await context.Database.MigrateAsync(cancellationToken);
            var provisioner = scope.ServiceProvider.GetRequiredService<AccountProvisioner>();

            var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
                provisioner.ProvisionAsync(
                    "owner@northstar.example",
                    IdentityTestData.ValidPassword,
                    " ",
                    cancellationToken));

            Assert.Equal("name", exception.ParamName);
        }

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<EngageOpsDbContext>();

        Assert.Empty(await verificationContext.Users.ToListAsync(cancellationToken));
        Assert.Empty(await verificationContext.Organisations.ToListAsync(cancellationToken));
        Assert.Empty(await verificationContext.OrganisationMemberships.ToListAsync(cancellationToken));
    }
}
