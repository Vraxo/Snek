using Snek.Ast;

namespace Snek.Generation;

public class StatementEmitter
{
    private readonly GenerationContext _ctx;
    private readonly ExpressionEmitter _expressions;

    public StatementEmitter(GenerationContext ctx, ExpressionEmitter expressions)
    {
        _ctx = ctx;
        _expressions = expressions;
    }

    private int ComputeLocalsSize(IEnumerable<StatementNode> statements)
    {
        int size = 0;
        foreach (var stmt in statements)
        {
            if (stmt is VariableDeclarationNode)
                size += 4;
        }
        return size;
    }

    public void EmitEntryPoint(IReadOnlyList<StatementNode> statements)
    {
        List<StatementNode> topLevel = statements
            .Where(s => s is not FunctionDefNode)
            .ToList();

        if (topLevel.Count == 0)
        {
            // No top-level statements — emit an empty _start stub
            _ctx.EmitLine("_start:");
            _ctx.Emit("xor eax, eax");
            _ctx.Emit("ret");
            _ctx.EmitLine();
            return;
        }

        _ctx.EmitLine("_start:");
        _ctx.Emit("push ebp");
        _ctx.Emit("mov ebp, esp");

        // Reserve space for local variables
        int localsSize = ComputeLocalsSize(topLevel);
        if (localsSize > 0)
            _ctx.Emit($"sub esp, {localsSize}");

        foreach (StatementNode stmt in topLevel)
        {
            Emit(stmt);
        }

        _ctx.Emit("xor eax, eax");
        _ctx.Emit("leave");
        _ctx.Emit("ret");
        _ctx.EmitLine();
    }

    public void EmitFunction(FunctionDefNode func)
    {
        string mangledName = _ctx.MangleName(func.Name.Value);

        _ctx.EmitLine($"{mangledName}:");
        _ctx.Emit("push ebp");
        _ctx.Emit("mov ebp, esp");

        int paramOffset = 8;

        foreach (ParameterNode param in func.Parameters)
        {
            _ctx.Emit($"; param {param.Name.Value} at [ebp+{paramOffset}]");
            paramOffset += 4;
        }

        foreach (StatementNode stmt in func.Body)
        {
            Emit(stmt);
        }

        if (func.ReturnType == null)
        {
            _ctx.Emit("xor eax, eax");
        }

        _ctx.Emit("leave");
        _ctx.Emit("ret");
        _ctx.EmitLine();
    }

    public void Emit(StatementNode stmt)
    {
        switch (stmt)
        {
            case ExpressionStatementNode expr:
                EmitExpressionStatement(expr);
                break;

            case ReturnStatementNode ret:
                EmitReturn(ret);
                break;

            case IfStatementNode ifs:
                EmitIf(ifs);
                break;

            case WhileStatementNode whl:
                EmitWhile(whl);
                break;

            case VariableDeclarationNode varDecl:
                EmitVariableDeclaration(varDecl);
                break;

            default:
                _ctx.Emit("; unsupported statement");
                break;
        }
    }

    private void EmitExpressionStatement(ExpressionStatementNode stmt)
    {
        _expressions.Emit(stmt.Expression);
        _ctx.Emit("pop eax");
    }

    private void EmitReturn(ReturnStatementNode ret)
    {
        if (ret.Value == null)
        {
            _ctx.Emit("xor eax, eax");
        }
        else
        {
            _expressions.Emit(ret.Value);
        }
    }

    private void EmitIf(IfStatementNode ifs)
    {
        string elseLabel = $"_else_{_ctx.LabelCounter++}";
        string endLabel = $"_endif_{_ctx.LabelCounter}";

        _expressions.Emit(ifs.Condition);
        _ctx.Emit("pop eax");
        _ctx.Emit("test eax, eax");
        _ctx.Emit($"jz {elseLabel}");

        foreach (StatementNode s in ifs.ThenBody)
        {
            Emit(s);
        }

        _ctx.Emit($"jmp {endLabel}");
        _ctx.EmitLine($"{elseLabel}:");

        if (ifs.ElseBody != null)
        {
            foreach (StatementNode s in ifs.ElseBody)
            {
                Emit(s);
            }
        }

        _ctx.EmitLine($"{endLabel}:");
    }

    private void EmitWhile(WhileStatementNode whl)
    {
        string startLabel = $"_while_{_ctx.LabelCounter}";
        string endLabel = $"_endwhile_{_ctx.LabelCounter++}";

        _ctx.EmitLine($"{startLabel}:");
        _expressions.Emit(whl.Condition);
        _ctx.Emit("pop eax");
        _ctx.Emit("test eax, eax");
        _ctx.Emit($"jz {endLabel}");

        foreach (StatementNode s in whl.Body)
        {
            Emit(s);
        }

        _ctx.Emit($"jmp {startLabel}");
        _ctx.EmitLine($"{endLabel}:");
    }

    private void EmitVariableDeclaration(VariableDeclarationNode varDecl)
    {
        // Emit initializer
        if (varDecl.Initializer != null)
        {
            _expressions.Emit(varDecl.Initializer);
        }
        else
        {
            // Default zero-initialize
            _ctx.Emit("xor eax, eax");
            _ctx.Emit("push eax");
        }

        // Allocate stack offset
        int offset = _ctx.NextLocalOffset;
        _ctx.LocalOffsets[varDecl.Name.Value] = offset;
        _ctx.NextLocalOffset += 4;

        // Store value into local variable slot
        _ctx.Emit("pop eax");
        _ctx.Emit($"mov [ebp-{offset}], eax");
    }
}