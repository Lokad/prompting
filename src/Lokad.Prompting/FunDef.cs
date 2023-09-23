using System.Text.Json;

namespace Lokad.Prompting;

public enum ParameterType
{
    Boolean,
    Number,
    String,
}

public class ParamDef
{
    public string Name { get; set; }
    public string Description { get; set; }
    public ParameterType ParamType { get; set; }
    public bool Optional { get; set; }

    public ParamDef(string name, string description, ParameterType paramType = ParameterType.String, bool optional = false)
    {
        Name = name;
        Description = description;
        ParamType = paramType;
        Optional = optional;
    }
}

/// <summary> Function definitions to be passed to the LLM. </summary>
/// <remarks> 
/// See also https://learn.microsoft.com/en-us/azure/ai-services/openai/how-to/function-calling
/// </remarks>
public class FunDef
{
    public string Name { get; }
    public string Description { get; }
    public IReadOnlyList<ParamDef> Parameters { get; }
    public Func<JsonDocument, string> Evaluator { get; }

    /// <summary> 
    /// The chat must be interrupted client-side after this function is called.
    /// Intended for functions that submit results (hence, no point in continuing any further).
    /// </summary>
    public bool IsFinal { get; set; }

    public FunDef(string name, 
        string description, 
        IReadOnlyList<ParamDef> parameters, 
        Func<JsonDocument, string> evaluator,
        bool isFinal = false) 
    {
        Name = name;
        Description = description;
        Parameters = parameters;
        Evaluator = evaluator;
        IsFinal = isFinal;
    }
}
