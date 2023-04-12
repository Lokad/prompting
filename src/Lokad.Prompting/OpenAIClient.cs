using OpenAI_API;
using OpenAI_API.Completions;

namespace Lokad.Prompting;

public class OpenAIClient : ICompletionClient
{
    public int TokenCapacity => 2000;

    OpenAIAPI _api;

    public OpenAIClient(string apiKey)
    {
        _api = new OpenAIAPI(apiKey);
    }

    public string GetCompletion(string prompt)
    {
        return _api.Completions            
            .CreateCompletionAsync(
            new CompletionRequest(prompt,
                model: OpenAI_API.Models.Model.DavinciText,
                temperature: 0.1)).Result.ToString();
    }
}
