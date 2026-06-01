namespace Gesterum.Deploy.Orchestrator.Api.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "gesterum-orchestrator";
    public string Audience { get; set; } = "gesterum-operators";
    public string Key { get; set; } = "CHANGE_ME_MIN_32_CHARS_SECRET_KEY";
    public int ExpirationMinutes { get; set; } = 120;
}
