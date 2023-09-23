using Azure.Core.Pipeline;
using Azure.Core;

public class AzureOpenAICompletionRetryPolicy : HttpPipelinePolicy
{
    private const int maxRetries = 10;

    public override void Process(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        RetryProcess(message, pipeline, async: false).GetAwaiter().GetResult();
    }

    public override async ValueTask ProcessAsync(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline)
    {
        await RetryProcess(message, pipeline, async: true);
    }

    private async ValueTask RetryProcess(HttpMessage message, ReadOnlyMemory<HttpPipelinePolicy> pipeline, bool async)
    {
        var delay = TimeSpan.FromSeconds(1); // Initial delay
        for (int i = 0; i < maxRetries; i++)
        {
            if (async)
            {
                await ProcessNextAsync(message, pipeline);
            }
            else
            {
                ProcessNext(message, pipeline);
            }

            if (!message.ResponseClassifier.IsErrorResponse(message))
            {
                return;
            }

            // Handle error responses here
            var statusCode = message.Response.Status;

            if ((statusCode == 408 || statusCode == 429 || statusCode == 500 ||
                 statusCode == 502 || statusCode == 503 || statusCode == 504) && i < maxRetries)
            {
                await Task.Delay(delay); // wait for the delay
                delay *= 2; // exponential backoff
            }
            else
            {
                return; // if the status is not one of the above or max retries exceeded, return the response
            }
        }

        throw new HttpRequestException($"Retry attempts failed after {maxRetries} retries.");
    }
}
