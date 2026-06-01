namespace Gesterum.Deploy.Orchestrator.Api.Options;

public sealed class JobsOptions
{
    public const string SectionName = "Jobs";
    public bool RequireApprovalForDangerousActions { get; set; } = true;
    public string DataSource { get; set; } = "Data Source=data/orchestrator.db";
}
