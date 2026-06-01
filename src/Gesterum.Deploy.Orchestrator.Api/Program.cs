using Gesterum.Deploy.Orchestrator.Api.Contracts;
using Gesterum.Deploy.Orchestrator.Api.Data;
using Gesterum.Deploy.Orchestrator.Api.Models;
using Gesterum.Deploy.Orchestrator.Api.Options;
using Gesterum.Deploy.Orchestrator.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

builder.Services.Configure<CloudflareOptions>(builder.Configuration.GetSection(CloudflareOptions.SectionName));
builder.Services.Configure<AwsOptions>(builder.Configuration.GetSection(AwsOptions.SectionName));
builder.Services.Configure<NginxOptions>(builder.Configuration.GetSection(NginxOptions.SectionName));
builder.Services.Configure<DeployTemplateOptions>(builder.Configuration.GetSection(DeployTemplateOptions.SectionName));
builder.Services.Configure<JobsOptions>(builder.Configuration.GetSection(JobsOptions.SectionName));
builder.Services.Configure<EnvironmentApprovalOptions>(builder.Configuration.GetSection(EnvironmentApprovalOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var jobsDataSource = builder.Configuration[$"{JobsOptions.SectionName}:DataSource"] ?? "Data Source=data/orchestrator.db";
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(jobsDataSource));

builder.Services
    .AddIdentityCore<AppUser>(opt =>
    {
        opt.Password.RequireDigit = true;
        opt.Password.RequiredLength = 8;
        opt.Password.RequireUppercase = true;
        opt.Password.RequireLowercase = true;
        opt.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddHttpClient<CloudflareService>();
builder.Services.AddScoped<SqsService>();
builder.Services.AddScoped<NginxService>();
builder.Services.AddScoped<NginxVhostService>();
builder.Services.AddScoped<DeployTemplateService>();
builder.Services.AddScoped<DeployExecutorService>();
builder.Services.AddScoped<JobOrchestratorService>();
builder.Services.AddScoped<JwtService>();

builder.Services.AddSingleton<JobQueueService>();
builder.Services.AddHostedService<JobWorker>();

var jwtOpt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpt.Key));

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOpt.Issuer,
            ValidAudience = jwtOpt.Audience,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization(opt =>
{
    opt.AddPolicy("RequireOperator", p => p.RequireRole("operator", "admin"));
    opt.AddPolicy("RequireReviewer", p => p.RequireRole("reviewer", "admin"));
    opt.AddPolicy("RequireAdmin", p => p.RequireRole("admin"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var roles = new[] { "admin", "operator", "reviewer" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }
}

app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { ok = true, utc = DateTime.UtcNow }));

app.MapPost("/api/auth/seed-admin", async (SeedAdminRequest req, UserManager<AppUser> users) =>
{
    if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { error = "email/password requeridos" });

    var exists = await users.FindByEmailAsync(req.Email);
    if (exists is not null)
        return Results.BadRequest(new { error = "admin already exists" });

    var user = new AppUser
    {
        UserName = req.Email,
        Email = req.Email,
        DisplayName = req.DisplayName
    };

    var result = await users.CreateAsync(user, req.Password);
    if (!result.Succeeded)
        return Results.BadRequest(result.Errors);

    await users.AddToRoleAsync(user, "admin");
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/auth/login", async (LoginRequest req, UserManager<AppUser> users, SignInManager<AppUser> signIn, JwtService jwt) =>
{
    var user = await users.FindByEmailAsync(req.Email);
    if (user is null)
        return Results.BadRequest(new { error = "invalid credentials" });

    var passOk = await signIn.CheckPasswordSignInAsync(user, req.Password, false);
    if (!passOk.Succeeded)
        return Results.BadRequest(new { error = "invalid credentials" });

    var roles = await users.GetRolesAsync(user);
    var (token, expiresAtUtc) = jwt.CreateToken(user, roles);

    var role = roles.FirstOrDefault() ?? "operator";
    return Results.Ok(new LoginResponse { Token = token, ExpiresAtUtc = expiresAtUtc, Role = role });
});

app.MapPost("/api/cloudflare/dns", async (CreateDnsRecordRequest req, CloudflareService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Content))
        return Results.BadRequest(new { error = "name/content requeridos" });

    var res = await svc.CreateDnsRecordAsync(req, ct);
    return res.Ok ? Results.Ok(res) : Results.BadRequest(res);
}).RequireAuthorization("RequireOperator");

