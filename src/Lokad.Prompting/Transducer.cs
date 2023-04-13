using System.Text;

namespace Lokad.Prompting;

/// <summary> 
/// Apply a general tranformation process in a streamed manner.
/// Intended for long documents that exceed token limits, for
/// operations like translation, typo/grammar fixing, format 
/// conversions, etc.
/// </summary>
/// <remarks>
/// The gist of the transducer strategy consists of chunking,
/// but stepping back both input and output at each iteration
/// in order to let GPT clean continuations between one chunk
/// and the next.
/// 
/// PROMPT TEMPLATE:
/// 
/// Continue the following translatin from English to French.
/// ### ENGLISH INPUT ###
/// {{input}}
/// ### FRENCH OUTPUT ###
/// {{output}}
/// </remarks>
public class Transducer
{
    public const string InputTag = "{{input}}";
    public const string OutputTag = "{{output}}";

    float OverlapRatio = 0.4f;
    float CharPerToken = 4f; // HACK: no port of 'tiktoken' package for C# yet

    ICompletionClient _client;
    int _tokenCapacity;

    public Transducer(ICompletionClient client, int? tokenCapacity = null)
    {
        _client = client;
        _tokenCapacity = tokenCapacity ?? _client.TokenCapacity;
    }

    public string Do(string prompt, string content)
    {
        if (prompt == null || !prompt.Contains(InputTag) || !prompt.Contains(OutputTag))
            throw new ArgumentException("Invalid prompt");

        var promptTokenCount = (int)((prompt.Length - InputTag.Length - OutputTag.Length) / CharPerToken);

        var residualTokenCapacity = (_tokenCapacity - promptTokenCount) / 2;

        var inputStep = (int)(residualTokenCapacity * (1 - OverlapRatio) * CharPerToken);
        var inputSize = (int)(residualTokenCapacity * CharPerToken);

        var outputTail = string.Empty;

        var builder = new StringBuilder();

        for (var i = 0; ; i += inputStep)
        {
            var input = prompt
                .Replace(InputTag, content[i..Math.Min(i + inputSize, content.Length)])
                .Replace(OutputTag, outputTail);

            outputTail = _client.GetCompletion(input);

            if(i + inputSize < content.Length)
            {
                // truncate output, except for last chunck
                outputTail = outputTail[0..^((int)(outputTail.Length * OverlapRatio))];
                builder.Append(outputTail);
            }
            else
            {
                builder.Append(outputTail);
                break;
            }
        }

        return builder.ToString();
    }
}