namespace Snek.Ast;

/// <summary>
/// Base record for all AST nodes. Provides parent navigation for context-aware analysis.
/// </summary>
public abstract record AstNode
{
    public AstNode? Parent { get; set; }

    /// <summary>
    /// Yields all ancestor nodes from immediate parent to root.
    /// </summary>
    public IEnumerable<AstNode> Ancestors()
    {
        var current = Parent;
        while (current is not null)
        {
            yield return current;
            current = current.Parent;
        }
    }

    /// <summary>
    /// Finds the first ancestor of the specified type, or null.
    /// </summary>
    public T? AncestorOfType<T>() where T : AstNode
    {
        return Ancestors().OfType<T>().FirstOrDefault();
    }
}

// Placeholder for concrete node types to be added in subsequent iterations
public abstract record StatementNode : AstNode;
public abstract record ExpressionNode : AstNode;
public abstract record DeclarationNode : AstNode;