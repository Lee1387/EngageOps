using EngageOps.Api.Organisations;
using EngageOps.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EngageOps.Api.Identity;

public sealed class AccountProvisioner(
    EngageOpsDbContext context,
    UserManager<ApplicationUser> userManager,
    OrganisationProvisioner organisationProvisioner)
{
    private const string UniqueUserNameIndexName = "UserNameIndex";

    public async Task<AccountProvisioningResult> ProvisionAsync(
        string email,
        string password,
        string organisationName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(organisationName);

        cancellationToken.ThrowIfCancellationRequested();

        // Identity and tenant setup must commit together so failed provisioning cannot leave an orphaned account.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var trimmedEmail = email.Trim();
        var user = new ApplicationUser
        {
            UserName = trimmedEmail,
            Email = trimmedEmail,
        };
        IdentityResult userCreation;
        try
        {
            userCreation = await userManager.CreateAsync(user, password);
        }
        catch (DbUpdateException exception) when (IsDuplicateUserNameViolation(exception))
        {
            // Identity checks first, but the database index arbitrates concurrent registration races.
            return new AccountProvisioningResult.Rejected(
                [userManager.ErrorDescriber.DuplicateUserName(trimmedEmail)]);
        }

        if (!userCreation.Succeeded)
        {
            return new AccountProvisioningResult.Rejected(userCreation.Errors.ToArray());
        }

        var organisation = await organisationProvisioner.ProvisionAsync(
            user.Id,
            organisationName,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The newly created account could not be associated with an organisation.");

        await transaction.CommitAsync(cancellationToken);

        return new AccountProvisioningResult.Created(user, organisation);
    }

    private static bool IsDuplicateUserNameViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: UniqueUserNameIndexName,
        };
}
