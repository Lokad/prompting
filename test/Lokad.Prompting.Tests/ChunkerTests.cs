﻿using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using Xunit;

namespace Lokad.Prompting.Tests;

public class ChunkerTests
{
    private readonly IConfiguration _config;
    public ChunkerTests()
    {
        _config = new ConfigurationBuilder()
            .AddUserSecrets<ChunkerTests>()
            .Build();
    }


    [Fact]
    public void Echo()
    {
        var chunker = new Chunker(new EchoClient());

        string content = "The quick brown fox jumps over the lazy dog.";

        var output = chunker.Do(instruction: "Do the echo", EchoClient.Separator, content: content);

        Assert.Equal(content, output); // plain echo
    }

    [Fact]
    public void OpenAI()
    {
        var apiKey = _config["OpenAIKey"];
        var chunker = new Chunker(new OpenAIClient(apiKey));

        string content = "0 1 2 3 4 ";

        var output = chunker.Do(instruction: "Add the missing digit", "###", content: content);

        Assert.Equal("5", output);
    }
}
