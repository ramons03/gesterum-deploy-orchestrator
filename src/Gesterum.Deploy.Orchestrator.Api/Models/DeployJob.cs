namespace Gesterum.Deploy.Orchestrator.Api.Models;

public sealed class DeployJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string JobType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public string Status { get; set; } = "queued"; // queued|approved|running|succeeded|failed|rejected
    public bool RequiresApproval { get; set; }
    public bool Approved { get; set; }
    public string? Environment { get; set; }
    public string RequestedBy { get; set; } = "system";
    public string? Runtime { get; set; }
    public string? ResultJson { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
}
