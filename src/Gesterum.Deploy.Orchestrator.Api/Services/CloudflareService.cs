using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Gesterum.Deploy.Orchestrator.Api.Contracts;
using Gesterum.Deploy.Orchestrator.Api.Models;
using Gesterum.Deploy.Orchestrator.Api.Options;
using Microsoft.Extensions.Options;

namespace Gesterum.Deploy.Orchestrator.Api.Services;

public sealed class CloudflareService
{
    private readonly HttpClient _http;
    private readonly CloudflareOptions _opt;

    public CloudflareService(HttpClient http, IOptions<CloudflareOptions> opt)
    {
        _http = http;
        _opt = opt.Value;
    }

    public async Task<OperationResult> CreateDnsRecordAsync(CreateDnsRecordRequest req, CancellationToken ct)
    {
        if (_opt.DryRun)
        {
            return new OperationResult
            {
                Ok = true,
                Message = "dry-run: DNS record not created",
                Data = req
            };
        }

        if (string.IsNullOrWhiteSpace(_opt.ApiToken) || string.IsNullOrWhiteSpace(_opt.ZoneId))
        {
            return new OperationResult { Ok = false, Message = "Cloudflare ApiToken/ZoneId not configured" };
        }

        using var msg = new HttpRequestMessage(HttpMethod.Post,
            $"https://api.cloudflare.com/client/v4/zones/{_opt.ZoneId}/dns_records");

        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _opt.ApiToken);

        var payload = new
        {
            type = req.Type,
            name = req.Name,
            content = req.Content,
            ttl = req.Ttl,
            proxied = req.Proxied
        };

        msg.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(msg, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        return new OperationResult
        {
            Ok = res.IsSuccessStatusCode,
            Message = res.IsSuccessStatusCode ? "DNS record created" : "Cloudflare API error",
            Data = body
        };
    }
}
