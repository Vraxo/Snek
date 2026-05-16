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
                _ctx.Emit("; unsupported expression");
                break;
        }
    }

    private void EmitLiteral(LiteralExpressionNode lit)
    {
        switch (lit.Value.Type)
        {
            case TokenType.StringLiteral:
                string label = _ctx.StringLiterals.First(kvp => kvp.Value == lit.Value.Value).Key;
                _ctx.Emit($"push {label}");
                break;

            case TokenType.IntegerLiteral:
                _ctx.Emit($"push {lit.Value.Value}");
                break;

            case TokenType.KeywordTrue:
                _ctx.Emit("push 1");
                break;

            case TokenType.KeywordFalse:
                _ctx.Emit("push 0");
                break;
        }
    }

    private void EmitIdentifier(IdentifierExpressionNode id)
    {
        if (_ctx.LocalOffsets.TryGetValue(id.Name.Value, out int offset))
        {
            // Load from local variable on stack
            _ctx.Emit($"; load {id.Name.Value} (ebp-{offset})");
            _ctx.Emit($"mov eax, [ebp-{offset}]");
            _ctx.Emit("push eax");
        }
        else
        {
            // Not a local variable (could be global or undefined)
            // For now, push a placeholder 0 and emit a comment
            _ctx.Emit($"; load {id.Name.Value} (global/undefined)");
            _ctx.Emit("push 0");
        }
    }

    private void EmitCall(CallExpressionNode call)
    {
        string callee = call.Callee is IdentifierExpressionNode id
            ? id.Name.Value
            : "unknown";

        string target;

        if (callee == "print")
        {
            // Handle print with proper format string
            EmitPrintCall(call);
            return;
        }
        else if (callee == "pause")
        {
            target = "[_getch]";
        }
        else if (_ctx.ExternalFunctions.Contains(callee))
        {
            target = $"[{callee}]";
        }
        else
        {
            target = _ctx.MangleName(callee);
        }

        // Normal function call: push arguments right-to-left
        for (int i = call.Arguments.Count - 1; i >= 0; i--)
        {
            Emit(call.Arguments[i]);
        }

        _ctx.Emit($"call {target}");

        if (call.Arguments.Count > 0)
        {
            _ctx.Emit($"add esp, {call.Arguments.Count * 4}");
        }

        _ctx.Emit("push eax");
    }

    private void EmitPrintCall(CallExpressionNode call)
    {
        if (call.Arguments.Count == 0)
        {
            // No arguments: just print newline
            string formatLabel = GetOrCreateFormatString("\n");
            _ctx.Emit($"push {formatLabel}");
            _ctx.Emit("call [printf]");
            _ctx.Emit("add esp, 4");
            _ctx.Emit("push eax");
            return;
        }

        // Check the type of the first argument
        ExpressionNode arg = call.Arguments[0];

        if (IsStringLiteral(arg))
        {
            // String literal: just push the string (no format string needed)
            Emit(arg);
            _ctx.Emit("call [printf]");
            _ctx.Emit("add esp, 4");
        }
        else
        {
            // For integers and other types, use format string
            string formatLabel = GetOrCreateFormatString("%d\n");

            // Push value first (rightmost argument for cdecl)
            Emit(arg);

            // Then push format string (leftmost argument)
            _ctx.Emit($"push {formatLabel}");

            _ctx.Emit("call [printf]");
            _ctx.Emit("add esp, 8"); // format string (4 bytes) + value (4 bytes)
        }

        _ctx.Emit("push eax");
    }

    private static bool IsStringLiteral(ExpressionNode expr)
    {
        return expr is LiteralExpressionNode lit
            && lit.Value.Type == TokenType.StringLiteral;
    }

    private string GetOrCreateFormatString(string format)
    {
        // Find existing format string or create new one
        foreach (KeyValuePair<string, string> kvp in _ctx.StringLiterals)
        {
            if (kvp.Value == format)
            {
                return kvp.Key;
            }
        }

        string label = $"fmt{_ctx.StringCounter++}";
        _ctx.StringLiterals[label] = format;
        return label;
    }

    private void EmitBinary(BinaryExpressionNode bin)
    {
        Emit(bin.Right);
        Emit(bin.Left);

        _ctx.Emit("pop ebx");
        _ctx.Emit("pop eax");

        switch (bin.Operator.Type)
        {
            case TokenType.Plus:
                _ctx.Emit("add eax, ebx");
                break;

            case TokenType.Minus:
                _ctx.Emit("sub eax, ebx");
                break;

            case TokenType.Star:
                _ctx.Emit("imul eax, ebx");
                break;

            case TokenType.DoubleEquals:
                _ctx.Emit("cmp eax, ebx");
                _ctx.Emit("sete al");
                _ctx.Emit("movzx eax, al");
                break;

            default:
                _ctx.Emit("; unsupported binary op");
                break;
        }

        _ctx.Emit("push eax");
    }
}