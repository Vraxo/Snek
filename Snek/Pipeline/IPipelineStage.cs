using Snek.Diagnoistics;

namespace Snek.Pipeline;

public interface IPipelineStage
{
    bool Execute(CompilationContext context);

    IReadOnlyList<Diagnostic> GetDiagnostics();
}