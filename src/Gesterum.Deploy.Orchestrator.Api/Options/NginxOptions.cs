namespace Gesterum.Deploy.Orchestrator.Api.Options;

public sealed class NginxOptions
{
    public const string SectionName = "Nginx";
    public bool DryRun { get; set; } = true;
    public string Mode { get; set; } = "local"; // local|ssh

    public string SshHost { get; set; } = string.Empty;
    public int SshPort { get; set; } = 22;
    public string SshUser { get; set; } = string.Empty;
    public string SshPassword { get; set; } = string.Empty;
}
