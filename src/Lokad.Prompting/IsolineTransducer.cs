namespace Lokad.Prompting;

// IDEA: recursively call the non-isoline transducer here (in case of very big line)

/// <summary> 
/// Apply a line-isomorphic tranformation process in a streamed 
/// manner. Intended for long line-sensitive documents (like 
/// properly delinated Markdown files) that exceed token limits, 
/// for operations like translation, typo/grammar fixing, format 
/// conversions, etc.
/// </summary>
/// <remarks>
/// The gist of the transducer strategy consists of 
/// - chunking line-wise, 
/// - decorating the input with line number prefixes
/// - stepping back both input and output at each iteration
/// 
/// This ensures a high quality continuation from one segment
/// to the next.
/// 
/// PROMPT TEMPLATE:
/// 
/// Continue the following translating from English to French.
/// ### ENGLISH INPUT ###
/// {{input}}
/// ### FRENCH OUTPUT ###
/// {{output}}
/// </remarks>
public class IsolineTransducer
{
    public const string InputTag = "{{input}}";
    public const string OutputTag = "{{output}}";

    ICompletionClient _client;

    public IsolineTransducer(ICompletionClient client)
    {
        _client = client;
    }

    public string Do(string prompt, string content)
    {
        if (prompt == null || !prompt.Contains(InputTag) || !prompt.Contains(OutputTag))
            throw new ArgumentException("Invalid prompt");

        prompt = prompt.Replace("\r\n", "\n"); // Always use Unix LR
        content = content.Replace("\r\n", "\n"); 

        var promptTokenCount = _client.GetTokenCount(
            prompt.Replace(InputTag, string.Empty).Replace(OutputTag, string.Empty));

        var inputTokenCapacity = (_client.TokenCapacity - promptTokenCount) * 3 / 5; // heuristics to not exceed capacity
        var outputTokenCapacity = (_client.TokenCapacity - promptTokenCount) * 1 / 5;

        var inputLines = content.Split('\n', StringSplitOptions.None)
            .Reverse().SkipWhile(string.IsNullOrWhiteSpace).Reverse() // remove the blank lines at the end of any
            .ToArray();
        var inputTokenCounts = inputLines.Select(_client.GetTokenCount).ToArray();

        if (inputLines.Length == 0) // degenerate content
            return string.Empty;

        if (inputTokenCounts.Max() > inputTokenCapacity)
            throw new ArgumentOutOfRangeException(nameof(content));

        var outputLines = new Dictionary<int, string>();
        var outputTokenCounts = new Dictionary<int, int>();

        // i = start line of input, j = end line of input
        // k = start line of tail
        for (int i = 0, k = 0; ;)
        {
            // Maximize input size under token capacity
            var inputTokenCount = 0;
            int j;
            for (j = i; j < inputLines.Length; j++)
            {
                if (inputTokenCount + inputTokenCounts[j] + 4 < inputTokenCapacity)
                {
                    // +4 tokens for 'L123' prefix, plus line return
                    inputTokenCount += inputTokenCounts[j] + 4;
                }
                else break;
            }
            // Back-track until 'j' is not a blank line because it confuses GPT
            while (string.IsNullOrWhiteSpace(inputLines[j - 1])) j--;

            // Range of input lines, [i .. j]
            var input = string.Empty;
            for (var n = i; n < j; n++)
                input += $"L{n} " + inputLines[n] + '\n'; // Always use Unix LR

            // Range of output line [k ..]
            var outputTail = string.Empty;
            for (var n = k; n < outputLines.Count; n++)
                outputTail += $"L{n} " + outputLines[n] + '\n'; // Always use Unix LR

            var bait = $"L{outputLines.Count}";
            outputTail += bait;

            var query = prompt.Replace(InputTag, input).Replace(OutputTag, outputTail);
            var output = bait + _client.GetCompletion(query);

            var newLines = output.Split('\n').ToArray();
            foreach (var newLine in newLines)
            {
                if (!newLine.StartsWith("L"))
                    throw new InvalidOperationException("Bogus line.");

                // prune the line prefix
                var lineNumberStr = newLine.Substring(1, newLine.IndexOf(" "));
                var cleanLine = newLine.Substring(newLine.IndexOf(" ") + 1);

                if (!int.TryParse(lineNumberStr, out var lineNumber))
                    throw new InvalidOperationException("Bogus line number.");

                outputLines.Add(lineNumber, cleanLine);
                outputTokenCounts.Add(lineNumber, _client.GetTokenCount(cleanLine));
            }

            if (outputLines.Count >= inputLines.Length) // we are done
                break;

            // Maximize tail size under token capacity
            var tailTokenCount = 0;
            for (k = outputLines.Keys.Max(); k >= 0 && k > i; k--)
            {
                if (tailTokenCount + outputTokenCounts[k] + 4 < outputTokenCapacity)
                {
                    tailTokenCount += outputTokenCounts[k] + 4;
                }
                else break;
            }

            // Move forward the input
            i = k;
        }

        return string.Join(Environment.NewLine, 
            Enumerable.Range(0, outputLines.Count).Select(i => outputLines[i]));
    }
}
