using Snek.Ast;

namespace Snek.Generation;

public class StatementEmitter
{
    private readonly GenerationContext _generationContext;
    private readonly ExpressionEmitter _expressionEmitter;

    public StatementEmitter(GenerationContext generationContext, ExpressionEmitter expressionEmitter)
    {
        _generationContext = generationContext;
        _expressionEmitter = expressionEmitter;
    }

    public void EmitEntryPoint(IReadOnlyList<StatementNode> statements)
    {
        List<StatementNode> topLevelStatements = statements
            .Where(statement => statement is not FunctionDefNode)
            .ToList();

        if (topLevelStatements.Count == 0)
        {
            EmitEmptyEntryPointStub();
            return;
        }

        BeginFunctionPrologue();
        ReserveLocalVariablesSpace(topLevelStatements);
        EmitStatements(topLevelStatements);
        EmitFunctionEpilogueWithZeroReturn();
    }

    public void EmitFunction(FunctionDefNode function)
    {
        string mangledName = _generationContext.MangleName(function.Name.Value);
        _generationContext.EmitLine($"{mangledName}:");
        BeginFunctionPrologue();

        EmitParameterComments(function.Parameters);
        EmitStatements(function.Body);

        if (function.ReturnType == null)
            _generationContext.Emit("xor eax, eax");

        EmitFunctionEpilogue();
    }

    public void Emit(StatementNode statement)
    {
        switch (statement)
        {
            case ExpressionStatementNode expressionStatement:
                EmitExpressionStatement(expressionStatement);
                break;
            case ReturnStatementNode returnStatement:
                EmitReturnStatement(returnStatement);
                break;
            case IfStatementNode ifStatement:
                EmitIfStatement(ifStatement);
                break;
            case WhileStatementNode whileStatement:
                EmitWhileStatement(whileStatement);
                break;
            case VariableDeclarationNode variableDeclaration:
                EmitVariableDeclaration(variableDeclaration);
                break;
            default:
                _generationContext.Emit("; unsupported statement");
                break;
        }
    }

    private void EmitEmptyEntryPointStub()
    {
        _generationContext.EmitLine("_start:");
        _generationContext.Emit("xor eax, eax");
        _generationContext.Emit("ret");
        _generationContext.EmitLine();
    }

    private void BeginFunctionPrologue()
    {
        _generationContext.Emit("push ebp");
        _generationContext.Emit("mov ebp, esp");
    }

    private void EmitFunctionEpilogueWithZeroReturn()
    {
        _generationContext.Emit("xor eax, eax");
        EmitFunctionEpilogue();
    }

    private void EmitFunctionEpilogue()
    {
        _generationContext.Emit("leave");
        _generationContext.Emit("ret");
        _generationContext.EmitLine();
    }

    private void ReserveLocalVariablesSpace(List<StatementNode> topLevelStatements)
    {
        int localsSize = ComputeLocalVariablesSize(topLevelStatements);
        if (localsSize > 0)
            _generationContext.Emit($"sub esp, {localsSize}");
    }

    private int ComputeLocalVariablesSize(IEnumerable<StatementNode> statements)
    {
        int size = 0;
        foreach (var statement in statements)
        {
            if (statement is VariableDeclarationNode)
                size += 4;
        }
        return size;
    }

    private void EmitStatements(IEnumerable<StatementNode> statements)
    {
        foreach (StatementNode statement in statements)
        {
            Emit(statement);
        }
    }

    private void EmitParameterComments(IEnumerable<ParameterNode> parameters)
    {
        int paramOffset = 8;
        foreach (ParameterNode parameter in parameters)
        {
            _generationContext.Emit($"; param {parameter.Name.Value} at [ebp+{paramOffset}]");
            paramOffset += 4;
        }
    }

    private void EmitExpressionStatement(ExpressionStatementNode expressionStatement)
    {
        _expressionEmitter.Emit(expressionStatement.Expression);
        _generationContext.Emit("pop eax");
    }

    private void EmitReturnStatement(ReturnStatementNode returnStatement)
    {
        if (returnStatement.Value == null)
        {
            _generationContext.Emit("xor eax, eax");
        }
        else
        {
            _expressionEmitter.Emit(returnStatement.Value);
        }
    }

    private void EmitIfStatement(IfStatementNode ifStatement)
    {
        string elseLabel = $"_else_{_generationContext.LabelCounter++}";
        string endLabel = $"_endif_{_generationContext.LabelCounter}";

        _expressionEmitter.Emit(ifStatement.Condition);
        _generationContext.Emit("pop eax");
        _generationContext.Emit("test eax, eax");
        _generationContext.Emit($"jz {elseLabel}");

        EmitStatements(ifStatement.ThenBody);
        _generationContext.Emit($"jmp {endLabel}");
        _generationContext.EmitLine($"{elseLabel}:");

        if (ifStatement.ElseBody != null)
        {
            EmitStatements(ifStatement.ElseBody);
        }

        _generationContext.EmitLine($"{endLabel}:");
    }

    private void EmitWhileStatement(WhileStatementNode whileStatement)
    {
        string startLabel = $"_while_{_generationContext.LabelCounter}";
        string endLabel = $"_endwhile_{_generationContext.LabelCounter++}";

        _generationContext.EmitLine($"{startLabel}:");
        _expressionEmitter.Emit(whileStatement.Condition);
        _generationContext.Emit("pop eax");
        _generationContext.Emit("test eax, eax");
        _generationContext.Emit($"jz {endLabel}");

        EmitStatements(whileStatement.Body);
        _generationContext.Emit($"jmp {startLabel}");
        _generationContext.EmitLine($"{endLabel}:");
    }

    private void EmitVariableDeclaration(VariableDeclarationNode variableDeclaration)
    {
        if (variableDeclaration.Initializer != null)
        {
            _expressionEmitter.Emit(variableDeclaration.Initializer);
        }
        else
        {
            _generationContext.Emit("xor eax, eax");
            _generationContext.Emit("push eax");
        }

        int offset = _generationContext.NextLocalOffset;
        _generationContext.LocalOffsets[variableDeclaration.Name.Value] = offset;
        _generationContext.NextLocalOffset += 4;

        _generationContext.Emit("pop eax");
        _generationContext.Emit($"mov [ebp-{offset}], eax");
    }
}