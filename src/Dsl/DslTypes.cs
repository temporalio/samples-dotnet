namespace TemporalioSamples.Dsl;

using System.Text.Json.Serialization;

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

public record DslInput
{
    required public Statement Root { get; init; }

    public Dictionary<string, object> Variables { get; init; } = new();
}
