using System.Diagnostics;
using System.Text.Json;
using Gesterum.Deploy.Orchestrator.Api.Contracts;
using Gesterum.Deploy.Orchestrator.Api.Models;
using Gesterum.Deploy.Orchestrator.Api.Options;
using Microsoft.Extensions.Options;

namespace Gesterum.Deploy.Orchestrator.Api.Services;

public sealed class DeployExecutorService
{
    private readonly DeployTemplateOptions _opt;

    public DeployExecutorService(IOptions<DeployTemplateOptions> opt)
    {
        _opt = opt.Value;
    }

    public Task<OperationResult> ExecuteAsync(DeployJob job, CancellationToken _)
    {
        if (!job.JobType.Equals("deploy.execute", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new OperationResult
            {
                Ok = true,
                Message = "job type ignored by executor",
                Data = new { job.JobType }
            });
        }

        ExecuteDeployRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<ExecuteDeployRequest>(job.PayloadJson);
        }
        catch (Exception ex)
        {
            return Task.FromResult(new OperationResult { Ok = false, Message = "invalid payload", Data = ex.Message });
        }

        if (req is null || string.IsNullOrWhiteSpace(req.StartCommand))
        {
            return Task.FromResult(new OperationResult { Ok = false, Message = "invalid deploy request" });
        }

        if (_opt.DryRun)
        {
            return Task.FromResult(new OperationResult
            {
                Ok = true,
                Message = "dry-run deploy execution",
                Data = req
            });
        }

        try
        {
            var psi = new ProcessStartInfo("/bin/sh", $"-lc \"cd {req.AppPath} && {req.StartCommand}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var proc = Process.Start(psi);
            if (proc is null) return Task.FromResult(new OperationResult { Ok = false, Message = "failed to start process" });

            var stdout = proc.StandardOutput.ReadToEnd();
            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            return Task.FromResult(new OperationResult
            {
                Ok = proc.ExitCode == 0,
                Message = proc.ExitCode == 0 ? "deploy command finished" : "deploy command failed",
                Data = new { proc.ExitCode, stdout, stderr }
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new OperationResult { Ok = false, Message = ex.Message });
        }
    }
}
