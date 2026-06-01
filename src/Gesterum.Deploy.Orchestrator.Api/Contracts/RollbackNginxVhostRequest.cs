namespace Gesterum.Deploy.Orchestrator.Api.Contracts;

public sealed class RollbackNginxVhostRequest
{
    public string Domain { get; set; } = string.Empty;
    public string SnapshotFileName { get; set; } = string.Empty;
}
