namespace Snek.Lexer;

public class LexerRules
{
    public Dictionary<string, TokenType> Keywords { get; } = [];
    public List<(string Pattern, TokenType Type)> Operators { get; } = [];
    public char StringDelimiter { get; set; } = '"';
    public char CharDelimiter { get; set; } = '\'';
    public bool SupportsIndentation { get; set; } = true;
    public int TabWidth { get; set; } = 2;
    public bool AllowTrailingCommas { get; set; } = true;
    public HashSet<char> IdentifierStartChars { get; } = ['_'];
    public HashSet<char> IdentifierContinueChars { get; } = [];

    public LexerRules()
    {
        // Default Snek keywords
        Keywords["fn"] = TokenType.KeywordFn;
        Keywords["if"] = TokenType.KeywordIf;
        Keywords["else"] = TokenType.KeywordElse;
        Keywords["while"] = TokenType.KeywordWhile;
        Keywords["for"] = TokenType.KeywordFor;
        Keywords["in"] = TokenType.KeywordIn;
        Keywords["return"] = TokenType.KeywordReturn;
        Keywords["break"] = TokenType.KeywordBreak;
        Keywords["continue"] = TokenType.KeywordContinue;
        Keywords["pass"] = TokenType.KeywordPass;
        Keywords["import"] = TokenType.KeywordImport;
        Keywords["from"] = TokenType.KeywordFrom;
        Keywords["as"] = TokenType.KeywordAs;
        Keywords["class"] = TokenType.KeywordClass;
        Keywords["void"] = TokenType.KeywordVoid;
        Keywords["int"] = TokenType.KeywordInt;
        Keywords["string"] = TokenType.KeywordString;
        Keywords["bool"] = TokenType.KeywordBool;
        Keywords["float"] = TokenType.KeywordFloat;
        Keywords["true"] = TokenType.KeywordTrue;
        Keywords["false"] = TokenType.KeywordFalse;
        Keywords["none"] = TokenType.KeywordNone;
        Keywords["and"] = TokenType.KeywordAnd;
        Keywords["or"] = TokenType.KeywordOr;
        Keywords["not"] = TokenType.KeywordNot;

        // Default operators (longest first to avoid prefix conflicts)
        Operators.Add(("**=", TokenType.DoubleStarAssign));
        Operators.Add(("//=", TokenType.DoubleSlashAssign));
        Operators.Add(("<<=", TokenType.LeftShiftAssign));
        Operators.Add((">>=", TokenType.RightShiftAssign));
        Operators.Add(("+=", TokenType.PlusAssign));
        Operators.Add(("-=", TokenType.MinusAssign));
        Operators.Add(("*=", TokenType.StarAssign));
        Operators.Add(("/=", TokenType.SlashAssign));
        Operators.Add(("%=", TokenType.PercentAssign));
        Operators.Add(("&=", TokenType.AmpersandAssign));
        Operators.Add(("|=", TokenType.PipeAssign));
        Operators.Add(("^=", TokenType.CaretAssign));
        Operators.Add(("==", TokenType.DoubleEquals));
        Operators.Add(("!=", TokenType.NotEquals));
        Operators.Add(("<=", TokenType.LessEqual));
        Operators.Add((">=", TokenType.GreaterEqual));
        Operators.Add(("->", TokenType.Arrow));
        Operators.Add(("**", TokenType.DoubleStar));
        Operators.Add(("//", TokenType.DoubleSlash));
        Operators.Add(("<<", TokenType.LeftShift));
        Operators.Add((">>", TokenType.RightShift));
        Operators.Add(("+", TokenType.Plus));
        Operators.Add(("-", TokenType.Minus));
        Operators.Add(("*", TokenType.Star));
        Operators.Add(("/", TokenType.Slash));
        Operators.Add(("%", TokenType.Percent));
        Operators.Add(("=", TokenType.Equals));
        Operators.Add(("<", TokenType.LessThan));
        Operators.Add((">", TokenType.GreaterThan));
        Operators.Add((":", TokenType.Colon));
        Operators.Add((",", TokenType.Comma));
        Operators.Add((".", TokenType.Dot));
        Operators.Add(("(", TokenType.LeftParen));
        Operators.Add((")", TokenType.RightParen));
        Operators.Add(("[", TokenType.LeftBracket));
        Operators.Add(("]", TokenType.RightBracket));
        Operators.Add(("{", TokenType.LeftBrace));
        Operators.Add(("}", TokenType.RightBrace));
        Operators.Add(("@", TokenType.At));
        Operators.Add(("?", TokenType.Question));
        Operators.Add(("&", TokenType.Ampersand));
        Operators.Add(("|", TokenType.Pipe));
        Operators.Add(("~", TokenType.Tilde));
        Operators.Add(("^", TokenType.Caret));
    }

    public static LexerRules CreatePythonStyle()
    {
        LexerRules rules = new();
        rules.Keywords["def"] = TokenType.KeywordDef;
        return rules;
    }
}