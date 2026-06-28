using Snek.Core.Ast;
using Snek.Core.Pipeline;

namespace Snek.Core.Generation;

public class CodeGenerator : ICodeGenerator
{
    private readonly GenerationContext _generationContext = new();
    private SectionEmitter _sectionEmitter = null!;
    private StringCollector _stringCollector = null!;
    private ExpressionEmitter _expressionEmitter = null!;
    private StatementEmitter _statementEmitter = null!;

    public string? Generate(AstNode root, CompilationContext context)
    {
        InitializeComponents();

        if (root is not ProgramNode program)
        {
            return null;
        }

        _stringCollector.Collect(program);

        EmitPrelude();
        EmitFunctionDefinitions(program);
        EmitEntryPoint(program);

        return _generationContext.Output.ToString();
    }

    private void InitializeComponents()
    {
        _generationContext.Reset();
        _sectionEmitter = new(_generationContext);
        _stringCollector = new(_generationContext);
        _expressionEmitter = new(_generationContext);
        _statementEmitter = new(_generationContext, _expressionEmitter);
    }

    private void EmitPrelude()
    {
        _sectionEmitter.EmitHeader();
        _sectionEmitter.EmitDataSection();
        _sectionEmitter.EmitImportSection();
        _sectionEmitter.EmitTextSectionHeader();
        _sectionEmitter.EmitEntryPoint();
    }

    private void EmitFunctionDefinitions(ProgramNode program)
    {
        foreach (StatementNode statement in program.Statements)
        {
            if (statement is not FunctionDefNode function)
            {
                continue;
            }

            _statementEmitter.EmitFunction(function);
        }
    }

    private void EmitEntryPoint(ProgramNode program)
    {
        _statementEmitter.EmitEntryPoint(program.Statements);
    }
}