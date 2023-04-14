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
/// Continue the following translating from English to French.
/// ### ENGLISH INPUT ###
/// {{input}}
/// ### FRENCH OUTPUT ###
/// {{output}}
/// </remarks>
public class Transducer
{
    public const string InputTag = "{{input}}";
    public const string OutputTag = "{{output}}";

    const float CharPerToken = 4f; // guesstimation

    float InputOverlapRatio = 0.5f;
    float OutputBackoffRatio = 0.15f;

    ICompletionClient _client;

    public Transducer(ICompletionClient client)
    {
        _client = client;
    }

    public string Do(string prompt, string content)
    {
        if (prompt == null || !prompt.Contains(InputTag) || !prompt.Contains(OutputTag))
            throw new ArgumentException("Invalid prompt");

        var promptTokenCount = _client.GetTokenCount(
            prompt.Replace(InputTag, string.Empty).Replace(OutputTag, string.Empty));

        // hack: the 1/3 factor is heuristic (1/2 is too high)
        var residualTokenCapacity = (_client.TokenCapacity - promptTokenCount) / 3;

        var optimisticInputSize = (int)(residualTokenCapacity * CharPerToken);

        var outputTail = string.Empty;

        var inputs = new List<string>(); // for troubleshooting, perf hit is inconsequential
        var outputs = new List<string>();

        for (var i = 0; ;)
        {
            // exponential shrinking of the next input (shrinking from the right)
            var inputSize = optimisticInputSize;
            string input;
            do
            {
                input = content[i..Math.Min(i + inputSize, content.Length)];
                inputSize = (int)(inputSize * 0.9); // exponention shrinking
            } while (_client.GetTokenCount(input) > residualTokenCapacity);

            inputs.Add(input);

            var query = prompt.Replace(InputTag, input).Replace(OutputTag, outputTail);
            var output = _client.GetCompletion(query);

            if(i + input.Length < content.Length)
            {
                // truncate output, to faciliate later reconciliation
                output = output[0..^((int)(output.Length * OutputBackoffRatio))];
                outputs.Add(output);
                outputTail += output;

                // exponential shrinking of the next output tail (shrinking from the left)
                var outputTailSize = outputTail.Length;
                while(_client.GetTokenCount(outputTail) > residualTokenCapacity)
                {
                    outputTail = outputTail[(outputTail.Length - outputTailSize)..];
                    outputTailSize = (int)(outputTailSize * 0.9);
                }
            }
            else
            {
                outputs.Add(output);
                break;
            }

            // ensure overlap between successive inputs
            i += (int)(input.Length / 0.9 * (1 - InputOverlapRatio));
        }

        return string.Join("", outputs);
    }
}