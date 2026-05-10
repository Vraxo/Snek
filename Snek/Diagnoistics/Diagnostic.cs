namespace Snek.Diagnoistics;

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
        string prefix = Severity == DiagnosticSeverity.Error
            ? "error"
            : "warning";

        return $"{SourceName}({Line},{Column}): {prefix}: {Message}";
    }
}