app.MapPost("/api/aws/sqs", async (CreateSqsQueueRequest req, SqsService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.QueueName))
        return Results.BadRequest(new { error = "queueName requerido" });

    var res = await svc.CreateQueueAsync(req, ct);
    return res.Ok ? Results.Ok(res) : Results.BadRequest(res);
}).RequireAuthorization("RequireOperator");

app.MapPost("/api/nginx", async (NginxCommandRequest req, NginxService svc, CancellationToken ct) =>
{
    var res = await svc.ExecuteAsync(req, ct);
    return res.Ok ? Results.Ok(res) : Results.BadRequest(res);
}).RequireAuthorization("RequireOperator");

app.MapPost("/api/nginx/vhost", async (CreateNginxVhostRequest req, NginxVhostService svc, IOptions<DeployTemplateOptions> opt) =>
{
    var res = await svc.CreateOrUpdateAsync(req, opt.Value.DryRun);
    return res.Ok ? Results.Ok(res) : Results.BadRequest(res);
}).RequireAuthorization("RequireOperator");

app.MapPost("/api/nginx/vhost/rollback", async (RollbackNginxVhostRequest req, NginxVhostService svc, IOptions<DeployTemplateOptions> opt) =>
{
    var res = await svc.RollbackAsync(req, opt.Value.DryRun);
    return res.Ok ? Results.Ok(res) : Results.BadRequest(res);
}).RequireAuthorization("RequireOperator");

app.MapPost("/api/deploy/template", async (DeployTemplateRequest req, DeployTemplateService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.AppName) || string.IsNullOrWhiteSpace(req.Domain))
        return Results.BadRequest(new { error = "appName/domain requeridos" });

    var res = await svc.BuildPlanAsync(req, ct);
    return Results.Ok(res);
}).RequireAuthorization("RequireOperator");

app.MapPost("/api/jobs/enqueue", async (EnqueueJobRequest req, JobOrchestratorService orchestrator, ClaimsPrincipal user, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.JobType))
        return Results.BadRequest(new { error = "jobType requerido" });

    if (string.IsNullOrWhiteSpace(req.RequestedBy))
        req.RequestedBy = user.Identity?.Name ?? "unknown";

    var job = await orchestrator.EnqueueAsync(req, ct);
    return Results.Ok(new OperationResult { Ok = true, Message = "job enqueued", Data = job });
}).RequireAuthorization("RequireOperator");

app.MapGet("/api/jobs", async (JobOrchestratorService orchestrator, CancellationToken ct) =>
{
    var jobs = await orchestrator.ListAsync(ct);
    return Results.Ok(jobs);
}).RequireAuthorization("RequireOperator");

app.MapGet("/api/jobs/{id:guid}", async (Guid id, JobOrchestratorService orchestrator, CancellationToken ct) =>
{
    var job = await orchestrator.GetAsync(id, ct);
    return job is null ? Results.NotFound() : Results.Ok(job);
}).RequireAuthorization("RequireOperator");

app.MapPost("/api/jobs/{id:guid}/approval", async (Guid id, ApproveJobRequest req, JobOrchestratorService orchestrator, CancellationToken ct) =>
{
    var job = await orchestrator.ApproveAsync(id, req.Approve, ct);
    return job is null ? Results.NotFound() : Results.Ok(job);
}).RequireAuthorization("RequireReviewer");

app.Run();
