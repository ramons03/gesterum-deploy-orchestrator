namespace Gesterum.Deploy.Orchestrator.Api.Contracts;

public sealed class EnqueueJobRequest
{
    public string JobType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
}
