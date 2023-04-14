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
    public void TrivialTransduce()
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

    [Fact(Skip = "2min to run")]
    public void TranslateLongWebpage()
    {
        // Run from /test/Lokad.Prompting.Tests/bin/Debug/net7.0
        var content = File.ReadAllText("../../../../../sample/transducer/long-webpage.md");

        var prompt =
"""
Continue the following translation from English to French.
The output may not be starting at the same place than the input.
Preserve TOML front matter, don't touch the '---' delimiters.
Preserve all the Markdown syntax.
Do not touch filenames (ex: images).
Continue until you reach the line '!=!=! FRENCH OUTPUT !=!=!' 
(but don't include it in your translation).

!=!=! ENGLISH INPUT !=!=!
{{input}}

!=!=! FRENCH OUTPUT !=!=!
{{output}}
""";

        var apiKey = _config["OpenAIKey"];
        var transducer = new Transducer(new OpenAIClient(apiKey));

        var output = transducer.Do(prompt, content);

        File.WriteAllText("../../../../../sample/transducer/long-webpage-output.md", output);
    }

    [Fact(Skip = "3min to run")]
    public void MarkdownifyLongEmail()
    {
        // Run from /test/Lokad.Prompting.Tests/bin/Debug/net7.0
        var content = File.ReadAllText("../../../../../sample/transducer/long-email.html");

        var prompt =
"""
Continue the following conversion from HTML to Markdown.
The output may not be starting at the same place than the input.
For images use the markdown syntax `![]()` but preserve the exact
file path as found in the original HTML.
============== RAW EMAIL HTML INPUT ==============
{{input}}
============== EMAIL MARKDOWN OUTPUT ==============
{{output}}
""";

        var apiKey = _config["OpenAIKey"];
        var transducer = new Transducer(new OpenAIClient(apiKey));

        var output = transducer.Do(prompt, content);

        File.WriteAllText("../../../../../sample/transducer/long-email-output.md", output);
    }

    [Fact(Skip = "30min to run")]
    public void AnonymizeLongEmail()
    {
        // Run from /test/Lokad.Prompting.Tests/bin/Debug/net7.0
        var content = File.ReadAllText("../../../../../sample/transducer/long-email.html");

        var prompt =
"""
Continue the following conversion from HTML to anonymized HTML.
The output may not be starting at the same place than the input.
Replace any person name, company name, email address by anonymous
equivalent. 
For example, replace 'John Doe <john.doe@contoso.com>' by 'Person 1 <person1@company1.com>'.
Idem, replace '+33 3 72 73 34 93' with '+1 123 456 789'.
Idem, replace '80 avenue des Champs Elyses' with '123 street'.
Idem, replace 'Paris, France' with 'City 1, Country 1'.
Idem, replace 'Contoso Limited' with 'Company 1 Inc'
Stay consistent with your replacements. Any expression
that hint the personal or corporate identity of the original authors 
should be likewise anonymized by those replacements. 
Keep HTML markup unchanged, again except for elements that give away
the identify of the persons or companies.
============== RAW EMAIL HTML INPUT ==============
{{input}}
============= ANONYMIZED EMAIL OUTPUT ============
{{output}}
""";

        var apiKey = _config["OpenAIKey"];
        var transducer = new Transducer(new OpenAIClient(apiKey));

        var output = transducer.Do(prompt, content);

        File.WriteAllText("../../../../../sample/transducer/long-email-anonymized.html", output);
    }

    [Fact(Skip = "4min to run")]
    public void CleanupLongAudioTranscript()
    {
        // Run from /test/Lokad.Prompting.Tests/bin/Debug/net7.0
        var content = string.Join(Environment.NewLine,
            File.ReadLines("../../../../../sample/transducer/long-audio-transcript.vtt")
                .Where((line, index) => index % 3 == 0)
                .Skip(1)
                .Select(line => line.Replace("</v>", string.Empty)));

        var prompt =
"""
Continue the following conversion from .VTT to Markdown.
The output may not be starting at the same place than the input.
The audio transcript quality of the .VTT file is poor. 
Produce a higher quality edited version.
Remove oral hesitations.
Reduce the chitchat and neduce the number of transitions between people.
Rephrase "oral" segment in the way they would be written instead.
Make the back-and-forth replies bigger than they were.
============== RAW .VTT INPUT ==============
{{input}}
============== MARKDOWN OUTPUT ==============
{{output}}
""";

        var apiKey = _config["OpenAIKey"];
        var transducer = new Transducer(new OpenAIClient(apiKey));

        var output = transducer.Do(prompt, content);

        File.WriteAllText("../../../../../sample/transducer/long-audio-transcript-output.md", output);
    }
}
