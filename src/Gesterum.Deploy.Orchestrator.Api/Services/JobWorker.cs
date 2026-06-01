using System.Text.Json;
using Gesterum.Deploy.Orchestrator.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Gesterum.Deploy.Orchestrator.Api.Services;

public sealed class JobWorker : BackgroundService
{
    private readonly ILogger<JobWorker> _logger;
    private readonly IServiceProvider _sp;
    private readonly JobQueueService _queue;

    public JobWorker(ILogger<JobWorker> logger, IServiceProvider sp, JobQueueService queue)
    {
        _logger = logger;
        _sp = sp;
        _queue = queue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var jobId in _queue.ReadAllAsync(stoppingToken))
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var executor = scope.ServiceProvider.GetRequiredService<DeployExecutorService>();

            var job = await db.Jobs.FirstOrDefaultAsync(x => x.Id == jobId, stoppingToken);
            if (job is null) continue;

            if (job.RequiresApproval && !job.Approved)
            {
                _logger.LogInformation("Job {JobId} waiting approval", job.Id);
                continue;
            }

            try
            {
                job.Status = "running";
                job.StartedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(stoppingToken);

                var result = await executor.ExecuteAsync(job, stoppingToken);
                job.ResultJson = JsonSerializer.Serialize(result);
                job.Status = result.Ok ? "succeeded" : "failed";
                job.Error = result.Ok ? null : result.Message;
                job.FinishedAtUtc = DateTime.UtcNow;

                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                job.Status = "failed";
                job.Error = ex.Message;
                job.FinishedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(stoppingToken);
                _logger.LogError(ex, "Job {JobId} failed", job.Id);
            }
        }
    }
}
