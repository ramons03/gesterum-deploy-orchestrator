namespace Gesterum.Deploy.Orchestrator.Api.Contracts;

public sealed class CreateNginxVhostRequest
{
    public string Domain { get; set; } = string.Empty;
    public int UpstreamPort { get; set; }
    public bool EnableTlsRedirect { get; set; } = true;
}
