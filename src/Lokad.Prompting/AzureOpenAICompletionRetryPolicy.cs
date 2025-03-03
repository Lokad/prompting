using System.ClientModel.Primitives;

public class AzureOpenAICompletionRetryPolicy : PipelinePolicy
{
    private const int maxRetries = 10;

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        RetryProcess(message, pipeline, currentIndex, async: false).GetAwaiter().GetResult();
    }

    public override async ValueTask ProcessAsync(PipelineMessage message,
        IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        await RetryProcess(message, pipeline, currentIndex, async: true);
    }

    private async ValueTask RetryProcess(PipelineMessage message,
        IReadOnlyList<PipelinePolicy> pipeline, int currentIndex, bool async)
    {
        var delay = TimeSpan.FromSeconds(1); // Initial delay
        for (int i = 0; i < maxRetries; i++)
        {
            if (async)
            {
                await ProcessNextAsync(message, pipeline, currentIndex);
            }
            else
            {
                ProcessNext(message, pipeline, currentIndex);
            }

            if (message.ResponseClassifier.TryClassify(message, out var isError) && !isError)
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
