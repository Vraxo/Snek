namespace Snek.Diagnoistics;

/// <summary>
/// Represents a compiler diagnostic (error or warning) with source location.
/// </summary>
public record Diagnostic(
    string SourceName,
    string Message,
    int Line,
    int Column,
    DiagnosticSeverity Severity = DiagnosticSeverity.Error)
{
    public bool IsError => Severity == DiagnosticSeverity.Error;
    public bool IsWarning => Severity == DiagnosticSeverity.Warning;

    public override string ToString()
    {
        var prefix = Severity == DiagnosticSeverity.Error ? "error" : "warning";
        return $"{SourceName}({Line},{Column}): {prefix}: {Message}";
    }
}

public enum DiagnosticSeverity { Error, Warning }