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
    private static readonly string[] ClientNames =
    [
        .. new[]
            {
                "Alderbrook",
                "Beacon",
                "Cedar",
                "Delta",
                "Elmbridge",
                "Frontier",
                "Granite",
                "Harbour",
                "Meridian",
            }
            .SelectMany(prefix => new[]
            {
                $"{prefix} Advisory",
                $"{prefix} Facilities",
                $"{prefix} Logistics",
                $"{prefix} Operations",
                $"{prefix} Services",
            }),
    ];

    private readonly DevelopmentDataOptions settings = options.Value;

    public async Task<DevelopmentDataSeedResult> SeedAsync(
        CancellationToken cancellationToken)
    {
        ValidateSettings();

        var user = await userManager.FindByEmailAsync(settings.Email);
        Organisation organisation;
        if (user is null)
        {
            var result = await accountProvisioner.ProvisionAsync(
                settings.Email,
                settings.Password,
                settings.OrganisationName,
                cancellationToken);
            if (result is not AccountProvisioningResult.Created created)
            {
                var rejected = (AccountProvisioningResult.Rejected)result;
                throw new InvalidOperationException(
                    $"The development account could not be created: " +
                    $"{string.Join(", ", rejected.Errors.Select(error => error.Description))}");
            }

            user = created.User;
            organisation = created.Organisation;
        }
        else
        {
            organisation = await GetOrCreateOrganisationAsync(user.Id, cancellationToken);
        }

        var existingNames = await context.Clients
            .Where(client => client.OrganisationId == organisation.Id)
            .Select(client => client.Name)
            .ToListAsync(cancellationToken);
        var existingNameSet = existingNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var clients = ClientNames
            .Where(name => !existingNameSet.Contains(name))
            .Select(name => Client.Create(organisation.Id, name))
            .ToArray();

        context.Clients.AddRange(clients);
        await context.SaveChangesAsync(cancellationToken);

        return new DevelopmentDataSeedResult(
            user.Id,
            organisation.Id,
            settings.Email,
            settings.OrganisationName,
            clients.Length,
            existingNames.Count + clients.Length);
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

        var organisations = await (
                from membership in context.OrganisationMemberships
                join candidate in context.Organisations
                    on membership.OrganisationId equals candidate.Id
                where membership.UserId == user.Id &&
                    candidate.Name == settings.OrganisationName
                select candidate)
            .ToListAsync(cancellationToken);

        if (organisations.Count > 1)
        {
            throw new InvalidOperationException(
                "The development account has multiple matching organisations; reset was stopped.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken);
        var organisation = organisations.SingleOrDefault();
        var assignmentCount = 0;
        var clientCount = 0;
        var workerCount = 0;
        if (organisation is not null)
        {
            assignmentCount = await context.Assignments
                .Where(assignment => assignment.OrganisationId == organisation.Id)
                .ExecuteDeleteAsync(cancellationToken);
            clientCount = await context.Clients
                .Where(client => client.OrganisationId == organisation.Id)
                .ExecuteDeleteAsync(cancellationToken);
            workerCount = await context.Workers
                .Where(worker => worker.OrganisationId == organisation.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await context.OrganisationMemberships
                .Where(membership => membership.OrganisationId == organisation.Id)
                .ExecuteDeleteAsync(cancellationToken);
            context.Organisations.Remove(organisation);
            await context.SaveChangesAsync(cancellationToken);
        }

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
            organisation is null ? 0 : 1,
            clientCount,
            workerCount,
            assignmentCount);
    }

    private async Task<Organisation> GetOrCreateOrganisationAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var organisations = await (
                from membership in context.OrganisationMemberships
                join organisation in context.Organisations
                    on membership.OrganisationId equals organisation.Id
                where membership.UserId == userId &&
                    organisation.Name == settings.OrganisationName
                select organisation)
            .ToListAsync(cancellationToken);

        return organisations.Count switch
        {
            0 => await organisationProvisioner.ProvisionAsync(
                    userId,
                    settings.OrganisationName,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "The development organisation could not be created."),
            1 => organisations[0],
            _ => throw new InvalidOperationException(
                "The development account has multiple matching organisations; seeding was stopped."),
        };
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(settings.Email) ||
            string.IsNullOrWhiteSpace(settings.Password) ||
            string.IsNullOrWhiteSpace(settings.OrganisationName))
        {
            throw new InvalidOperationException(
                "Development data email, password, and organisation name are required.");
        }
    }
}

public sealed record DevelopmentDataSeedResult(
    Guid UserId,
    Guid OrganisationId,
    string Email,
    string OrganisationName,
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
