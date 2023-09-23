namespace Lokad.Prompting;

/// <summary> 
/// (July 2023) Azure OpenAI content filter can't be displayed.
/// Unfortunately, they have quite a fair share of false positive.
/// </summary>
public class ContentFilteredException : Exception
{
    public string PartialCompletion { get; }

    public ContentFilteredException(string message, string partialCompletion, Exception inner = null) 
        : base(message, inner) 
    {
        PartialCompletion = partialCompletion ?? string.Empty;
    }
}

public class ContentLengthExceededException : Exception
{
    public string PartialCompletion { get; }

    public ContentLengthExceededException(string message, string partialCompletion, Exception inner = null) 
        : base(message, inner) 
    {
        PartialCompletion = partialCompletion ?? string.Empty;
    }
}