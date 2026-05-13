using Snek.Ast;
using Snek.Pipeline;

namespace Snek.Generation;

public class CodeGenerator : ICodeGenerator
{
    private readonly GenerationContext _ctx = new();
    private SectionEmitter _sections = null!;
    private StringCollector _collector = null!;
    private ExpressionEmitter _expressions = null!;
    private StatementEmitter _statements = null!;

    public string? Generate(AstNode root, CompilationContext context)
    {
        _ctx.Reset();
        _sections = new(_ctx);
        _collector = new(_ctx);
        _expressions = new(_ctx);
        _statements = new(_ctx, _expressions);

        if (root is not ProgramNode program)
        {
            return null;
        }

        _collector.Collect(program);

        _sections.EmitHeader();
        _sections.EmitDataSection();
        _sections.EmitImportSection();
        _sections.EmitTextSectionHeader();
        _sections.EmitEntryPoint();

        // Emit all function definitions first
        foreach (StatementNode stmt in program.Statements)
        {
            if (stmt is FunctionDefNode func)
            {
                _statements.EmitFunction(func);
            }
        }

        // Emit implicit entry point from top-level statements
        _statements.EmitEntryPoint(program.Statements);

        return _ctx.Output.ToString();
    }
}