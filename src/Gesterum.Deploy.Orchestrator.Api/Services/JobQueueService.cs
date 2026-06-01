using System.Threading.Channels;
using Gesterum.Deploy.Orchestrator.Api.Models;

namespace Gesterum.Deploy.Orchestrator.Api.Services;

public sealed class JobQueueService
{
    private readonly Channel<DeployJob> _channel = Channel.CreateUnbounded<DeployJob>();

    public ValueTask EnqueueAsync(DeployJob job, CancellationToken ct)
        => _channel.Writer.WriteAsync(job, ct);

    public IAsyncEnumerable<DeployJob> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}
