using Snek.Lexing;

namespace Snek.Ast;

public record VariableDeclarationNode(
    Token Name,
    TypeNode Type,
    ExpressionNode? Initializer,
    int IndentLevel
) : StatementNode;