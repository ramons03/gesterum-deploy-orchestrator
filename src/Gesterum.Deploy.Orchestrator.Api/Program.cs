using Gesterum.Deploy.Orchestrator.Api.Contracts;
using Gesterum.Deploy.Orchestrator.Api.Data;
using Gesterum.Deploy.Orchestrator.Api.Models;
using Gesterum.Deploy.Orchestrator.Api.Options;
using Gesterum.Deploy.Orchestrator.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Security.Claims;
using System.Text.Encodings.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<CloudflareOptions>(builder.Configuration.GetSection(CloudflareOptions.SectionName));
builder.Services.Configure<AwsOptions>(builder.Configuration.GetSection(AwsOptions.SectionName));
builder.Services.Configure<NginxOptions>(builder.Configuration.GetSection(NginxOptions.SectionName));
builder.Services.Configure<DeployTemplateOptions>(builder.Configuration.GetSection(DeployTemplateOptions.SectionName));
builder.Services.Configure<JobsOptions>(builder.Configuration.GetSection(JobsOptions.SectionName));
builder.Services.Configure<EnvironmentApprovalOptions>(builder.Configuration.GetSection(EnvironmentApprovalOptions.SectionName));

var jobsDataSource = builder.Configuration[$"{JobsOptions.SectionName}:DataSource"] ?? "Data Source=data/orchestrator.db";
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(jobsDataSource));

builder.Services.AddHttpClient<CloudflareService>();
builder.Services.AddScoped<SqsService>();
builder.Services.AddScoped<NginxService>();
builder.Services.AddScoped<NginxVhostService>();
builder.Services.AddScoped<DeployTemplateService>();
builder.Services.AddScoped<DeployExecutorService>();
builder.Services.AddScoped<JobOrchestratorService>();

builder.Services.AddSingleton<JobQueueService>();
builder.Services.AddHostedService<JobWorker>();

builder.Services.AddAuthentication("ApiKey")
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", _ => { });
builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { ok = true, utc = DateTime.UtcNow }));

app.MapPost("/api/cloudflare/dns", async (CreateDnsRecordRequest req, CloudflareService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Content))
        return Results.BadRequest(new { error = "name/content requeridos" });

    var res = await svc.CreateDnsRecordAsync(req, ct);
    return res.Ok ? Results.Ok(res) : Results.BadRequest(res);
}).RequireAuthorization();

app.MapPost("/api/aws/sqs", async (CreateSqsQueueRequest req, SqsService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.QueueName))
        return Results.BadRequest(new { error = "queueName requerido" });

    var res = await svc.CreateQueueAsync(req, ct);
    return res.Ok ? Results.Ok(res) : Results.BadRequest(res);
}).RequireAuthorization();

app.MapPost("/api/nginx", async (NginxCommandRequest req, NginxService svc, CancellationToken ct) =>
{
    var res = await svc.ExecuteAsync(req, ct);
    return res.Ok ? Results.Ok(res) : Results.BadRequest(res);
}).RequireAuthorization();

app.MapPost("/api/nginx/vhost", async (CreateNginxVhostRequest req, NginxVhostService svc, IOptions<DeployTemplateOptions> opt) =>
{
    var res = await svc.CreateOrUpdateAsync(req, opt.Value.DryRun);
    return res.Ok ? Results.Ok(res) : Results.BadRequest(res);
}).RequireAuthorization();

app.MapPost("/api/nginx/vhost/rollback", async (RollbackNginxVhostRequest req, NginxVhostService svc, IOptions<DeployTemplateOptions> opt) =>
{
    var res = await svc.RollbackAsync(req, opt.Value.DryRun);
    return res.Ok ? Results.Ok(res) : Results.BadRequest(res);
}).RequireAuthorization();

app.MapPost("/api/deploy/template", async (DeployTemplateRequest req, DeployTemplateService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.AppName) || string.IsNullOrWhiteSpace(req.Domain))
        return Results.BadRequest(new { error = "appName/domain requeridos" });

    var res = await svc.BuildPlanAsync(req, ct);
    return Results.Ok(res);
}).RequireAuthorization();

app.MapPost("/api/jobs/enqueue", async (EnqueueJobRequest req, JobOrchestratorService orchestrator, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.JobType))
        return Results.BadRequest(new { error = "jobType requerido" });

    var job = await orchestrator.EnqueueAsync(req, ct);
    return Results.Ok(new OperationResult { Ok = true, Message = "job enqueued", Data = job });
}).RequireAuthorization();

app.MapGet("/api/jobs", async (JobOrchestratorService orchestrator, CancellationToken ct) =>
{
    var jobs = await orchestrator.ListAsync(ct);
    return Results.Ok(jobs);
}).RequireAuthorization();

app.MapGet("/api/jobs/{id:guid}", async (Guid id, JobOrchestratorService orchestrator, CancellationToken ct) =>
{
    var job = await orchestrator.GetAsync(id, ct);
    return job is null ? Results.NotFound() : Results.Ok(job);
}).RequireAuthorization();

app.MapPost("/api/jobs/{id:guid}/approval", async (Guid id, ApproveJobRequest req, JobOrchestratorService orchestrator, CancellationToken ct) =>
{
    var job = await orchestrator.ApproveAsync(id, req.Approve, ct);
    return job is null ? Results.NotFound() : Results.Ok(job);
}).RequireAuthorization();

app.Run();

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly AuthOptions _auth;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<AuthOptions> authOptions)
        : base(options, logger, encoder)
    {
        _auth = authOptions.Value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!_auth.Enabled)
        {
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "anonymous") }, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        if (!Request.Headers.TryGetValue("X-API-Key", out var key))
            return Task.FromResult(AuthenticateResult.Fail("X-API-Key missing"));

        if (string.IsNullOrWhiteSpace(_auth.ApiKey) || key.ToString() != _auth.ApiKey)
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key"));

        var claims = new[] { new Claim(ClaimTypes.Name, "api-client") };
        var claimsIdentity = new ClaimsIdentity(claims, Scheme.Name);
        var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
        var authTicket = new AuthenticationTicket(claimsPrincipal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(authTicket));
    }
}
