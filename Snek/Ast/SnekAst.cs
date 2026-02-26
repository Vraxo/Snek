using Snek.Lexer;

namespace Snek.Ast;

// Program root
public record ProgramNode(List<StatementNode> Statements) : AstNode;

// Statements
public record FunctionDefNode(
    Token Name,
    List<ParameterNode> Parameters,
    TypeNode? ReturnType,
    List<StatementNode> Body,
    int IndentLevel) : StatementNode;

public record ExpressionStatementNode(ExpressionNode Expression) : StatementNode;

public record IfStatementNode(
    ExpressionNode Condition,
    List<StatementNode> ThenBody,
    List<StatementNode>? ElseBody,
    int IndentLevel) : StatementNode;

public record WhileStatementNode(
    ExpressionNode Condition,
    List<StatementNode> Body,
    int IndentLevel) : StatementNode;

public record ReturnStatementNode(ExpressionNode? Value) : StatementNode;

public record PassStatementNode : StatementNode;

public record BreakStatementNode : StatementNode;

public record ContinueStatementNode : StatementNode;

// Expressions
public record CallExpressionNode(ExpressionNode Callee, List<ExpressionNode> Arguments) : ExpressionNode;

public record BinaryExpressionNode(ExpressionNode Left, Token Operator, ExpressionNode Right) : ExpressionNode;

public record UnaryExpressionNode(Token Operator, ExpressionNode Operand) : ExpressionNode;

public record MemberAccessExpressionNode(ExpressionNode Object, Token Member) : ExpressionNode;

public record IndexExpressionNode(ExpressionNode Target, ExpressionNode Index) : ExpressionNode;

public record LiteralExpressionNode(Token Value) : ExpressionNode;

public record IdentifierExpressionNode(Token Name) : ExpressionNode;

public record ListExpressionNode(List<ExpressionNode> Elements) : ExpressionNode;

public record DictExpressionNode(List<(ExpressionNode Key, ExpressionNode Value)> Items) : ExpressionNode;

// Parameters and Types
public record ParameterNode(Token Name, TypeNode? TypeAnnotation, ExpressionNode? Default) : AstNode;

public record TypeNode(Token Name, List<TypeNode>? GenericArgs) : AstNode
{
    public static TypeNode Simple(Token name)
    {
        return new(name, null);
    }

    public static TypeNode Generic(Token name, List<TypeNode> args)
    {
        return new(name, args);
    }
}