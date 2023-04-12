using Xunit;

namespace Lokad.Prompting.Tests;

public class ChunkerTests
{
    [Fact]
    public void Echo()
    {
        var chunker = new Chunker(new EchoClient());

        string content = "The quick brown fox jumps over the lazy dog.";

        var output = chunker.Do(instruction: "Do the echo", EchoClient.Separator, content: content);

        Assert.Equal(output, content);
    }

    [Fact]
    public void OpenAI()
    {

    }
}
