using OpenAI_API;
using OpenAI_API.Completions;
using OpenAI_API.Models;

namespace Lokad.Prompting;

public class OpenAIClient : ICompletionClient
{
    /// <remarks>Fine-tuned for Davinci model of OpenAI with a token size hard-coded at 4 chars.</remarks>
    public int TokenCapacity => 1500; // HACK: this property should be model-dependent

    readonly Model _model;

    readonly double _temperature;

    readonly OpenAIAPI _api;

    public OpenAIClient(string apiKey, Model? model = null, double temperature = 0.1)
    {
        _api = new OpenAIAPI(apiKey);
        _model = model ?? Model.DavinciText;
        _temperature = temperature; 
    }

    public string GetCompletion(string prompt)
    {
        return _api.Completions            
            .CreateCompletionAsync(
            new CompletionRequest(prompt,
                model: _model,
                temperature: _temperature,
                max_tokens: 2049 /* HACK: this should not be hard-coded */)).Result.ToString();
    }
}
