namespace Lokad.Prompting.Tests;

/// <summary> Mock implementation that echoes the input. </summary>
public class EchoClient : ICompletionClient
{
    public int TokenCapacity => 14;

    public static string Separator => "###";

    public string GetCompletion(string input)
    {
        var s = input.Split(Separator);
        //var instruction = s[0];
        var content = s[1];
        var prevOutput = s[2];

        if (prevOutput.Length == 0) return content;

        var output = content[(content.IndexOf(prevOutput[^3 ..]) + 3) ..];

        return output;
    }
}