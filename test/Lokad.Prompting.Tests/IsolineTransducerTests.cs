using Microsoft.Extensions.Configuration;
using Xunit;

namespace Lokad.Prompting.Tests;

public class IsolineTransducerTests
{
    private readonly IConfiguration _config;
    public IsolineTransducerTests()
    {
        _config = new ConfigurationBuilder()
            .AddUserSecrets<TransducerTests>()
            .Build();
    }

    [Fact] // [Fact(Skip = "5min to run")]
    public void TranslateLongHugoPage()
    {
        // Run from /test/Lokad.Prompting.Tests/bin/Debug/net7.0
        var content = File.ReadAllText("../../../../../sample/transducer/long-webpage.md");

        var prompt =
"""
Continue the following translation from English to French.
The output may not be starting at the same place than the input.
Preserve TOML front matter, don't touch the '---' delimiters.
Do not translate the keys in the TOML header.
Preserve all the Markdown syntax. 
Do not skip images, such as `![Blah blah](/my-image.jpg)`.
Do not touch filenames (ex: `/my-image.jpg`).
Keep prefix line numbers untouched (ex: L123).
Keep blank lines untouched.
Keep line breaks untouched. 
Don't introduce extra line breaks, don't remove them either.

!=!=! ENGLISH INPUT !=!=!
{{input}}

!=!=! FRENCH OUTPUT !=!=!
{{output}}
""";

        var apiKey = _config["OpenAIKey"];
        var transducer = new IsolineTransducer(new OpenAIClient(apiKey));

        var output = transducer.Do(prompt, content);

        File.WriteAllText("../../../../../sample/transducer/long-webpage-output.md", output);
    }
}
