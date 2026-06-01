namespace Gesterum.Deploy.Orchestrator.Api.Models;

public sealed class OperationResult
{
    public bool Ok { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Data { get; set; }
}
