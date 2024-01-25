using Xunit;
namespace Lokad.Prompting.Tests;

public class NonAzureOpenAI
{
    [Fact(Skip = "No config setup")]
    public void SmokeTest()
    {
        var model = "gpt-3.5-turbo";
        var apiKey = "sk-..";
        var c = AzureOpenAICompletionClient.FromOpenAI(apiKey, model, 4096);

        var r = c.GetCompletion("compose a haiku");

        Assert.NotNull(r);
        Assert.True(r.Length > 10);
    }
}
