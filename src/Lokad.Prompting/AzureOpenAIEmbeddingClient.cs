using Azure.AI.OpenAI;
using SharpToken;

namespace Lokad.Prompting;

public class AzureOpenAIEmbeddingClient : IEmbeddingClient
{
    private readonly OpenAIClient _client;

    private readonly string _deployment;

    private readonly int _tokenCapacity;

    private GptEncoding _encoding;

    public int TokenCapacity => _tokenCapacity;

    public AzureOpenAIEmbeddingClient(OpenAIClient client, string deployment, int tokenCapacity)
         : this(client, deployment, tokenCapacity, Encodings.DefaultEncoding)
    {

    }

    public AzureOpenAIEmbeddingClient(OpenAIClient client, string deployment, int tokenCapacity, GptEncoding encoding)
    {
        _client = client;
        _deployment = deployment;
        _tokenCapacity = tokenCapacity;
        _encoding = encoding;
    }

    public float[] GetEmbedding(string input)
    {
        var tk = GetTokenCount(input);
        if (tk > TokenCapacity)
            throw new ArgumentOutOfRangeException(nameof(input));

        // '\n' sanitization suggested by the API for better performance
        var sanitized = input.Replace('\n', ' ');

        var options = new EmbeddingsOptions(_deployment, new[] { sanitized });

        var response = _client.GetEmbeddings(options);
        return response.Value.Data[0].Embedding.ToArray();
    }

    public float[][] GetEmbeddings(IReadOnlyList<string> inputs)
    {
        if (inputs == null)
            throw new ArgumentNullException(nameof(inputs));

        foreach(var input in inputs)
        {
            var tk = GetTokenCount(input);
            if (tk > TokenCapacity)
                throw new ArgumentOutOfRangeException(nameof(input));
        }

        // '\n' sanitization suggested by the API for better performance
        var options = new EmbeddingsOptions(_deployment, inputs.Select(input => input.Replace('\n', ' ')));

        var response = _client.GetEmbeddings(options);
        return response.Value.Data.Select( d => d.Embedding.ToArray()).ToArray();
    }

    public int GetTokenCount(string input)
    {
        return _encoding.Encode(input).Count;
    }
}
