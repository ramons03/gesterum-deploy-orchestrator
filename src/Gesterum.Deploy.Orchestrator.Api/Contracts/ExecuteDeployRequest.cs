namespace Gesterum.Deploy.Orchestrator.Api.Contracts;

public sealed class ExecuteDeployRequest
{
    public string Runtime { get; set; } = "dotnet"; // dotnet|node|python
    public string AppPath { get; set; } = string.Empty;
    public string StartCommand { get; set; } = string.Empty;
    public string HealthUrl { get; set; } = "http://127.0.0.1:5000/health";
    public bool Dangerous { get; set; }
}
