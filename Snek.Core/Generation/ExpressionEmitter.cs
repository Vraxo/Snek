using Snek.Core.Ast;
using Snek.Core.Lexing;

namespace Snek.Core.Generation;

public class ExpressionEmitter
{
    private readonly GenerationContext _generationContext;
    private readonly BuiltinFunctionEmitter _builtinEmitter;

    public ExpressionEmitter(GenerationContext generationContext)
    {
        _generationContext = generationContext;
        _builtinEmitter = new BuiltinFunctionEmitter(generationContext, this);
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
        if (_builtinEmitter.TryEmitBuiltin(call))
        {
            return;
        }

        string calleeName = ExtractCalleeName(call);
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
        {
            return $"[{calleeName}]";
        }

        return _generationContext.MangleName(calleeName);
    }

    private void EmitArgumentsRightToLeft(CallExpressionNode call)
    {
        for (int i = call.Arguments.Count - 1; i >= 0; i--)
        {
            Emit(call.Arguments[i]);
        }
    }

    private void CleanupStackAfterCall(int argumentCount)
    {
        if (argumentCount <= 0)
        {
            return;
        }

        _generationContext.Emit($"add esp, {argumentCount * 4}");
    }

    private void EmitBinaryOperation(BinaryExpressionNode binary)
    {
        Emit(binary.Left);
        Emit(binary.Right);

        _generationContext.Emit("pop ebx"); // ebx = Right
        _generationContext.Emit("pop eax"); // eax = Left

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

            case TokenType.Slash:
            case TokenType.DoubleSlash:
                _generationContext.Emit("cdq");
                _generationContext.Emit("idiv ebx");
                break;

            case TokenType.Percent:
                _generationContext.Emit("cdq");
                _generationContext.Emit("idiv ebx");
                _generationContext.Emit("mov eax, edx");
                break;

            case TokenType.DoubleEquals:
                _generationContext.Emit("cmp eax, ebx");
                _generationContext.Emit("sete al");
                _generationContext.Emit("movzx eax, al");
                break;

            case TokenType.NotEquals:
                _generationContext.Emit("cmp eax, ebx");
                _generationContext.Emit("setne al");
                _generationContext.Emit("movzx eax, al");
                break;

            case TokenType.LessThan:
                _generationContext.Emit("cmp eax, ebx");
                _generationContext.Emit("setl al");
                _generationContext.Emit("movzx eax, al");
                break;

            case TokenType.GreaterThan:
                _generationContext.Emit("cmp eax, ebx");
                _generationContext.Emit("setg al");
                _generationContext.Emit("movzx eax, al");
                break;

            case TokenType.LessEqual:
                _generationContext.Emit("cmp eax, ebx");
                _generationContext.Emit("setle al");
                _generationContext.Emit("movzx eax, al");
                break;

            case TokenType.GreaterEqual:
                _generationContext.Emit("cmp eax, ebx");
                _generationContext.Emit("setge al");
                _generationContext.Emit("movzx eax, al");
                break;

            default:
                _generationContext.Emit("; unsupported binary op");
                break;
        }

        _generationContext.Emit("push eax");
    }
}