using System.ClientModel.Primitives;

namespace Lokad.Prompting;

/// <remarks>
/// When instantiating the client with direct OpenAI settings, it appears
/// that no retry policy is setup, or that the one being setup is ineffective
/// as the OpenAI API diverges from the Azure OpenAI one. This retry policy
/// ensures that the rate limiting is enforced on the direct OpenAI side.
/// </remarks>
public class RateLimitingPolicy : PipelinePolicy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _baseDelay;

    public RateLimitingPolicy(int maxRetries = 5, TimeSpan? baseDelay = null)
    {
        _maxRetries = maxRetries;
        _baseDelay = baseDelay ?? TimeSpan.FromSeconds(3);
    }

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        int retryCount = 0;

        while (true)
        {
            ProcessNext(message, pipeline, currentIndex);

            if (message.Response.Status != 429 || retryCount >= _maxRetries)
            {
                break;
            }

            retryCount++;
            var delay = CalculateExponentialDelay(retryCount);
            Thread.Sleep(delay); // Synchronous delay
        }
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        int retryCount = 0;

        while (true)
        {
            await ProcessNextAsync(message, pipeline, currentIndex);

            if (message.Response.Status != 429 || retryCount >= _maxRetries)
            {
                break;
            }

            retryCount++;
            var delay = CalculateExponentialDelay(retryCount);
            await Task.Delay(delay, message.CancellationToken);
        }
    }

    private TimeSpan CalculateExponentialDelay(int retryCount)
    {
        return TimeSpan.FromTicks(_baseDelay.Ticks * (long)Math.Pow(2, retryCount - 1));
    }
}
