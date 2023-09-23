namespace Lokad.Prompting;

/// <summary> Intended for prompt engineering purposes. </summary>
/// <remarks> 'LogPrompt' is always called first, followed by 
/// 'LogCompletion' if a completion is ever obtained. </remarks>
public interface ICompletionLogger
{
    public void LogPrompt(string prompt);
    public void LogCompletion(string completion);
}
