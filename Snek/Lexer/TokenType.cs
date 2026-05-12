namespace Snek.Lexer;

public enum TokenType
{
    // Structural
    Unknown,
    Eof,
    Newline,
    Indent,
    Dedent,

    // Keywords
    KeywordFn,
    KeywordIf,
    KeywordElse,
    KeywordWhile,
    KeywordFor,
    KeywordIn,
    KeywordReturn,
    KeywordBreak,
    KeywordContinue,
    KeywordPass,
    KeywordImport,
    KeywordFrom,
    KeywordAs,
    KeywordClass,
    KeywordDef,  // Alias for fn in alternate syntax
    KeywordInt,
    KeywordString,
    KeywordBool,
    KeywordFloat,
    KeywordTrue,
    KeywordFalse,
    KeywordNone,
    KeywordAnd,
    KeywordOr,
    KeywordNot,

    // Literals
    Identifier,
    IntegerLiteral,
    FloatLiteral,
    StringLiteral,
    CharLiteral,

    // Operators
    Plus,
    Minus,
    Star,
    Slash,
    Percent,
    DoubleStar,     // **
    DoubleSlash,    // //
    Equals,         // =
    DoubleEquals,   // ==
    NotEquals,      // !=
    LessThan,
    GreaterThan,
    LessEqual,
    GreaterEqual,
    Arrow,          // ->
    Colon,
    Comma,
    Dot,
    LeftParen,
    RightParen,
    LeftBracket,
    RightBracket,
    LeftBrace,
    RightBrace,
    At,             // @ for decorators
    Question,       // ? for optional
    Ampersand,
    Pipe,
    Tilde,
    Caret,
    LeftShift,
    RightShift,

    // Assignment operators
    PlusAssign,
    MinusAssign,
    StarAssign,
    SlashAssign,
    PercentAssign,
    DoubleStarAssign,
    DoubleSlashAssign,
    AmpersandAssign,
    PipeAssign,
    CaretAssign,
    LeftShiftAssign,
    RightShiftAssign,
}