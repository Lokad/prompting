using Azure;
using Azure.AI.OpenAI;
using OpenAI;
using OpenAI.Assistants;
using OpenAI.Chat;
using SharpToken;
using System.ClientModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Lokad.Prompting;

// TODO: implement the 'live' support when functions are provided.

public class AzureOpenAICompletionClient : ICompletionClient
{
    private readonly OpenAIClient _client;

    private readonly string _deployment;

    private readonly int _tokenCapacity;

    private GptEncoding _encoding;

    public int TokenCapacity => _tokenCapacity;

    private readonly Action<string> _live;

    public string SystemPrompt { get; set; }

    public IReadOnlyList<FunDef> Functions { get; set; }

    public AzureOpenAICompletionClient(OpenAIClient client, string deployment, int tokenCapacity, Action<string>? live = null)
        : this(client, deployment, tokenCapacity, Encodings.DefaultEncoding, live)
    {
    }

    /// <remarks> The 'live' is used to get streamed outputs from the LLM. </remarks>
    public AzureOpenAICompletionClient(OpenAIClient client, string deployment, int tokenCapacity, GptEncoding encoding, Action<string>? live = null)
    {
        _client = client;
        _deployment = deployment;
        _tokenCapacity = tokenCapacity;
        _encoding = encoding;
        _live = live ?? new Action<string>(_ => { });
        Functions = Array.Empty<FunDef>();
    }

