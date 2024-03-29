﻿namespace Lokad.Prompting;

public interface IEmbeddingClient
{
    int TokenCapacity { get; }

    public int GetTokenCount(string input);

    public float[] GetEmbedding(string input);

    public IReadOnlyList<float[]> GetEmbeddings(IReadOnlyList<string> inputs);
}
