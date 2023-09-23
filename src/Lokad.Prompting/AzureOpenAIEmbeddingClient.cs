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

    public float[] GetEmbedding(string content)
    {
        var tk = GetTokenCount(content);
        if (tk > TokenCapacity)
            throw new ArgumentOutOfRangeException(nameof(content));

        // '\n' sanitization suggested by the API for better performance
        var sanitized = content.Replace('\n', ' ');

        var options = new EmbeddingsOptions(sanitized);

        var response = _client.GetEmbeddings(_deployment, options);
        return response.Value.Data[0].Embedding.ToArray();
    }

    public int GetTokenCount(string content)
    {
        return _encoding.Encode(content).Count;
    }
}
