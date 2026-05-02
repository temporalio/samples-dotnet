namespace TemporalioSamples.ToolRegistryIncidentTriage;

public record AlertPayload(
    string Status,
    Dictionary<string, string> Labels,
    Dictionary<string, string> Annotations,
    string StartsAt,
    string? EndsAt = null,
    string? Fingerprint = null);

public record ProposedRemediation(string Action, string Justification);

public record TriageResult(
    string Status, // "resolved" | "unresolved"
    string Summary,
    List<ProposedRemediation> Remediations);

public record ApprovalRequest(string Message, string Diagnosis, string ProposedAction);

public record ApprovalResponse(string Decision, string Reason);
