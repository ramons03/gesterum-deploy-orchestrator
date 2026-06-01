namespace Gesterum.Deploy.Orchestrator.Api.Options;

public sealed class CloudflareOptions
{
    public const string SectionName = "Cloudflare";
    public string ApiToken { get; set; } = string.Empty;
    public string ZoneId { get; set; } = string.Empty;
    public bool DryRun { get; set; } = true;
}
