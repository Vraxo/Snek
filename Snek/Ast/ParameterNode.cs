using Snek.Lexer;

namespace Snek.Ast;

public record ParameterNode(Token Name, TypeNode? TypeAnnotation, ExpressionNode? Default) : AstNode;