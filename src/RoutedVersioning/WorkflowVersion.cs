namespace RoutedVersioning;

public class WorkflowVersion
{
    public string Value { get; }

    public WorkflowVersion(string value)
    {
        Value = value;
    }

    public static readonly WorkflowVersion V1 = new("v1");
    public static readonly WorkflowVersion V2 = new("v2");
    public static readonly WorkflowVersion V3 = new("v3");

    public override string ToString() => Value;

    public override bool Equals(object? obj)
    {
        return obj is WorkflowVersion other && Value == other.Value;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
}