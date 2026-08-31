using EngageOps.Api.Organisations;
using Microsoft.AspNetCore.Identity;

namespace EngageOps.Api.Identity;

public abstract record AccountProvisioningResult
{
    private AccountProvisioningResult()
    {
    }

    public sealed record Created(ApplicationUser User, Organisation Organisation)
        : AccountProvisioningResult;

    public sealed record Rejected(IReadOnlyList<IdentityError> Errors)
        : AccountProvisioningResult;
}
