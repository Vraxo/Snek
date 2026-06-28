using Snek.Core.Diagnoistics;

namespace Snek.Core.Pipeline;

public interface IPipelineStage
{
    bool Execute(CompilationContext context);

    IReadOnlyList<Diagnostic> GetDiagnostics();
}