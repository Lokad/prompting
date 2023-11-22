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
/// ===== ENGLISH INPUT =====
/// {{input}}
/// ===== FRENCH OUTPUT =====
/// {{output}}
/// </remarks>
public class IsolineTransducer
{
    /// <summary> Tokens for line number prefix, plus the line return. </summary>
    const int LineNumberTokenCount = 4;

    public const string InputTag = "{{input}}";
    public const string OutputTag = "{{output}}";

    ICompletionClient _client;
    int _tokenCapacity;

    /// <summary> Depending on the task, the capacity can have to be adjusted downward. </summary>
    public IsolineTransducer(ICompletionClient client, int? tokenCapacity =  null)
    {
        _client = client;
        _tokenCapacity = tokenCapacity ?? _client.TokenCapacity;
    }

    public string Do(string prompt, string content, ICompletionLogger log = null, CancellationToken cancel = default)
    {
        if (prompt == null || !prompt.Contains(InputTag) || !prompt.Contains(OutputTag))
            throw new ArgumentException("Invalid prompt");

        prompt = prompt.Replace("\r\n", "\n").TrimEnd(Environment.NewLine.ToCharArray()); // Always use Unix LR
        content = content.Replace("\r\n", "\n"); 

        var promptTokenCount = _client.GetTokenCount(
            prompt.Replace(InputTag, string.Empty).Replace(OutputTag, string.Empty));

        var extensionTokenCapacity = (_tokenCapacity - promptTokenCount) * 3 / 10; // heuristics to not exceed capacity
        var overlapTokenCapacity = (_tokenCapacity - promptTokenCount) * 2 / 10;

        var inputLines = content.Split('\n', StringSplitOptions.None)
            .Reverse().SkipWhile(string.IsNullOrWhiteSpace).Reverse() // remove the blank lines at the end of any
            .ToArray();
        var inputTokenCounts = inputLines.Select(_client.GetTokenCount).ToArray();

        if (inputLines.Length == 0) // degenerate content
            return string.Empty;

        if (inputTokenCounts.Max() > extensionTokenCapacity)
        {
            for(var i = 0; i < inputTokenCounts.Length; i++)
            {
                if (inputTokenCounts[i] > extensionTokenCapacity)
                    throw new InvalidDataException($"Line {i} too long ({inputTokenCounts[i]} tokens, max {extensionTokenCapacity}): {inputLines[i].Substring(0, 60)}.. Split this big line into multiple shorter ones.");
            }
        }

        var outputLines = new Dictionary<int, string>();
        var outputTokenCounts = new Dictionary<int, int>();

        do
        {
            // i = start line of input, j = end line of input (inclusive)

            var i = outputLines.Count;

            // Maximize overlap size under token capacity
            var overlapTokenCount = 0;
            while(i > 0 &&
                overlapTokenCount 
                    + inputTokenCounts[i - 1] 
                    + outputTokenCounts[i - 1] 
                    + 2 * LineNumberTokenCount
                        < overlapTokenCapacity)
            {
                overlapTokenCount +=
                    inputTokenCounts[i - 1]
                    + outputTokenCounts[i - 1]
                    + 2 * LineNumberTokenCount;

                i--;
            }

        SetInput:

            // SET THE INPUT

            var j = i - 1;

            // Maximize extension size under token capacity
            var extensionTokenCount = 0;
            while (j < inputLines.Length - 1 &&
                extensionTokenCount
                    + inputTokenCounts[j + 1]
                    + LineNumberTokenCount
                        < extensionTokenCapacity)
            {
                extensionTokenCount +=
                    inputTokenCounts[j + 1]
                    + LineNumberTokenCount;

                j++;
            }

            if (j < i)
                throw new InvalidOperationException("Line too long, can't be processed.");

            // Back-track until 'j' is not a blank line because it confuses the LLM
            while (j > 0 && string.IsNullOrWhiteSpace(inputLines[j])) j--;

            // Line shift ensures that line numbering stays as 2-digits, as LLM struggles to enumerate further.
            var lineShift = i - 1;

            // Range of input lines, [i .. j]
            var input = string.Empty;
            for (var n = i; n <= j; n++)
                input += $"L{n - lineShift} " + inputLines[n] + '\n'; // Always use Unix LR
            var stopWord = $"L{j + 1 - lineShift}"; // insert the stop word at the very end of the input
            input += stopWord;

            // SET THE OUTPUT

            // Range of output line [i .. k]
            var outputTail = string.Empty;
            for (var n = i; n < outputLines.Count; n++)
                outputTail += $"L{n - lineShift} " + outputLines[n] + '\n'; // Always use Unix LR

            // Do not include the whitespace at the end of the bait, it doesn't work due to LLM tokenization.
            var bait = $"L{outputLines.Count - lineShift}";
            outputTail += bait;

            var query = prompt.Replace(InputTag, input).Replace(OutputTag, outputTail);
            log?.LogPrompt(query);
            var completion = _client.GetCompletion(query, new[] { stopWord }, out bool isStopped, cancel);
            log?.LogCompletion(completion);

            // Due to chat wrapping, the initial whitespace is usually dropped, this is OK.
            if (!completion.StartsWith(" ")) completion = " " + completion;

            var output = bait + completion;

            // Empty entries may happen due to LLM behavior (should not).
            var newLines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToArray();

            // When the stopword isn't hit, the last line is frequently only half-processed, hence, we ditch it.
            if (!isStopped && newLines.Length > 1)
                newLines = newLines[..^1];

            var isFirstLine = true;
            foreach (var newLine in newLines)
            {
                // Forgotten line numbers may happen with LLM. We break and resume from breakage point.
                if (!newLine.StartsWith("L") || !newLine.Contains(" "))
                {
                    if (!isFirstLine)
                        goto ProcessTrailingBlanks;

                    if(i < outputLines.Count)
                    {
                        i++;
                        goto SetInput;
                    }
                    
                    throw new InvalidOperationException("Missing line number.");
                }

                // prune the line prefix
                var lineNumberStr = newLine.Substring(1, newLine.IndexOf(" "));
                var cleanLine = newLine.Substring(newLine.IndexOf(" ") + 1);

                if (!int.TryParse(lineNumberStr, out var lineNumber))
                {
                    if (!isFirstLine)
                        goto ProcessTrailingBlanks;

                    if (i < outputLines.Count)
                    {
                        i++;
                        goto SetInput;
                    }

                    throw new InvalidOperationException("Bogus line number.");
                }

                lineNumber += lineShift;

                // Duplicate line numbers may happen with LLM. We break and resume from breakage point.
                if ((lineNumber > 0 && !outputLines.ContainsKey(lineNumber - 1)) || outputLines.ContainsKey(lineNumber))
                {
                    if (!isFirstLine)
                        goto ProcessTrailingBlanks;

                    if (i < outputLines.Count)
                    {
                        i++;
                        goto SetInput;
                    }

                    throw new InvalidOperationException("Non incremental line number.");
                }

                isFirstLine = false;

                // GPT may incorrectly sometimes be off indentation wise
                var indent = inputLines[lineNumber].TakeWhile(char.IsWhiteSpace).Count();
                cleanLine = new string(' ', indent) + cleanLine.TrimStart();

                outputLines.Add(lineNumber, cleanLine);
                outputTokenCounts.Add(lineNumber, _client.GetTokenCount(cleanLine));

                // Garbage may be introduced 'after' completing the task, just ignore if done already.
                if (outputLines.Count >= inputLines.Length) // we are done
                    break;
            }

        ProcessTrailingBlanks:

            // Move forward on blank lines because it confuses GPT
            while (outputLines.Count < inputLines.Length && string.IsNullOrWhiteSpace(inputLines[outputLines.Count]))
            {
                var ln = outputLines.Count;
                outputLines.Add(ln, inputLines[ln]);
                outputTokenCounts.Add(ln, _client.GetTokenCount(string.Empty));
            }

        } while (outputLines.Count < inputLines.Length);

        return string.Join(Environment.NewLine, 
            Enumerable.Range(0, outputLines.Count).Select(i => outputLines[i]));
    }
}
