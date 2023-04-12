namespace Lokad.Prompting;

public interface ICompletionClient
{
    int TokenCapacity { get; }

    string GetCompletion(string prompt);
}
