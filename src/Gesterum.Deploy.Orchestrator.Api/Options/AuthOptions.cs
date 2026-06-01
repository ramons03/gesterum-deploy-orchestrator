namespace Gesterum.Deploy.Orchestrator.Api.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public bool Enabled { get; set; } = false;
    public string ApiKey { get; set; } = string.Empty;
}
