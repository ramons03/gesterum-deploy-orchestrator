namespace Gesterum.Deploy.Orchestrator.Api.Options;

public sealed class AwsOptions
{
    public const string SectionName = "Aws";
    public string Region { get; set; } = "us-east-1";
    public bool DryRun { get; set; } = true;
}
