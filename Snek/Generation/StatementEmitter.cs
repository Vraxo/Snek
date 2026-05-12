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
}