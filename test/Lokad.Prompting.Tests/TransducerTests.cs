using Microsoft.Extensions.Configuration;
using Xunit;

namespace Lokad.Prompting.Tests;

public class TransducerTests
{
    private readonly IConfiguration _config;
    public TransducerTests()
    {
        _config = new ConfigurationBuilder()
            .AddUserSecrets<TransducerTests>()
            .Build();
    }


    [Fact]
    public void Echo()
    {
        var transducer = new Transducer(new EchoClient());

        var prompt =
"""
Do the echo###{{input}}###{{output}}
""";

        string content = "The quick brown fox jumps over the lazy dog.";

        var output = transducer.Do(prompt, content);

        Assert.Equal(content, output); // plain echo
    }

    [Fact]
    public void OpenAI()
    {
        var apiKey = _config["OpenAIKey"];
        var transducer = new Transducer(new OpenAIClient(apiKey));

        var prompt =
"""
Add the missing digit
###
{{input}}
###
{{output}}
""";

        string content = "0 1 2 3 4 ";

        var output = transducer.Do(prompt, content);

        Assert.Equal("5", output);
    }
}
