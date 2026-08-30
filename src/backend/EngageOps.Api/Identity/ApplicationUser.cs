using Microsoft.AspNetCore.Identity;

namespace EngageOps.Api.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser()
    {
        // IdentityUser<Guid> does not initialise a usable key or security stamp for new users.
        Id = Guid.CreateVersion7();
        SecurityStamp = Guid.NewGuid().ToString();
    }
}
