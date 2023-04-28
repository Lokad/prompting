namespace Lokad.Prompting;

public interface ICompletionClient
{
    int TokenCapacity { get; }

    int GetTokenCount(string content);

    string GetCompletion(string prompt);
    Task<string> GetCompletionAsync(string prompt);
}
