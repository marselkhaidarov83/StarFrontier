using UnityEngine;

public class ValidationIssue
{
    public ValidationSeverity Severity { get; }
    public string Message { get; }
    public Object SourceObject { get; }

    public ValidationIssue(ValidationSeverity severity, string message, Object sourceObject)
    {
        Severity = severity;
        Message = message;
        SourceObject = sourceObject;
    }

    public override string ToString()
    {
        var sourceName = SourceObject != null ? SourceObject.name : "Unknown";
        return $"[{Severity}] {sourceName}: {Message}";
    }
}