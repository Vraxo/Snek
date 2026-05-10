namespace Snek.Ast;

public abstract record AstNode
{
    public AstNode? Parent { get; set; }

    public IEnumerable<AstNode> Ancestors()
    {
        AstNode? current = Parent;

        while (current is not null)
        {
            yield return current;
            current = current.Parent;
        }
    }

    public T? AncestorOfType<T>() where T : AstNode
    {
        return Ancestors()
            .OfType<T>()
            .FirstOrDefault();
    }
}