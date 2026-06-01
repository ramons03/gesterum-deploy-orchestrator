namespace Gesterum.Deploy.Orchestrator.Api.Contracts;

public sealed class NginxCommandRequest
{
    public string Action { get; set; } = "status"; // status|test|reload|restart
}
