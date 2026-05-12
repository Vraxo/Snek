using Snek.Ast;
using Snek.Lexer;

namespace Snek.Generation;

public class ExpressionEmitter
{
    private readonly GenerationContext _ctx;

    public ExpressionEmitter(GenerationContext ctx)
    {
        _ctx = ctx;
    }

    public void Emit(ExpressionNode expr)
    {
        switch (expr)
        {
            case LiteralExpressionNode lit:
                EmitLiteral(lit);
                break;

            case IdentifierExpressionNode id:
                EmitIdentifier(id);
                break;

            case CallExpressionNode call:
                EmitCall(call);
                break;

            case BinaryExpressionNode bin:
                EmitBinary(bin);
                break;

            default:
                _ctx.Output.AppendLine("    ; unsupported expression");
                break;
        }
    }

    private void EmitLiteral(LiteralExpressionNode lit)
    {
        switch (lit.Value.Type)
        {
            case TokenType.StringLiteral:
                string label = _ctx.StringLiterals.First(kvp => kvp.Value == lit.Value.Value).Key;
                _ctx.Output.AppendLine($"    push {label}");
                break;

            case TokenType.IntegerLiteral:
                _ctx.Output.AppendLine($"    push {lit.Value.Value}");
                break;

            case TokenType.KeywordTrue:
                _ctx.Output.AppendLine("    push 1");
                break;

            case TokenType.KeywordFalse:
                _ctx.Output.AppendLine("    push 0");
                break;
        }
    }

    private void EmitIdentifier(IdentifierExpressionNode id)
    {
        _ctx.Output.AppendLine($"    ; load {id.Name.Value}");
        _ctx.Output.AppendLine("    push 0");
    }

    private void EmitCall(CallExpressionNode call)
    {
        // Push arguments right-to-left
        for (int i = call.Arguments.Count - 1; i >= 0; i--)
        {
            Emit(call.Arguments[i]);
        }

        string callee = call.Callee is IdentifierExpressionNode id
            ? id.Name.Value
            : "unknown";

        string target;

        if (callee == "print")
        {
            target = "[printf]";
            _ctx.ExternalFunctions.Add("printf");
        }
        else
        {
            target = callee == "main"
                ? "_main"
                : _ctx.ExternalFunctions.Contains(callee) ? $"[{callee}]" : callee;
        }

        _ctx.Output.AppendLine($"    call {target}");

        if (call.Arguments.Count > 0)
        {
            _ctx.Output.AppendLine($"    add esp, {call.Arguments.Count * 4}");
        }

        _ctx.Output.AppendLine("    push eax");
    }

    private void EmitBinary(BinaryExpressionNode bin)
    {
        Emit(bin.Right);
        Emit(bin.Left);

        _ctx.Output.AppendLine("    pop ebx");
        _ctx.Output.AppendLine("    pop eax");

        switch (bin.Operator.Type)
        {
            case TokenType.Plus:
                _ctx.Output.AppendLine("    add eax, ebx");
                break;

            case TokenType.Minus:
                _ctx.Output.AppendLine("    sub eax, ebx");
                break;

            case TokenType.Star:
                _ctx.Output.AppendLine("    imul eax, ebx");
                break;

            case TokenType.DoubleEquals:
                _ctx.Output.AppendLine("    cmp eax, ebx");
                _ctx.Output.AppendLine("    sete al");
                _ctx.Output.AppendLine("    movzx eax, al");
                break;

            default:
                _ctx.Output.AppendLine("    ; unsupported binary op");
                break;
        }

        _ctx.Output.AppendLine("    push eax");
    }
}