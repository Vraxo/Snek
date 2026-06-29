using Snek.Core.Ast;
using Snek.Core.Lexing;
using System.Reflection;

namespace Snek.Core.Generation;

public class StringCollector
{
    private readonly GenerationContext _ctx;
    private readonly HashSet<string> _localDeclarations = [];

    public StringCollector(GenerationContext ctx)
    {
        _ctx = ctx;
    }

    public void Collect(AstNode node)
    {
        CollectLocalDeclarations(node);
        CollectNode(node);
        WalkChildren(node);
    }

    private void CollectLocalDeclarations(AstNode node)
    {
        if (node is ProgramNode program)
        {
            foreach (StatementNode statement in program.Statements)
            {
                if (statement is FunctionDefNode func)
                {
                    _localDeclarations.Add(func.Name.Value);
                }
                else if (statement is ClassDefNode classDef)
                {
                    _localDeclarations.Add(classDef.Name.Value);
                }
                else if (statement is ImplBlockNode implBlock)
                {
                    string className = implBlock.TargetClass.Value;
                    foreach (FunctionDefNode method in implBlock.Methods)
                    {
                        _localDeclarations.Add($"{className}_{method.Name.Value}");
                    }
                }
            }
        }
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
        else if (node is ListExpressionNode)
        {
            _ctx.ExternalFunctions.Add("malloc");
        }
        else if (node is ClassDefNode classDef)
        {
            _ctx.ClassFields[classDef.Name.Value] = classDef.Fields.Select(f => f.Name.Value).ToList();
            _ctx.ExternalFunctions.Add("malloc");
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

        string name = id.Name.Value;

        if (_localDeclarations.Contains(name))
        {
            return; // Local function, constructor, or method call. Bypass DLL linkage.
        }

        if (name is "print")
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

        if (name is "pause")
        {
            _ctx.ExternalFunctions.Add("_getch");
            return;
        }

        if (name is "read_i32")
        {
            _ctx.ExternalFunctions.Add("scanf");
            if (!_ctx.StringLiterals.ContainsValue("%d"))
            {
                _ctx.StringLiterals[$"fmt{_ctx.StringCounter++}"] = "%d";
            }
            return;
        }

        _ctx.ExternalFunctions.Add(name);
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