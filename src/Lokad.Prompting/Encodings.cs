using SharpToken;

namespace Lokad.Prompting;

public static class Encodings
{
    /// <summary> The default encoding for OpenAI models. </summary>
    /// <remarks>
    /// The 'cl100k_base' is used for GPT-3.5, GPT-$ models.
    /// 
    /// Beware, calling 'GptEncoding.GetEncoding' is slow, like 100ms.
    /// </remarks>
    public static GptEncoding DefaultEncoding = GptEncoding.GetEncoding("cl100k_base");
}
