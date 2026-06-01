using Gesterum.Deploy.Orchestrator.Api.Models;

namespace Gesterum.Deploy.Orchestrator.Api.Services;

public sealed class JobWorker : BackgroundService
{
    private readonly ILogger<JobWorker> _logger;
    private readonly JobQueueService _queue;

    public JobWorker(ILogger<JobWorker> logger, JobQueueService queue)
    {
        _logger = logger;
        _queue = queue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Processing job {JobId} type={JobType}", job.Id, job.JobType);
                await Task.Delay(100, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {JobId} failed", job.Id);
            }
        }
    }
}
