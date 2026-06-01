using Gesterum.Deploy.Orchestrator.Api.Contracts;
using Gesterum.Deploy.Orchestrator.Api.Options;
using Gesterum.Deploy.Orchestrator.Api.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

builder.Services.Configure<CloudflareOptions>(builder.Configuration.GetSection(CloudflareOptions.SectionName));
builder.Services.Configure<AwsOptions>(builder.Configuration.GetSection(AwsOptions.SectionName));
builder.Services.Configure<NginxOptions>(builder.Configuration.GetSection(NginxOptions.SectionName));

builder.Services.AddHttpClient<CloudflareService>();
builder.Services.AddScoped<SqsService>();
builder.Services.AddScoped<NginxService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new { ok = true, utc = DateTime.UtcNow }));

app.MapPost("/api/cloudflare/dns", async (CreateDnsRecordRequest req, CloudflareService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Content))
        return Results.BadRequest(new { error = "name/content requeridos" });

    var res = await svc.CreateDnsRecordAsync(req, ct);
    return res.Ok ? Results.Ok(res) : Results.BadRequest(res);
});

app.MapPost("/api/aws/sqs", async (CreateSqsQueueRequest req, SqsService svc, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.QueueName))
        return Results.BadRequest(new { error = "queueName requerido" });

    var res = await svc.CreateQueueAsync(req, ct);
    return res.Ok ? Results.Ok(res) : Results.BadRequest(res);
});

app.MapPost("/api/nginx", async (NginxCommandRequest req, NginxService svc, CancellationToken ct) =>
{
    var res = await svc.ExecuteAsync(req, ct);
    return res.Ok ? Results.Ok(res) : Results.BadRequest(res);
});

app.Run();