    public static AzureOpenAICompletionClient FromOpenAI(string apiKey, string model, int tokenCapacity, Action<string>? live = null)
    {
        var client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions() { RetryPolicy = new RateLimitingPolicy() });
        return new AzureOpenAICompletionClient(client, model /* model used as deployment */, tokenCapacity, live);
    }

    public string GetCompletion(string prompt, CancellationToken cancel = default)
    {

        return GetCompletion(prompt, Array.Empty<string>(), out var _, cancel);
    }

    public string GetCompletion(string prompt, IReadOnlyList<string> stopSequences, out bool isStopped, CancellationToken cancel = default)
    {
        int retriesLeft = 5;

    Retry:

        try
        {
            return GetCompletionInternal(prompt, stopSequences, out isStopped, cancel);
        }
        // HACK: The 'IOException' aren't properly caught by the Azure OpenAI SDK, despite having its own retry policy.
        catch (IOException ex) when ((ex.Message ?? "").Contains("Unable to read data from the transport connection"))
        {
            retriesLeft--;

            Thread.Sleep(1000);

            if (retriesLeft >= 0)
                goto Retry;

            throw;
        }
    }

    private string GetCompletionInternal(string prompt, IReadOnlyList<string> stopSequences, out bool isStopped, CancellationToken cancel = default)
    {
        var chatClient = _client.GetChatClient(_deployment);

        var messages = new List<ChatMessage>();

        ChatCompletionOptions completionOptions = new()
        {
            Temperature = 0
        };

        foreach (var stop in stopSequences)
            completionOptions.StopSequences.Add(stop);

        if (!string.IsNullOrWhiteSpace(SystemPrompt))
        {
            messages.Add(new SystemChatMessage(SystemPrompt));
        }

        messages.Add(new UserChatMessage(prompt));

        var completionBuilder = new StringBuilder();

        if (Functions.Count > 0)
        {
            var finishReason = ProcessWithFunctionsAsync(chatClient, messages, completionOptions, completionBuilder, cancel)
                .GetAwaiter().GetResult();

            isStopped = finishReason == ChatFinishReason.Stop;

            return completionBuilder.ToString();
        }

        try
        {
            var response = chatClient.CompleteChatStreamingAsync(messages, completionOptions);

            var finishReason = ProcessAsync(response, completionBuilder, cancel).GetAwaiter().GetResult();

            if (finishReason == ChatFinishReason.ContentFilter)
                throw new ContentFilteredException("Azure Content Filter", completionBuilder.ToString());

            isStopped = finishReason == ChatFinishReason.Stop;

            return completionBuilder.ToString();
        }
        catch (RequestFailedException ex)
            when (ex.Message.Contains("The response was filtered due to the prompt triggering Azure OpenAI’s content management policy."))
        {
            throw new ContentFilteredException("Azure Content Filter", completionBuilder.ToString(), ex);
        }
        catch (RequestFailedException ex)
            when (ex.ErrorCode == "context_length_exceeded")
        {
            throw new ContentLengthExceededException(ex.Message.Split('\n').First(), completionBuilder.ToString(), ex);
        }
        catch (RequestFailedException ex) // For OpenAI only
            when (ex.Message != null && ex.Message.Contains("maximum context length is"))
        {
            throw new ContentLengthExceededException(ex.Message.Split('\n').First(), completionBuilder.ToString(), ex);
        }
    }

    private async Task<ChatFinishReason?> ProcessAsync(
        AsyncCollectionResult<StreamingChatCompletionUpdate> response,
        StringBuilder completionBuilder,
        CancellationToken cancel)
    {
        ChatFinishReason? finishReason = null;

        await foreach (StreamingChatCompletionUpdate c in response)
        {
            if (cancel.IsCancellationRequested)
                throw new TaskCanceledException();

            foreach (ChatMessageContentPart contentPart in c.ContentUpdate)
            {
                _live(contentPart.Text);
                completionBuilder.Append(contentPart.Text);
            }

            finishReason = c.FinishReason;
        }

        return finishReason;
    }

    private async Task<ChatFinishReason?> ProcessWithFunctionsAsync(
        ChatClient chatClient,
        List<ChatMessage> messages,
        ChatCompletionOptions completionOptions,
        StringBuilder completionBuilder,
        CancellationToken cancel)
    {
        foreach (var fdef in Functions)
        {
            var fd = ChatTool.CreateFunctionTool(
                fdef.Name,
                fdef.Description,
                BinaryData.FromObjectAsJson(
                new
                {
                    Type = "object",
                    Properties = fdef.Parameters.ToDictionary(
                        p => p.Name,
                        p => new
                        {
                            Type = p.ParamType switch
                            {
                                ParameterType.Boolean => "boolean",
                                ParameterType.Number => "number",
                                ParameterType.String => "string",
                            },
                            p.Description
                        }),
                    Required = fdef.Parameters.Where(p => !p.Optional).Select(p => p.Name),
                },
                new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            );

            completionOptions.Tools.Add(fd);
        }
    
        try
        {
            bool requiresAction;

            ChatFinishReason? finishReason = null;

            do
            {
                requiresAction = false;
                StreamingChatToolCallsBuilder toolCallsBuilder = new();

                if (cancel.IsCancellationRequested)
                    throw new TaskCanceledException();

                // If there is an error 400 here due to 'functions'
                // update the deployment to have model version '0613' or later.
                var response = chatClient.CompleteChatStreamingAsync(messages, completionOptions);

                await foreach (StreamingChatCompletionUpdate completionUpdate in response)
                {
                    foreach (ChatMessageContentPart contentPart in completionUpdate.ContentUpdate)
                    {
                        completionBuilder.Append(contentPart.Text);
                    }

                    foreach (StreamingChatToolCallUpdate toolCallUpdate in completionUpdate.ToolCallUpdates)
                    {
                        toolCallsBuilder.Append(toolCallUpdate);
                    }

                    finishReason = completionUpdate.FinishReason;

                    switch (completionUpdate.FinishReason)
                    {
                        case ChatFinishReason.Stop:
                            {
                                // Add the assistant message to the conversation history.
                                messages.Add(new AssistantChatMessage(completionBuilder.ToString()));
                                break;
                            }

                        case ChatFinishReason.ToolCalls:
                            {
                                // First, collect the accumulated function arguments into complete tool calls to be processed
                                IReadOnlyList<ChatToolCall> toolCalls = toolCallsBuilder.Build();

                                // Next, add the assistant message with tool calls to the conversation history.
                                AssistantChatMessage assistantMessage = new(toolCalls);

                                if (completionBuilder.Length > 0)
                                {
                                    assistantMessage.Content.Add(ChatMessageContentPart
                                        .CreateTextPart(completionBuilder.ToString()));
                                }

                                messages.Add(assistantMessage);

                                // Then, add a new tool message for each tool call to be resolved.
                                foreach (ChatToolCall toolCall in toolCalls)
                                {
                                    using JsonDocument argumentsJson = JsonDocument.Parse(toolCall.FunctionArguments);

                                    var fun = Functions.First(f => f.Name == toolCall.FunctionName);

                                    var toolResult = fun.Evaluator(argumentsJson);

                                    messages.Add(new ToolChatMessage(toolCall.Id,
                                        JsonSerializer.Serialize(toolResult,
                                                new JsonSerializerOptions()
                                                    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
                                                )
                                            )
                                        );
                                }

                                requiresAction = true;

                                break;
                            }

                        case ChatFinishReason.Length:
                            throw new NotImplementedException("Incomplete model output due to MaxTokens parameter or token limit exceeded.");

                        case ChatFinishReason.ContentFilter:
                            throw new ContentFilteredException("Azure Content Filter", completionBuilder.ToString());

                        case ChatFinishReason.FunctionCall:
                            throw new NotImplementedException("Deprecated in favor of tool calls.");

                        case null:
                            break;
                    }
                }
            } while (requiresAction);

            return finishReason;
        }
        catch (RequestFailedException ex)
            when (ex.Message.Contains("The response was filtered due to the prompt triggering Azure OpenAI’s content management policy."))
        {
            throw new ContentFilteredException("Azure Content Filter", completionBuilder.ToString(), ex);
        }
        catch (RequestFailedException ex)
            when (ex.ErrorCode == "context_length_exceeded")
        {
            throw new ContentLengthExceededException(ex.Message.Split('\n').First(), completionBuilder.ToString(), ex);
        }
        catch (RequestFailedException ex) // For OpenAI only
            when (ex.Message != null && ex.Message.Contains("maximum context length is"))
        {
            throw new ContentLengthExceededException(ex.Message.Split('\n').First(), completionBuilder.ToString(), ex);
        }
    }

    public int GetTokenCount(string content)
    {
        return _encoding.Encode(content).Count;
    }
}
