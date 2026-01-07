using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace TemporalioSamples.Dsl;

public record DslInput
{
    public record ActivityInvocation
    {
        required public string Name { get; init; }

        public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();

        public string? Result { get; init; }
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "statementType")]
    [JsonDerivedType(typeof(ActivityStatement), "activity")]
    [JsonDerivedType(typeof(SequenceStatement), "sequence")]
    [JsonDerivedType(typeof(ParallelStatement), "parallel")]
    public abstract record Statement;

    public record ActivityStatement : Statement
    {
        required public ActivityInvocation Activity { get; init; }
    }

    public record Sequence
    {
        required public IReadOnlyList<Statement> Elements { get; init; }
    }

    public record SequenceStatement : Statement
    {
        required public Sequence Sequence { get; init; }
    }

    public record ParallelBranches
    {
        required public IReadOnlyList<Statement> Branches { get; init; }
    }

    public record ParallelStatement : Statement
    {
        required public ParallelBranches Parallel { get; init; }
    }

    required public Statement Root { get; init; }

    public Dictionary<string, object> Variables { get; init; } = new();

    public static DslInput Parse(string yamlContent)
    {
        var deserializer = new DeserializerBuilder().Build();

        var yamlObject = deserializer.Deserialize<Dictionary<string, object>>(yamlContent)
            ?? throw new InvalidOperationException("Failed to parse YAML");

        return ConvertToDslInput(yamlObject);
    }

    private static DslInput ConvertToDslInput(Dictionary<string, object> yaml)
    {
        var variables = new Dictionary<string, object>();
        if (yaml.TryGetValue("variables", out var varsObj) && varsObj is Dictionary<object, object> varsDict)
        {
            foreach (var kvp in varsDict)
            {
                variables[kvp.Key.ToString() ?? string.Empty] = kvp.Value;
            }
        }

        var rootObj = yaml["root"];
        var root = ConvertToStatement(rootObj);

        return new DslInput { Root = root, Variables = variables };
    }

    private static Statement ConvertToStatement(object obj)
    {
        if (obj is not Dictionary<object, object> dict)
        {
            throw new ArgumentException("Statement must be a dictionary");
        }

        if (dict.TryGetValue("activity", out var activityObj))
        {
            return new ActivityStatement { Activity = ConvertToActivityInvocation(activityObj) };
        }

        if (dict.TryGetValue("sequence", out var sequenceObj))
        {
            var seqDict = (Dictionary<object, object>)sequenceObj;
            var elements = ((List<object>)seqDict["elements"])
                .Select(ConvertToStatement)
                .ToList();
            return new SequenceStatement { Sequence = new Sequence { Elements = elements } };
        }

        if (dict.TryGetValue("parallel", out var parallelObj))
        {
            var parDict = (Dictionary<object, object>)parallelObj;
            var branches = ((List<object>)parDict["branches"])
                .Select(ConvertToStatement)
                .ToList();
            return new ParallelStatement { Parallel = new ParallelBranches { Branches = branches } };
        }

        throw new ArgumentException("Unknown statement type");
    }

    private static ActivityInvocation ConvertToActivityInvocation(object obj)
    {
        var dict = (Dictionary<object, object>)obj;
        var name = dict["name"].ToString() ?? throw new ArgumentException("Activity name is required");

        var arguments = new List<string>();
        if (dict.TryGetValue("arguments", out var argsObj) && argsObj is List<object> argsList)
        {
            arguments = argsList.Select(a => a.ToString() ?? string.Empty).ToList();
        }

        string? result = null;
        if (dict.TryGetValue("result", out var resultObj))
        {
            result = resultObj.ToString();
        }

        return new ActivityInvocation
        {
            Name = name,
            Arguments = arguments,
            Result = result,
        };
    }
}