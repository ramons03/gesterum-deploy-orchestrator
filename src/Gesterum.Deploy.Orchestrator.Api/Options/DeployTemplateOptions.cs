namespace Gesterum.Deploy.Orchestrator.Api.Options;

public sealed class DeployTemplateOptions
{
    public const string SectionName = "DeployTemplates";
    public bool DryRun { get; set; } = true;
    public string DefaultHost { get; set; } = "127.0.0.1";
}
