using Snek.Core.Lexing;

namespace Snek.Core.Ast;

public record UseStatementNode(Token ModuleName, Token? ItemName, bool IsWildcard) : StatementNode;