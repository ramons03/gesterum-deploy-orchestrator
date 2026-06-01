using System.Diagnostics;
using Gesterum.Deploy.Orchestrator.Api.Contracts;
using Gesterum.Deploy.Orchestrator.Api.Models;

namespace Gesterum.Deploy.Orchestrator.Api.Services;

public sealed class NginxVhostService
{
    private const string SitesAvailable = "/etc/nginx/sites-available";
    private const string SitesEnabled = "/etc/nginx/sites-enabled";

    public Task<OperationResult> CreateOrUpdateAsync(CreateNginxVhostRequest req, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(req.Domain) || req.UpstreamPort <= 0)
            return Task.FromResult(new OperationResult { Ok = false, Message = "domain/upstreamPort invalid" });

        var filePath = Path.Combine(SitesAvailable, req.Domain + ".conf");
        var snapshotDir = Path.Combine(SitesAvailable, "snapshots");
        Directory.CreateDirectory(snapshotDir);

        var snapshotName = $"{req.Domain}.conf.{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
        var snapshotPath = Path.Combine(snapshotDir, snapshotName);

        var content = BuildVhost(req.Domain, req.UpstreamPort, req.EnableTlsRedirect);

        if (dryRun)
        {
            return Task.FromResult(new OperationResult
            {
                Ok = true,
                Message = "dry-run: nginx vhost not written",
                Data = new { req.Domain, req.UpstreamPort, snapshot = snapshotPath }
            });
        }

        if (File.Exists(filePath))
            File.Copy(filePath, snapshotPath, overwrite: true);

        File.WriteAllText(filePath, content);

        var enabledPath = Path.Combine(SitesEnabled, req.Domain + ".conf");
        if (!File.Exists(enabledPath))
            RunShell($"ln -s {filePath} {enabledPath}");

        var test = RunShell("/usr/sbin/nginx -t");
        if (!test.ok)
            return Task.FromResult(new OperationResult { Ok = false, Message = "nginx -t failed", Data = test });

        var reload = RunShell("systemctl reload nginx");
        if (!reload.ok)
            return Task.FromResult(new OperationResult { Ok = false, Message = "nginx reload failed", Data = reload });

        return Task.FromResult(new OperationResult
        {
            Ok = true,
            Message = "nginx vhost applied",
            Data = new { req.Domain, snapshot = snapshotName }
        });
    }

    public Task<OperationResult> RollbackAsync(RollbackNginxVhostRequest req, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(req.Domain) || string.IsNullOrWhiteSpace(req.SnapshotFileName))
            return Task.FromResult(new OperationResult { Ok = false, Message = "domain/snapshot required" });

        var filePath = Path.Combine(SitesAvailable, req.Domain + ".conf");
        var snapshotPath = Path.Combine(SitesAvailable, "snapshots", req.SnapshotFileName);

        if (!File.Exists(snapshotPath))
            return Task.FromResult(new OperationResult { Ok = false, Message = "snapshot not found" });

        if (dryRun)
        {
            return Task.FromResult(new OperationResult
            {
                Ok = true,
                Message = "dry-run: rollback not executed",
                Data = new { req.Domain, req.SnapshotFileName }
            });
        }

        File.Copy(snapshotPath, filePath, overwrite: true);

        var test = RunShell("/usr/sbin/nginx -t");
        if (!test.ok)
            return Task.FromResult(new OperationResult { Ok = false, Message = "nginx -t failed after rollback", Data = test });

        var reload = RunShell("systemctl reload nginx");
        if (!reload.ok)
            return Task.FromResult(new OperationResult { Ok = false, Message = "nginx reload failed after rollback", Data = reload });

        return Task.FromResult(new OperationResult { Ok = true, Message = "rollback applied" });
    }

    private static string BuildVhost(string domain, int upstreamPort, bool tlsRedirect)
    {
        var redirectBlock = tlsRedirect
            ? "    location / {\n        return 301 https://$host$request_uri;\n    }"
            : "    location / {\n        proxy_pass http://127.0.0.1:" + upstreamPort + "/;\n        proxy_http_version 1.1;\n        proxy_set_header Host $host;\n        proxy_set_header X-Real-IP $remote_addr;\n        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;\n        proxy_set_header X-Forwarded-Proto $scheme;\n    }";

        return $"server {{\n    listen 80;\n    server_name {domain};\n\n{redirectBlock}\n}}\n";
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
        if (p is null) return (false, -1, string.Empty, "process start failed");

        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return (p.ExitCode == 0, p.ExitCode, stdout, stderr);
    }
}
