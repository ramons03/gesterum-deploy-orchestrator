namespace Gesterum.Deploy.Orchestrator.Api.Contracts;

public sealed class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public string Role { get; set; } = string.Empty;
}
