using Snek.Ast;
using Snek.Lexing;
using System.Reflection;

namespace Snek.Generation;

public class StringCollector
{
    private readonly GenerationContext _ctx;

    public StringCollector(GenerationContext ctx)
    {
        _ctx = ctx;
    }

    public void Collect(AstNode node)
    {
        CollectNode(node);
        WalkChildren(node);
    }

    private void CollectNode(AstNode node)
    {
        if (node is LiteralExpressionNode lit)
        {
            CollectStringLiteral(lit);
        }
        else if (node is CallExpressionNode call)
        {
            CollectExternalCall(call);
        }
    }

    private void CollectStringLiteral(LiteralExpressionNode lit)
    {
        if (lit.Value.Type != TokenType.StringLiteral)
        {
            return;
        }

        if (_ctx.StringLiterals.ContainsValue(lit.Value.Value))
        {
            return;
        }

        _ctx.StringLiterals[$"str{_ctx.StringCounter++}"] = lit.Value.Value;
    }

    private void CollectExternalCall(CallExpressionNode call)
    {
        if (call.Callee is not IdentifierExpressionNode id)
        {
            return;
        }

        if (id.Name.Value is "print")
        {
            _ctx.ExternalFunctions.Add("printf");

            // Check if we need format strings for non-string arguments
            if (call.Arguments.Count > 0 && !IsStringLiteral(call.Arguments[0]))
            {
                // Ensure integer format string is collected
                if (!_ctx.StringLiterals.ContainsValue("%d\n"))
                {
                    _ctx.StringLiterals[$"fmt{_ctx.StringCounter++}"] = "%d\n";
                }
            }
            else if (call.Arguments.Count == 0)
            {
                // Empty print - just newline
                if (!_ctx.StringLiterals.ContainsValue("\n"))
                {
                    _ctx.StringLiterals[$"fmt{_ctx.StringCounter++}"] = "\n";
                }
            }

            return;
        }

        if (id.Name.Value is "pause")
        {
            _ctx.ExternalFunctions.Add("_getch");
            return;
        }

        _ctx.ExternalFunctions.Add(id.Name.Value);
    }

    private bool IsStringLiteral(ExpressionNode expr)
    {
        return expr is LiteralExpressionNode lit
            && lit.Value.Type == TokenType.StringLiteral;
    }

    private void WalkChildren(AstNode node)
    {
        foreach (PropertyInfo prop in node.GetType().GetProperties())
        {
            if (prop.Name == "Parent")
            {
                continue;
            }

            object? value = prop.GetValue(node);

            if (value is AstNode child)
            {
                Collect(child);
            }
            else if (value is IEnumerable<AstNode> children)
            {
                foreach (AstNode c in children)
                {
                    Collect(c);
                }
            }
        }
    }
}