using System.Diagnostics;
using Gesterum.Deploy.Orchestrator.Api.Contracts;
using Gesterum.Deploy.Orchestrator.Api.Models;
using Gesterum.Deploy.Orchestrator.Api.Options;
using Microsoft.Extensions.Options;
using Renci.SshNet;

namespace Gesterum.Deploy.Orchestrator.Api.Services;

public sealed class NginxService
{
    private readonly NginxOptions _opt;

    public NginxService(IOptions<NginxOptions> opt)
    {
        _opt = opt.Value;
    }

    public Task<OperationResult> ExecuteAsync(NginxCommandRequest req, CancellationToken _)
    {
        if (_opt.DryRun)
        {
            return Task.FromResult(new OperationResult
            {
                Ok = true,
                Message = "dry-run: command not executed",
                Data = req
            });
        }

        var cmd = req.Action.ToLowerInvariant() switch
        {
            "status" => "systemctl is-active nginx",
            "test" => "/usr/sbin/nginx -t",
            "reload" => "systemctl reload nginx",
            "restart" => "systemctl restart nginx",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(cmd))
            return Task.FromResult(new OperationResult { Ok = false, Message = "unsupported action" });

        return _opt.Mode.Equals("ssh", StringComparison.OrdinalIgnoreCase)
            ? Task.FromResult(ExecuteOverSsh(cmd))
            : Task.FromResult(ExecuteLocal(cmd));
    }

    private OperationResult ExecuteLocal(string command)
    {
        try
        {
            var psi = new ProcessStartInfo("/bin/sh", $"-lc \"{command}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var p = Process.Start(psi);
            if (p is null) return new OperationResult { Ok = false, Message = "failed to start process" };

            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            return new OperationResult
            {
                Ok = p.ExitCode == 0,
                Message = p.ExitCode == 0 ? "nginx command executed" : "nginx command failed",
                Data = new { exitCode = p.ExitCode, stdout, stderr }
            };
        }
        catch (Exception ex)
        {
            return new OperationResult { Ok = false, Message = ex.Message };
        }
    }

    private OperationResult ExecuteOverSsh(string command)
    {
        try
        {
            using var ssh = new SshClient(_opt.SshHost, _opt.SshPort, _opt.SshUser, _opt.SshPassword);
            ssh.Connect();
            var result = ssh.RunCommand(command);
            ssh.Disconnect();

            return new OperationResult
            {
                Ok = result.ExitStatus == 0,
                Message = result.ExitStatus == 0 ? "ssh command executed" : "ssh command failed",
                Data = new { exitCode = result.ExitStatus, stdout = result.Result, stderr = result.Error }
            };
        }
        catch (Exception ex)
        {
            return new OperationResult { Ok = false, Message = ex.Message };
        }
    }
}
