using Azure;
using Azure.AI.OpenAI;
using SharpToken;
using System.Text;
using System.Text.Json;

namespace Lokad.Prompting;

public class AzureOpenAICompletionClient : ICompletionClient
{
    private readonly OpenAIClient _client;

    private readonly string _deployment;

    private readonly int _tokenCapacity;

    private GptEncoding _encoding;

    public int TokenCapacity => _tokenCapacity;

    private readonly Action<string> _live;

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

    public string GetCompletion(string prompt, CancellationToken cancel = default)
    {
        return GetCompletion(prompt, Array.Empty<string>(), out var _, cancel);
    }

    public string GetCompletion(string prompt, IReadOnlyList<string> stopSequences, out bool isStopped, CancellationToken cancel = default)
    {
        var completionOptions = new ChatCompletionsOptions()
        {
            Temperature = 0,
            DeploymentName = _deployment,
        };

        foreach(var stop in stopSequences)
            completionOptions.StopSequences.Add(stop);

        completionOptions.Messages.Add(new ChatRequestUserMessage(prompt));

        var completionBuilder = new StringBuilder();

        if (Functions.Count > 0)
        {
            var finishReason = ProcessWithFunctionsAsync(completionOptions, completionBuilder, cancel)
                .GetAwaiter().GetResult();

            isStopped = finishReason == CompletionsFinishReason.Stopped;

            return completionBuilder.ToString();
        }

        try
        {
            var response = _client.GetChatCompletionsStreaming(completionOptions);

            var finishReason = ProcessAsync(response, completionBuilder, cancel).GetAwaiter().GetResult();

            if (finishReason == CompletionsFinishReason.ContentFiltered)
                throw new ContentFilteredException("Azure Content Filter", completionBuilder.ToString());

            isStopped = finishReason == CompletionsFinishReason.Stopped;

            return completionBuilder.ToString();
        }
        catch (RequestFailedException ex) 
            when (ex.Message.Contains("The response was filtered due to the prompt triggering Azure OpenAI’s content management policy."))
        {
            throw new ContentFilteredException("Azure Content Filter", completionBuilder.ToString(), ex);
        }
    }

    private async Task<CompletionsFinishReason?> ProcessAsync(
        StreamingResponse<StreamingChatCompletionsUpdate> response, 
        StringBuilder completionBuilder,
        CancellationToken cancel)
    {
        CompletionsFinishReason? finishReason = null;

        await foreach (StreamingChatCompletionsUpdate c in response)
        {
            if (cancel.IsCancellationRequested)
                throw new TaskCanceledException();

            _live(c.ContentUpdate);
            completionBuilder.Append(c.ContentUpdate);

            finishReason = c.FinishReason;
        }

        return finishReason;
    }

    private async Task<CompletionsFinishReason?> ProcessWithFunctionsAsync(
        ChatCompletionsOptions completionOptions, 
        StringBuilder completionBuilder,
        CancellationToken cancel)
    {
        foreach (var fdef in Functions)
        {
            var fd = new FunctionDefinition()
            {
                Name = fdef.Name,
                Description = fdef.Description,
                Parameters = BinaryData.FromObjectAsJson(
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
                            Description = p.Description
                        }),
                    Required = fdef.Parameters.Where(p => !p.Optional).Select(p => p.Name),
                },
                new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            };

            completionOptions.Functions.Add(fd);
        }

        try
        {
            Response<ChatCompletions> response;
            CompletionsFinishReason? finishReason;

            ContinueAfterCall:

            if (cancel.IsCancellationRequested)
                throw new TaskCanceledException();

            // HACK: it should be possible to use a streamed version below (it wasn't possible in the early beta of the SDK)
            // StreamingResponse<StreamingChatCompletionsUpdate> response = _client.GetChatCompletionsStreaming(completionOptions);

            // If there is an error 400 here due to 'functions'
            // update the deployment to have model version '0613' or later.
            response = await _client.GetChatCompletionsAsync(completionOptions);

            var choice = response.Value.Choices[0];
            finishReason = choice.FinishReason;

            if (finishReason == CompletionsFinishReason.ContentFiltered)
                throw new ContentFilteredException("Azure Content Filter", completionBuilder.ToString());

            if (!string.IsNullOrWhiteSpace(choice.Message.Content))
                completionBuilder.Append(choice.Message.Content);

            if (finishReason == CompletionsFinishReason.FunctionCall)
            {
                completionOptions.Messages.Add(new ChatRequestAssistantMessage(choice.Message.Content)
                {
                    FunctionCall = choice.Message.FunctionCall,
                });

                var rawArgs = choice.Message.FunctionCall.Arguments;
                ChatRequestFunctionMessage funResult;
                try
                {
                    var parsedArgs = JsonDocument.Parse(rawArgs);
                    var fun = Functions.First(f => f.Name == choice.Message.FunctionCall.Name);
                    var res = fun.Evaluator(parsedArgs);

                    funResult = new ChatRequestFunctionMessage(
                        name: choice.Message.FunctionCall.Name,
                        content: JsonSerializer.Serialize(res,
                            new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

                    if (fun.IsFinal)
                    {
                        completionOptions.Messages.Add(funResult);
                        return CompletionsFinishReason.Stopped;
                    }
                }
                catch(Exception e)
                {
                    funResult = new ChatRequestFunctionMessage(
                        name: choice.Message.FunctionCall.Name,
                        content: JsonSerializer.Serialize(e.Message, 
                            new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }))
                    ;
                }

                completionOptions.Messages.Add(funResult);

                goto ContinueAfterCall;
            }
            
            return choice.FinishReason;
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
    }

    public int GetTokenCount(string content)
    {
        return _encoding.Encode(content).Count;
    }
}
