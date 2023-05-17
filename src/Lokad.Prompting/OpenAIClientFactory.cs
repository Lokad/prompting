using System;
using System.Collections.Generic;
using System.Text;

namespace Lokad.Prompting;

public class OpenAIClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        return new HttpClient
        {
            // The OpenAI API does usually answers albeit with considerable delay.
            Timeout = TimeSpan.FromMinutes(5)
        };
    }
}
