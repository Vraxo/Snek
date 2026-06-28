using Snek.Core.Ast;
using Snek.Core.Lexing;

namespace Snek.Core.Generation;

public class BuiltinFunctionEmitter
{
    private readonly GenerationContext _generationContext;
    private readonly ExpressionEmitter _expressionEmitter;

    public BuiltinFunctionEmitter(GenerationContext generationContext, ExpressionEmitter expressionEmitter)
    {
        _generationContext = generationContext;
        _expressionEmitter = expressionEmitter;
    }

    public bool TryEmitBuiltin(CallExpressionNode call)
    {
        string calleeName = ExtractCalleeName(call);

        switch (calleeName)
        {
            case "print":
                EmitPrintCall(call);
                return true;
            case "pause":
                EmitPauseCall();
                return true;
            default:
                return false;
        }
    }

    private string ExtractCalleeName(CallExpressionNode call)
    {
        return call.Callee is IdentifierExpressionNode identifier
            ? identifier.Name.Value
            : string.Empty;
    }

    private void EmitPauseCall()
    {
        _generationContext.Emit("call [_getch]");
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
        string formatLabel = _generationContext.EnsureFormatString("\n");
        _generationContext.Emit($"push {formatLabel}");
        _generationContext.Emit("call [printf]");
        _generationContext.Emit("add esp, 4");
        _generationContext.Emit("push eax");
    }

    private void EmitStringLiteralPrint(ExpressionNode stringExpression)
    {
        _expressionEmitter.Emit(stringExpression);
        _generationContext.Emit("call [printf]");
        _generationContext.Emit("add esp, 4");
    }

    private void EmitFormattedPrint(ExpressionNode valueExpression)
    {
        string formatLabel = _generationContext.EnsureFormatString("%d\n");
        _expressionEmitter.Emit(valueExpression);
        _generationContext.Emit($"push {formatLabel}");
        _generationContext.Emit("call [printf]");
        _generationContext.Emit("add esp, 8");
    }

    private static bool IsStringLiteral(ExpressionNode expression)
    {
        return expression is LiteralExpressionNode literal
            && literal.Value.Type == TokenType.StringLiteral;
    }
}