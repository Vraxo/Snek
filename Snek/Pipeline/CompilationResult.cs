using Snek.Diagnoistics;

namespace Snek.Pipeline;

public record CompilationResult(string? Output, IReadOnlyList<Diagnostic> Diagnostics)
{
    public CompilationResult(IReadOnlyList<Diagnostic> diagnostics) : this(null, diagnostics)
    {
    }

    public bool Success => !Diagnostics.Any(d => d.IsError);
}