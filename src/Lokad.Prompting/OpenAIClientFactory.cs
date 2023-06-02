namespace Lokad.Prompting;

public class OpenAIClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        return new HttpClient(new RetryHandler())
        {
            // The OpenAI API does usually answers albeit with considerable delay.
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    public class RetryHandler : DelegatingHandler
    {
        private const int maxRetries = 5;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var delay = TimeSpan.FromSeconds(1); // Initial delay
            for (int i = 0; i < maxRetries; i++)
            {
                var response = await base.SendAsync(request, cancellationToken);

                // 429 Too Many Requests
                if (response.StatusCode == (System.Net.HttpStatusCode)429 && i < maxRetries)
                {
                    await Task.Delay(delay, cancellationToken);
                    delay *= 2; // Exponential backoff
                    continue;
                }

                return response;
            }

            throw new HttpRequestException($"Retry attempts failed after {maxRetries} retries.");
        }
    }
}
