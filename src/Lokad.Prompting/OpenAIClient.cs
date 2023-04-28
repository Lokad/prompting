using OpenAI_API;
using OpenAI_API.Completions;
using OpenAI_API.Models;
using SharpToken;

namespace Lokad.Prompting;

public class OpenAIClient : ICompletionClient
{
    /// <remarks>Fine-tuned for Davinci model of OpenAI with a token size hard-coded at 4 chars.</remarks>
    public int TokenCapacity => 2000; // HACK: this property should be model-dependent

    readonly Model _model;

    readonly double _temperature;

    readonly OpenAIAPI _api;

    readonly GptEncoding _encoding;

    readonly int _maxTokens = 2049;

    public OpenAIClient(string apiKey, Model? model = null, double temperature = 0.0, IHttpClientFactory httpClientFactory = null)
    {
        _api = new OpenAIAPI(apiKey);
        _api.HttpClientFactory = httpClientFactory; // HINT: allow creating custom HttpClient (can be used to increase default Timeout)
        _model = model ?? Model.DavinciText;
        _temperature = temperature;
        _encoding = GptEncoding.GetEncoding("cl100k_base");
    }

    public int GetTokenCount(string content)
    {
        return _encoding.Encode(content).Count;
    }

    public string GetCompletion(string prompt)
    {
        return _api.Completions
            .CreateCompletionAsync(
            new CompletionRequest(prompt,
                model: _model,
                temperature: _temperature,
                max_tokens: _maxTokens
                )).GetAwaiter().GetResult().ToString(); // HINT: we use GetAwaiter().GetResult() to avoid async/await also better than .Result
    }

    public async Task<string> GetCompletionAsync(string prompt)
    {
        var completionResult = await _api.Completions
                        .CreateCompletionAsync(
                            new CompletionRequest(prompt,
                            model: _model,
                            temperature: _temperature,
                            max_tokens: _maxTokens
                            ));
        return completionResult.ToString();
    }
}
