namespace Lokad.Prompting;

public interface ICompletionClient
{
    int TokenCapacity { get; }

    int GetTokenCount(string content);

    IReadOnlyList<FunDef> Functions { get; set; }

    string GetCompletion(string prompt, CancellationToken cancel = default);

    string GetCompletion(string prompt,
        IReadOnlyList<string> stopSequences, 
        out bool isStopped,
        CancellationToken cancel = default);
}
