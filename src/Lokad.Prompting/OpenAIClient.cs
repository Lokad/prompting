using OpenAI_API;

namespace Lokad.Prompting;

internal class OpenAIClient : ICompletionClient
{
    public int TokenCapacity => 2000;

    OpenAIAPI _api;

    public OpenAIClient(string apiKey)
    {
        _api = new OpenAIAPI(apiKey);
    }

    public string GetCompletion(string prompt)
    {
        return _api.Completions.GetCompletion(prompt).Result;
    }
}
