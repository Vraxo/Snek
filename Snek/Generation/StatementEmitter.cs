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
                _ctx.Output.AppendLine("    ; unsupported statement");
                break;
        }
    }

    private void EmitExpressionStatement(ExpressionStatementNode stmt)
    {
        _expressions.Emit(stmt.Expression);
        _ctx.Output.AppendLine("    pop eax");
    }

    private void EmitReturn(ReturnStatementNode ret)
    {
        if (ret.Value == null)
        {
            _ctx.Output.AppendLine("    xor eax, eax");
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
        _ctx.Output.AppendLine("    pop eax");
        _ctx.Output.AppendLine("    test eax, eax");
        _ctx.Output.AppendLine($"    jz {elseLabel}");

        foreach (StatementNode s in ifs.ThenBody)
        {
            Emit(s);
        }

        _ctx.Output.AppendLine($"    jmp {endLabel}");
        _ctx.Output.AppendLine($"{elseLabel}:");

        if (ifs.ElseBody != null)
        {
            foreach (StatementNode s in ifs.ElseBody)
            {
                Emit(s);
            }
        }

        _ctx.Output.AppendLine($"{endLabel}:");
    }

    private void EmitWhile(WhileStatementNode whl)
    {
        string startLabel = $"_while_{_ctx.LabelCounter}";
        string endLabel = $"_endwhile_{_ctx.LabelCounter++}";

        _ctx.Output.AppendLine($"{startLabel}:");
        _expressions.Emit(whl.Condition);
        _ctx.Output.AppendLine("    pop eax");
        _ctx.Output.AppendLine("    test eax, eax");
        _ctx.Output.AppendLine($"    jz {endLabel}");

        foreach (StatementNode s in whl.Body)
        {
            Emit(s);
        }

        _ctx.Output.AppendLine($"    jmp {startLabel}");
        _ctx.Output.AppendLine($"{endLabel}:");
    }
}