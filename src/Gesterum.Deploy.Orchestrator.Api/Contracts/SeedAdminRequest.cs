namespace Gesterum.Deploy.Orchestrator.Api.Contracts;

public sealed class SeedAdminRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Admin";
}
