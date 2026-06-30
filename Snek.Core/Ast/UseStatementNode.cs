using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record UseStatementNode(List<Token> Path, bool IsWildcard) : StatementNode;