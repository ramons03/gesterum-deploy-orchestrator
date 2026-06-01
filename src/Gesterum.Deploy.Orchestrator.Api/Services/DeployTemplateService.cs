using Gesterum.Deploy.Orchestrator.Api.Contracts;
using Gesterum.Deploy.Orchestrator.Api.Models;
using Gesterum.Deploy.Orchestrator.Api.Options;
using Microsoft.Extensions.Options;

namespace Gesterum.Deploy.Orchestrator.Api.Services;

public sealed class DeployTemplateService
{
    private readonly DeployTemplateOptions _opt;

    public DeployTemplateService(IOptions<DeployTemplateOptions> opt)
    {
        _opt = opt.Value;
    }

    public Task<OperationResult> BuildPlanAsync(DeployTemplateRequest req, CancellationToken _)
    {
        var plan = new
        {
            req.AppName,
            req.Runtime,
            req.Domain,
            req.Port,
            req.HealthPath,
            Host = _opt.DefaultHost,
            Steps = new[]
            {
                "Precheck ports/process/nginx",
                "Build artifact",
                "Prepare runtime folder",
                "Create/Update nginx vhost",
                "nginx -t and reload",
                "Health check and smoke test",
                "Rollback plan"
            }
        };

        return Task.FromResult(new OperationResult
        {
            Ok = true,
            Message = _opt.DryRun ? "dry-run deploy template plan" : "deploy template plan",
            Data = plan
        });
    }
}
