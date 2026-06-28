using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record ParameterNode(Token Name, TypeNode? TypeAnnotation, ExpressionNode? Default) : AstNode;