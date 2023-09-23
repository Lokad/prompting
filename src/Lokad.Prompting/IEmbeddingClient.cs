namespace Lokad.Prompting;

public interface IEmbeddingClient
{
    int TokenCapacity { get; }

    public int GetTokenCount(string content);

    public float[] GetEmbedding(string content);
}
