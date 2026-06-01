namespace Gesterum.Deploy.Orchestrator.Api.Contracts;

public sealed class ExecuteDeployRequest
{
    public string Runtime { get; set; } = "dotnet"; // dotnet|node|python
    public string Environment { get; set; } = "staging"; // staging|production
    public string AppPath { get; set; } = string.Empty;
    public string BuildCommand { get; set; } = string.Empty;
    public string StartCommand { get; set; } = string.Empty;
    public string HealthUrl { get; set; } = "http://127.0.0.1:5000/health";
    public int HealthTimeoutSeconds { get; set; } = 30;
    public string? Domain { get; set; }
    public int? Port { get; set; }
    public bool CreateOrUpdateNginxVhost { get; set; }
    public bool Dangerous { get; set; }
}
