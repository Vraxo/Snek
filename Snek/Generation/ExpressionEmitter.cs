using Snek.Ast;
using Snek.Lexer;

namespace Snek.Generation;

public class ExpressionEmitter
{
    private readonly GenerationContext _generationContext;

    public ExpressionEmitter(GenerationContext generationContext)
    {
        _generationContext = generationContext;
    }

    public void Emit(ExpressionNode expression)
    {
        switch (expression)
        {
            case LiteralExpressionNode literal:
                EmitLiteralValue(literal);
                break;

            case IdentifierExpressionNode identifier:
                EmitIdentifierAccess(identifier);
                break;

            case CallExpressionNode call:
                EmitFunctionCall(call);
                break;

            case BinaryExpressionNode binary:
                EmitBinaryOperation(binary);
                break;

            default:
                _generationContext.Emit("; unsupported expression");
                break;
        }
    }

    private void EmitLiteralValue(LiteralExpressionNode literal)
    {
        switch (literal.Value.Type)
        {
            case TokenType.StringLiteral:
                string label = _generationContext.StringLiterals.First(kvp => kvp.Value == literal.Value.Value).Key;
                _generationContext.Emit($"push {label}");
                break;

            case TokenType.IntegerLiteral:
                _generationContext.Emit($"push {literal.Value.Value}");
                break;

            case TokenType.KeywordTrue:
                _generationContext.Emit("push 1");
                break;

            case TokenType.KeywordFalse:
                _generationContext.Emit("push 0");
                break;
        }
    }

    private void EmitIdentifierAccess(IdentifierExpressionNode identifier)
    {
        if (_generationContext.LocalOffsets.TryGetValue(identifier.Name.Value, out int offset))
        {
            _generationContext.Emit($"; load {identifier.Name.Value} (ebp-{offset})");
            _generationContext.Emit($"mov eax, [ebp-{offset}]");
            _generationContext.Emit("push eax");
        }
        else
        {
            _generationContext.Emit($"; load {identifier.Name.Value} (global/undefined)");
            _generationContext.Emit("push 0");
        }
    }

    private void EmitFunctionCall(CallExpressionNode call)
    {
        string calleeName = ExtractCalleeName(call);
        if (calleeName == "print")
        {
            EmitPrintCall(call);
            return;
        }
        if (calleeName == "pause")
        {
            EmitPauseCall();
            return;
        }

        string callTarget = DetermineCallTarget(calleeName);

        EmitArgumentsRightToLeft(call);
        _generationContext.Emit($"call {callTarget}");
        CleanupStackAfterCall(call.Arguments.Count);
        _generationContext.Emit("push eax");
    }

    private string ExtractCalleeName(CallExpressionNode call)
    {
        return call.Callee is IdentifierExpressionNode identifier
            ? identifier.Name.Value
            : "unknown";
    }

    private string DetermineCallTarget(string calleeName)
    {
        if (_generationContext.ExternalFunctions.Contains(calleeName))
            return $"[{calleeName}]";
        return _generationContext.MangleName(calleeName);
    }

    private void EmitPauseCall()
    {
        _generationContext.Emit("call [_getch]");
    }

    private void EmitArgumentsRightToLeft(CallExpressionNode call)
    {
        for (int i = call.Arguments.Count - 1; i >= 0; i--)
            Emit(call.Arguments[i]);
    }

    private void CleanupStackAfterCall(int argumentCount)
    {
        if (argumentCount > 0)
            _generationContext.Emit($"add esp, {argumentCount * 4}");
    }

    private void EmitPrintCall(CallExpressionNode call)
    {
        if (call.Arguments.Count == 0)
        {
            EmitPlainNewline();
            return;
        }

        ExpressionNode firstArgument = call.Arguments[0];
        if (IsStringLiteral(firstArgument))
        {
            EmitStringLiteralPrint(firstArgument);
        }
        else
        {
            EmitFormattedPrint(firstArgument);
        }
        _generationContext.Emit("push eax");
    }

    private void EmitPlainNewline()
    {
        string formatLabel = EnsureFormatString("\n");
        _generationContext.Emit($"push {formatLabel}");
        _generationContext.Emit("call [printf]");
        _generationContext.Emit("add esp, 4");
        _generationContext.Emit("push eax");
    }

    private void EmitStringLiteralPrint(ExpressionNode stringExpression)
    {
        Emit(stringExpression);
        _generationContext.Emit("call [printf]");
        _generationContext.Emit("add esp, 4");
    }

    private void EmitFormattedPrint(ExpressionNode valueExpression)
    {
        string formatLabel = EnsureFormatString("%d\n");
        Emit(valueExpression);
        _generationContext.Emit($"push {formatLabel}");
        _generationContext.Emit("call [printf]");
        _generationContext.Emit("add esp, 8");
    }

    private string EnsureFormatString(string format)
    {
        foreach (var kvp in _generationContext.StringLiterals)
        {
            if (kvp.Value == format)
                return kvp.Key;
        }

        string label = $"fmt{_generationContext.StringCounter++}";
        _generationContext.StringLiterals[label] = format;
        return label;
    }

    private static bool IsStringLiteral(ExpressionNode expression)
    {
        return expression is LiteralExpressionNode literal
            && literal.Value.Type == TokenType.StringLiteral;
    }

    private void EmitBinaryOperation(BinaryExpressionNode binary)
    {
        Emit(binary.Right);
        Emit(binary.Left);

        _generationContext.Emit("pop ebx");
        _generationContext.Emit("pop eax");

        switch (binary.Operator.Type)
        {
            case TokenType.Plus:
                _generationContext.Emit("add eax, ebx");
                break;

            case TokenType.Minus:
                _generationContext.Emit("sub eax, ebx");
                break;

            case TokenType.Star:
                _generationContext.Emit("imul eax, ebx");
                break;

            case TokenType.DoubleEquals:
                _generationContext.Emit("cmp eax, ebx");
                _generationContext.Emit("sete al");
                _generationContext.Emit("movzx eax, al");
                break;

            default:
                _generationContext.Emit("; unsupported binary op");
                break;
        }

        _generationContext.Emit("push eax");
    }
}