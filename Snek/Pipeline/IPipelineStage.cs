using Snek.Diagnoistics;

namespace Snek.Pipeline;

/// <summary>
/// Base contract for any pipeline stage. Enables uniform error handling and logging.
/// </summary>
public interface IPipelineStage
{
    /// <summary>
    /// Executes the stage, mutating the context and returning success status.
    /// </summary>
    bool Execute(CompilationContext context);

    /// <summary>
    /// Optional: collects diagnostics produced by this stage.
    /// </summary>
    IReadOnlyList<Diagnostic> GetDiagnostics();
}