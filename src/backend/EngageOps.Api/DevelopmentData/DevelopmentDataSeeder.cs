using EngageOps.Api.Assignments;
using EngageOps.Api.Clients;
using EngageOps.Api.Identity;
using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using EngageOps.Api.Workers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EngageOps.Api.DevelopmentData;

public sealed class DevelopmentDataSeeder(
    EngageOpsDbContext context,
    UserManager<ApplicationUser> userManager,
    AccountProvisioner accountProvisioner,
    OrganisationProvisioner organisationProvisioner,
    IOptions<DevelopmentDataOptions> options)
{
    private readonly DevelopmentDataOptions settings = options.Value;

    public async Task<DevelopmentDataSeedResult> SeedAsync(
        CancellationToken cancellationToken)
    {
        ValidateSettings();

        var user = await userManager.FindByEmailAsync(settings.Email);
        if (user is null)
        {
            var result = await accountProvisioner.ProvisionAsync(
                settings.Email,
                settings.Password,
                DevelopmentDataCatalog.Organisations[0].Name,
                cancellationToken);
            if (result is not AccountProvisioningResult.Created created)
            {
                var rejected = (AccountProvisioningResult.Rejected)result;
                throw new InvalidOperationException(
                    $"The development account could not be created: " +
                    $"{string.Join(", ", rejected.Errors.Select(error => error.Description))}");
            }

            user = created.User;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken);
        var organisations = await GetDemoOrganisationsAsync(user.Id, cancellationToken);
        var results = new List<DevelopmentOrganisationSeedResult>();
        foreach (var data in DevelopmentDataCatalog.Organisations)
        {
            var organisation = organisations.SingleOrDefault(candidate => candidate.Name == data.Name)
                ?? await organisationProvisioner.ProvisionAsync(user.Id, data.Name, cancellationToken)
                ?? throw new InvalidOperationException("The development organisation could not be created.");
            var existingNames = await context.Clients
                .Where(client => client.OrganisationId == organisation.Id)
                .Select(client => client.Name)
                .ToListAsync(cancellationToken);
            var existingNameSet = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var clients = data.ClientNames
                .Where(name => !existingNameSet.Contains(name))
                .Select(name => Client.Create(organisation.Id, name))
                .ToArray();

            context.Clients.AddRange(clients);
            results.Add(new DevelopmentOrganisationSeedResult(
                organisation.Id,
                organisation.Name,
                clients.Length,
                existingNames.Count + clients.Length));
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new DevelopmentDataSeedResult(user.Id, settings.Email, results);
    }

    public async Task<DevelopmentDataResetResult> ResetAsync(
        CancellationToken cancellationToken)
    {
        ValidateSettings();

        var user = await userManager.FindByEmailAsync(settings.Email);
        if (user is null)
        {
            return DevelopmentDataResetResult.Empty;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken);
        var organisations = await GetDemoOrganisationsAsync(user.Id, cancellationToken);
        var organisationIds = organisations.Select(organisation => organisation.Id).ToArray();

        if (await context.OrganisationMemberships.AnyAsync(
                membership => organisationIds.Contains(membership.OrganisationId) &&
                    membership.UserId != user.Id,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "A development organisation has other members; reset was stopped.");
        }

        var assignmentCount = await context.Assignments
            .Where(assignment => organisationIds.Contains(assignment.OrganisationId))
            .ExecuteDeleteAsync(cancellationToken);
        var clientCount = await context.Clients
            .Where(client => organisationIds.Contains(client.OrganisationId))
            .ExecuteDeleteAsync(cancellationToken);
        var workerCount = await context.Workers
            .Where(worker => organisationIds.Contains(worker.OrganisationId))
            .ExecuteDeleteAsync(cancellationToken);
        context.Organisations.RemoveRange(organisations);
        await context.SaveChangesAsync(cancellationToken);

        var userHasMemberships = await context.OrganisationMemberships
            .AnyAsync(membership => membership.UserId == user.Id, cancellationToken);
        if (!userHasMemberships)
        {
            var deletion = await userManager.DeleteAsync(user);
            if (!deletion.Succeeded)
            {
                throw new InvalidOperationException(
                    $"The development account could not be deleted: " +
                    $"{string.Join(", ", deletion.Errors.Select(error => error.Description))}");
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return new DevelopmentDataResetResult(
            organisations.Count,
            clientCount,
            workerCount,
            assignmentCount);
    }

    private async Task<List<Organisation>> GetDemoOrganisationsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var names = DevelopmentDataCatalog.Organisations.Select(data => data.Name).ToArray();
        var organisations = await (
                from membership in context.OrganisationMemberships
                join organisation in context.Organisations
                    on membership.OrganisationId equals organisation.Id
                where membership.UserId == userId &&
                    names.Contains(organisation.Name)
                select organisation)
            .ToListAsync(cancellationToken);

        if (organisations.GroupBy(organisation => organisation.Name).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException(
                "The development account has multiple organisations with the same demo name; the operation was stopped.");
        }

        return organisations;
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(settings.Email) ||
            string.IsNullOrWhiteSpace(settings.Password))
        {
            throw new InvalidOperationException(
                "Development data email and password are required.");
        }
    }
}

public sealed record DevelopmentDataSeedResult(
    Guid UserId,
    string Email,
    IReadOnlyList<DevelopmentOrganisationSeedResult> Organisations);

public sealed record DevelopmentOrganisationSeedResult(
    Guid OrganisationId,
    string Name,
    int AddedClientCount,
    int TotalClientCount);

public sealed record DevelopmentDataResetResult(
    int OrganisationCount,
    int ClientCount,
    int WorkerCount,
    int AssignmentCount)
{
    public static DevelopmentDataResetResult Empty { get; } = new(0, 0, 0, 0);
}
