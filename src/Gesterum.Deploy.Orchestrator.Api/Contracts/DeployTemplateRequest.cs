namespace Gesterum.Deploy.Orchestrator.Api.Contracts;

public sealed class DeployTemplateRequest
{
    public string AppName { get; set; } = string.Empty;
    public string Runtime { get; set; } = "dotnet"; // dotnet|node|python
    public string Domain { get; set; } = string.Empty;
    public int Port { get; set; } = 5000;
    public string HealthPath { get; set; } = "/health";
}
