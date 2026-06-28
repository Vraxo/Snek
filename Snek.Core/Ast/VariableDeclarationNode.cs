using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record VariableDeclarationNode(
    Token Name,
    TypeNode Type,
    ExpressionNode? Initializer
) : StatementNode;