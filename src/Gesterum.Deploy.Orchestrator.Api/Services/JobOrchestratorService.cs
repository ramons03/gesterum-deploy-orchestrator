using System.Text.Json;
using Gesterum.Deploy.Orchestrator.Api.Contracts;
using Gesterum.Deploy.Orchestrator.Api.Data;
using Gesterum.Deploy.Orchestrator.Api.Models;
using Gesterum.Deploy.Orchestrator.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Gesterum.Deploy.Orchestrator.Api.Services;

public sealed class JobOrchestratorService
{
    private readonly AppDbContext _db;
    private readonly JobQueueService _queue;
    private readonly JobsOptions _opt;

    public JobOrchestratorService(AppDbContext db, JobQueueService queue, IOptions<JobsOptions> opt)
    {
        _db = db;
        _queue = queue;
        _opt = opt.Value;
    }

    public async Task<DeployJob> EnqueueAsync(EnqueueJobRequest req, CancellationToken ct)
    {
        var requiresApproval = false;
        if (_opt.RequireApprovalForDangerousActions && req.JobType.Equals("deploy.execute", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<ExecuteDeployRequest>(req.PayloadJson);
                requiresApproval = parsed?.Dangerous == true;
            }
            catch
            {
                requiresApproval = true;
            }
        }

        var job = new DeployJob
        {
            JobType = req.JobType,
            PayloadJson = req.PayloadJson,
            RequiresApproval = requiresApproval,
            Approved = !requiresApproval,
            Status = requiresApproval ? "queued" : "approved"
        };

        _db.Jobs.Add(job);
        await _db.SaveChangesAsync(ct);

        await _queue.EnqueueAsync(job.Id, ct);
        return job;
    }

    public async Task<DeployJob?> ApproveAsync(Guid id, bool approve, CancellationToken ct)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (job is null) return null;

        job.Approved = approve;
        job.Status = approve ? "approved" : "rejected";
        await _db.SaveChangesAsync(ct);

        if (approve)
            await _queue.EnqueueAsync(job.Id, ct);

        return job;
    }

    public Task<List<DeployJob>> ListAsync(CancellationToken ct)
        => _db.Jobs.OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync(ct);

    public Task<DeployJob?> GetAsync(Guid id, CancellationToken ct)
        => _db.Jobs.FirstOrDefaultAsync(x => x.Id == id, ct);
}
