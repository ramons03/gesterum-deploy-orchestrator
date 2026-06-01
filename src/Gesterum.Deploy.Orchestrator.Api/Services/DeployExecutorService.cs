using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using Gesterum.Deploy.Orchestrator.Api.Contracts;
using Gesterum.Deploy.Orchestrator.Api.Models;
using Gesterum.Deploy.Orchestrator.Api.Options;
using Microsoft.Extensions.Options;

namespace Gesterum.Deploy.Orchestrator.Api.Services;

public sealed class DeployExecutorService
{
    private static readonly HashSet<string> AllowedDotnetBuild = new(StringComparer.OrdinalIgnoreCase)
    {
        "dotnet build -c Release",
        "dotnet publish -c Release"
    };

    private static readonly HashSet<string> AllowedNodeBuild = new(StringComparer.OrdinalIgnoreCase)
    {
        "npm run build",
        "pnpm build"
    };

    private static readonly HashSet<string> AllowedNodeStart = new(StringComparer.OrdinalIgnoreCase)
    {
        "npm run start",
        "node server.js",
        "pm2 restart all"
    };

    private static readonly HashSet<string> AllowedDotnetStart = new(StringComparer.OrdinalIgnoreCase)
    {
        "./start.sh",
        "dotnet run -c Release"
    };

    private readonly DeployTemplateOptions _opt;
    private readonly NginxVhostService _vhostService;
    private readonly HttpClient _httpClient = new();

    public DeployExecutorService(IOptions<DeployTemplateOptions> opt, NginxVhostService vhostService)
    {
        _opt = opt.Value;
        _vhostService = vhostService;
    }

    public async Task<OperationResult> ExecuteAsync(DeployJob job, CancellationToken ct)
    {
        if (!job.JobType.Equals("deploy.execute", StringComparison.OrdinalIgnoreCase))
        {
            return new OperationResult
            {
                Ok = true,
                Message = "job type ignored by executor",
                Data = new { job.JobType }
            };
        }

        ExecuteDeployRequest? req;
        try
        {
            req = JsonSerializer.Deserialize<ExecuteDeployRequest>(job.PayloadJson);
        }
        catch (Exception ex)
        {
            return new OperationResult { Ok = false, Message = "invalid payload", Data = ex.Message };
        }

        if (req is null || string.IsNullOrWhiteSpace(req.StartCommand) || string.IsNullOrWhiteSpace(req.AppPath))
            return new OperationResult { Ok = false, Message = "invalid deploy request" };

        var validation = ValidateCommands(req);
        if (!validation.Ok)
            return validation;

        if (_opt.DryRun)
        {
            return new OperationResult
            {
                Ok = true,
                Message = "dry-run deploy execution",
                Data = req
            };
        }

        var outputs = new List<object>();

        var buildCommand = ResolveBuildCommand(req);
        if (!string.IsNullOrWhiteSpace(buildCommand))
        {
            var buildRes = RunShell($"cd {req.AppPath} && {buildCommand}");
            outputs.Add(new { step = "build", buildRes.code, buildRes.stdout, buildRes.stderr });
            if (!buildRes.ok)
                return new OperationResult { Ok = false, Message = "build failed", Data = outputs };
        }

        var startRes = RunShell($"cd {req.AppPath} && {req.StartCommand}");
        outputs.Add(new { step = "start", startRes.code, startRes.stdout, startRes.stderr });
        if (!startRes.ok)
            return new OperationResult { Ok = false, Message = "start command failed", Data = outputs };

        if (req.CreateOrUpdateNginxVhost && !string.IsNullOrWhiteSpace(req.Domain) && req.Port.HasValue)
        {
            var vhostRes = await _vhostService.CreateOrUpdateAsync(new CreateNginxVhostRequest
            {
                Domain = req.Domain,
                UpstreamPort = req.Port.Value,
                EnableTlsRedirect = true
            }, dryRun: false);

            outputs.Add(new { step = "nginx-vhost", result = vhostRes });
            if (!vhostRes.Ok)
                return new OperationResult { Ok = false, Message = "nginx vhost failed", Data = outputs };
        }

        var healthOk = await WaitHealth(req.HealthUrl, req.HealthTimeoutSeconds, ct);
        outputs.Add(new { step = "health", ok = healthOk, req.HealthUrl, req.HealthTimeoutSeconds });

        if (!healthOk)
            return new OperationResult { Ok = false, Message = "health check failed", Data = outputs };

        return new OperationResult { Ok = true, Message = "deploy execution succeeded", Data = outputs };
    }

    private static OperationResult ValidateCommands(ExecuteDeployRequest req)
    {
        var runtime = req.Runtime.ToLowerInvariant();
        var build = ResolveBuildCommand(req);
        var start = req.StartCommand.Trim();

        var buildAllowed = runtime switch
        {
            "dotnet" => string.IsNullOrWhiteSpace(build) || AllowedDotnetBuild.Contains(build),
            "node" => string.IsNullOrWhiteSpace(build) || AllowedNodeBuild.Contains(build),
            "python" => string.IsNullOrWhiteSpace(build),
            _ => false
        };

        var startAllowed = runtime switch
        {
            "dotnet" => AllowedDotnetStart.Contains(start),
            "node" => AllowedNodeStart.Contains(start),
            "python" => start.Equals("python3 app.py", StringComparison.OrdinalIgnoreCase) || start.Equals("uvicorn app:app --host 0.0.0.0 --port 8000", StringComparison.OrdinalIgnoreCase),
            _ => false
        };

        if (!buildAllowed || !startAllowed)
        {
            return new OperationResult
            {
                Ok = false,
                Message = "command not allowed by runtime policy",
                Data = new { req.Runtime, build, start }
            };
        }

        return new OperationResult { Ok = true, Message = "commands validated" };
    }

    private static string ResolveBuildCommand(ExecuteDeployRequest req)
    {
        if (!string.IsNullOrWhiteSpace(req.BuildCommand))
            return req.BuildCommand.Trim();

        return req.Runtime.ToLowerInvariant() switch
        {
            "dotnet" => "dotnet build -c Release",
            "node" => "npm run build",
            "python" => string.Empty,
            _ => string.Empty
        };
    }

    private async Task<bool> WaitHealth(string url, int timeoutSeconds, CancellationToken ct)
    {
        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalSeconds < timeoutSeconds)
        {
            try
            {
                using var res = await _httpClient.GetAsync(url, ct);
                if (res.IsSuccessStatusCode)
                    return true;
            }
            catch
            {
            }

            await Task.Delay(1000, ct);
        }

        return false;
    }

    private static (bool ok, int code, string stdout, string stderr) RunShell(string command)
    {
        var psi = new ProcessStartInfo("/bin/sh", $"-lc \"{command}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        using var p = Process.Start(psi);
        if (p is null) return (false, -1, string.Empty, "failed to start process");

        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode == 0, p.ExitCode, stdout, stderr);
    }
}
