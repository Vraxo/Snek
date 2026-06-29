using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record FieldNode(Token Name, TypeNode Type) : AstNode;