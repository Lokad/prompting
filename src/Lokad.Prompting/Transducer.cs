using System.Text;

namespace Lokad.Prompting;

public class Transducer
{
    float InputOverlapRatio = 0.3f;
    float OutputOverlapRatio = 0.15f;
    float CharPerToken = 4f; // HACK: no port of 'tiktoken' package for C# yet

    ICompletionClient _client;

    public Transducer(ICompletionClient client)
    {
        _client = client;
    }

    public string Do(string instruction, string separator, string content)
    {
        // TODO: use a prompt template with {{input}} and {{output}}
        // TODO: use a single ratio (same for both) 
        var inputStep = (int)(_client.TokenCapacity/2 * (1 - InputOverlapRatio) * CharPerToken);
        var inputSize = (int)(_client.TokenCapacity/2 * CharPerToken);

        var outputTail = string.Empty;

        var builder = new StringBuilder();

        for (var i = 0; i < content.Length; i += inputStep)
        {
            var input = instruction 
                + separator + content[i..Math.Min(i + inputSize, content.Length)] // overlapping inputs
                + separator + outputTail;

            outputTail = _client.GetCompletion(input);

            if(i + inputStep < content.Length)
            {
                // truncate output, except for last chunck
                outputTail = outputTail[0..^((int)(outputTail.Length * OutputOverlapRatio))];    
            }

            builder.Append(outputTail);
        }

        return builder.ToString();
    }
}