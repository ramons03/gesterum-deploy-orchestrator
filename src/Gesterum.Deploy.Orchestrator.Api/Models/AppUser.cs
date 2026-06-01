using Microsoft.AspNetCore.Identity;

namespace Gesterum.Deploy.Orchestrator.Api.Models;

public sealed class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}
