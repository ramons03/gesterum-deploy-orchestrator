namespace Gesterum.Deploy.Orchestrator.Api.Contracts;

public sealed class CreateSqsQueueRequest
{
    public string QueueName { get; set; } = string.Empty;
    public bool Fifo { get; set; }
    public int VisibilityTimeoutSeconds { get; set; } = 30;
    public int MessageRetentionSeconds { get; set; } = 345600;
}
