using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using Gesterum.Deploy.Orchestrator.Api.Contracts;
using Gesterum.Deploy.Orchestrator.Api.Models;
using Gesterum.Deploy.Orchestrator.Api.Options;
using Microsoft.Extensions.Options;

namespace Gesterum.Deploy.Orchestrator.Api.Services;

public sealed class SqsService
{
    private readonly AwsOptions _opt;

    public SqsService(IOptions<AwsOptions> opt)
    {
        _opt = opt.Value;
    }

    public async Task<OperationResult> CreateQueueAsync(CreateSqsQueueRequest req, CancellationToken ct)
    {
        if (_opt.DryRun)
        {
            return new OperationResult
            {
                Ok = true,
                Message = "dry-run: queue not created",
                Data = req
            };
        }

        var region = RegionEndpoint.GetBySystemName(_opt.Region);
        using var client = new AmazonSQSClient(region);

        var queueName = req.Fifo && !req.QueueName.EndsWith(".fifo", StringComparison.OrdinalIgnoreCase)
            ? req.QueueName + ".fifo"
            : req.QueueName;

        var attrs = new Dictionary<string, string>
        {
            ["VisibilityTimeout"] = req.VisibilityTimeoutSeconds.ToString(),
            ["MessageRetentionPeriod"] = req.MessageRetentionSeconds.ToString()
        };

        if (req.Fifo)
        {
            attrs["FifoQueue"] = "true";
            attrs["ContentBasedDeduplication"] = "true";
        }

        var response = await client.CreateQueueAsync(new CreateQueueRequest
        {
            QueueName = queueName,
            Attributes = attrs
        }, ct);

        return new OperationResult
        {
            Ok = true,
            Message = "SQS queue created",
            Data = new { response.QueueUrl }
        };
    }
}
