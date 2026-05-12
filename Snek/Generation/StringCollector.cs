using Snek.Ast;
using Snek.Lexer;
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

        if (id.Name.Value is "main")
        {
            return;
        }

        if (id.Name.Value is "print")
        {
            _ctx.ExternalFunctions.Add("printf");
            return;
        }

        if (id.Name.Value is "pause")
        {
            _ctx.ExternalFunctions.Add("_getch");
            return;
        }

        _ctx.ExternalFunctions.Add(id.Name.Value);
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