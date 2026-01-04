namespace TemporalioSamples.Dsl;

using YamlDotNet.Serialization;

public static class DslParser
{
    public static DslInput ParseYaml(string yamlContent)
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
