namespace Gesterum.Deploy.Orchestrator.Api.Contracts;

public sealed class CreateDnsRecordRequest
{
    public string Type { get; set; } = "A"; // A|AAAA|CNAME|TXT
    public string Name { get; set; } = string.Empty; // ex: exfi.eldean.com.ar
    public string Content { get; set; } = string.Empty; // ex: 149.50.148.174
    public int Ttl { get; set; } = 120;
    public bool Proxied { get; set; } = true;
}
