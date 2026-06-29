namespace Snek.Core.Analysis;

public enum TypeKind
{
    Unknown,    // Type could not be determined
    Any,        // Dynamic or unknown type (like "Any" in the old system)
    I32,        // 32-bit integer
    F64,        // 64-bit float
    Bool,       // boolean
    Str,        // string
    Char,       // character
    NoneType,   // None value
    Function,   // Represents a function (metadata contains FunctionType)
    List,       // Represents a dynamic list / array
    Class,      // Represents a user-defined class
}