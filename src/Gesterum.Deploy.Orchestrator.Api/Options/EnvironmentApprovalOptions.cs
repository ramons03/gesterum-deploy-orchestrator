namespace Gesterum.Deploy.Orchestrator.Api.Options;

public sealed class EnvironmentApprovalOptions
{
    public const string SectionName = "EnvironmentApproval";
    public bool RequireApprovalInStaging { get; set; } = false;
    public bool RequireApprovalInProduction { get; set; } = true;
}
