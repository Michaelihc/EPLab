using System.Globalization;
using System.Text;

namespace EPLab.Compiler.Syntax;

public enum TokenKind
{
    End,
    Identifier,
    Number,
    String,
    InterpolatedString,
    DateTime,
    LeftParen,
    RightParen,
    LeftBracket,
    RightBracket,
    LeftBrace,
    RightBrace,
    Comma,
    Dot,
    Colon,
    Question,
    Hash,
    Ampersand,
    Plus,
    Minus,
    Star,
    Slash,
    IntegerDivide,
    Percent,
    Assign,
    Equal,
    NotEqual,
    Less,
    Greater,
    LessOrEqual,
    GreaterOrEqual,
    ApproximatelyEqual,
    And,
    Or,
    Not,
    BitAnd,
    BitOr,
    BitXor,
    BitNot,
    ShiftLeft,
    ShiftRight,
    Coalesce,
    NullConditionalDot,
    Arrow,
    Unknown,
}

public readonly record struct SyntaxToken(TokenKind Kind, string Text, object? Value, SourceSpan Span);

public sealed class ExpressionLexer
{
    private readonly string text;
    private readonly string filePath;
    private readonly int line;
    private int position;

    public ExpressionLexer(string text, string filePath, int line)
    {
        this.text = text;
        this.filePath = filePath;
        this.line = line;
    }

    public IReadOnlyList<SyntaxToken> Lex(DiagnosticBag diagnostics)
    {
        List<SyntaxToken> tokens = new();
        while (true)
        {
            SyntaxToken token = NextToken(diagnostics);
            tokens.Add(token);
            if (token.Kind == TokenKind.End)
                return tokens;
        }
    }

    private SyntaxToken NextToken(DiagnosticBag diagnostics)
    {
        SkipWhitespace();
        int start = position;
        if (position >= text.Length)
            return Token(TokenKind.End, string.Empty, null, start);

        char current = text[position];
        if (current == '\'')
        {
            position = text.Length;
            return Token(TokenKind.End, string.Empty, null, start);
        }

        if (current == '$' && position + 1 < text.Length && IsQuote(text[position + 1]))
        {
            position++;
            return LexString(true, diagnostics, start);
        }

        if (IsQuote(current))
            return LexString(false, diagnostics, start);

        if (char.IsDigit(current) || (current is '.' or '。' && position + 1 < text.Length && char.IsDigit(text[position + 1])))
            return LexNumber(start, diagnostics);

        if (IsIdentifierStart(current))
            return LexIdentifier(start);

        if (current == '[' && LooksLikeDateTimeLiteral())
            return LexDateTime(start, diagnostics);

        string remaining = text[position..];
        foreach ((string spelling, TokenKind kind) in MultiCharacterTokens)
        {
            if (remaining.StartsWith(spelling, StringComparison.Ordinal))
            {
                position += spelling.Length;
                return Token(kind, spelling, null, start);
            }
        }

        position++;
        return current switch
        {
            '(' or '（' => Token(TokenKind.LeftParen, current.ToString(), null, start),
            ')' or '）' => Token(TokenKind.RightParen, current.ToString(), null, start),
            '[' or '【' => Token(TokenKind.LeftBracket, current.ToString(), null, start),
            ']' or '】' => Token(TokenKind.RightBracket, current.ToString(), null, start),
            '{' or '｛' => Token(TokenKind.LeftBrace, current.ToString(), null, start),
            '}' or '｝' => Token(TokenKind.RightBrace, current.ToString(), null, start),
            ',' or '，' => Token(TokenKind.Comma, current.ToString(), null, start),
            '.' or '。' => Token(TokenKind.Dot, current.ToString(), null, start),
            ':' or '：' => Token(TokenKind.Colon, current.ToString(), null, start),
            '?' or '？' => Token(TokenKind.Question, current.ToString(), null, start),
            '#' => Token(TokenKind.Hash, current.ToString(), null, start),
            '&' => Token(TokenKind.Ampersand, current.ToString(), null, start),
            '+' or '＋' => Token(TokenKind.Plus, current.ToString(), null, start),
            '-' or '－' => Token(TokenKind.Minus, current.ToString(), null, start),
            '*' or '×' or '＊' => Token(TokenKind.Star, current.ToString(), null, start),
            '/' or '÷' => Token(TokenKind.Slash, current.ToString(), null, start),
            '\\' or '＼' => Token(TokenKind.IntegerDivide, current.ToString(), null, start),
            '%' or '％' => Token(TokenKind.Percent, current.ToString(), null, start),
            '=' or '＝' => Token(TokenKind.Assign, current.ToString(), null, start),
            '<' or '＜' => Token(TokenKind.Less, current.ToString(), null, start),
            '>' or '＞' => Token(TokenKind.Greater, current.ToString(), null, start),
            '!' or '！' => Token(TokenKind.Not, current.ToString(), null, start),
            '|' => Token(TokenKind.BitOr, current.ToString(), null, start),
            '^' => Token(TokenKind.BitXor, current.ToString(), null, start),
            '~' => Token(TokenKind.BitNot, current.ToString(), null, start),
            _ => Unknown(current, start, diagnostics),
        };
    }

    private SyntaxToken LexIdentifier(int start)
    {
        position++;
        while (position < text.Length && IsIdentifierPart(text[position]))
            position++;

        string value = text[start..position];
        string normalized = value.ToUpperInvariant();
        TokenKind kind = normalized switch
        {
            "且" or "AND" => TokenKind.And,
            "或" or "OR" => TokenKind.Or,
            "非" or "NOT" => TokenKind.Not,
            "模" or "MOD" => TokenKind.Percent,
            "位与" => TokenKind.BitAnd,
            "位或" => TokenKind.BitOr,
            "位异或" => TokenKind.BitXor,
            "左移" => TokenKind.ShiftLeft,
            "右移" => TokenKind.ShiftRight,
            _ => TokenKind.Identifier,
        };
        return Token(kind, value, value, start);
    }

    private SyntaxToken LexNumber(int start, DiagnosticBag diagnostics)
    {
        if (position + 1 < text.Length && text[position] == '0' && text[position + 1] is 'x' or 'X')
        {
            position += 2;
            while (position < text.Length && Uri.IsHexDigit(text[position]))
                position++;
            string suffix = ReadNumericSuffix();
            string hexText = text[start..position];
            string digits = hexText[2..(hexText.Length - suffix.Length)];
            if (!IsIntegerSuffix(suffix))
                return BadNumber(hexText, start, diagnostics);
            if (ulong.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong hexValue))
            {
                try
                {
                    return Token(TokenKind.Number, hexText, BoxInteger(hexValue, suffix), start);
                }
                catch (OverflowException)
                {
                    return BadNumber(hexText, start, diagnostics);
                }
            }
            return BadNumber(hexText, start, diagnostics);
        }

        bool seenDot = false;
        bool seenExponent = false;
        while (position < text.Length)
        {
            char character = text[position];
            if (char.IsDigit(character))
            {
                position++;
                continue;
            }

            if (character is '.' or '。' && !seenDot && !seenExponent)
            {
                seenDot = true;
                position++;
                continue;
            }

            if (character is 'e' or 'E' && !seenExponent)
            {
                seenExponent = true;
                position++;
                if (position < text.Length && text[position] is '+' or '-' or '＋' or '－')
                    position++;
                continue;
            }

            break;
        }

        string numericSuffix = ReadNumericSuffix();
        string rawNumberText = text[start..position].Replace('。', '.').Replace('＋', '+').Replace('－', '-');
        string numberText = rawNumberText[..(rawNumberText.Length - numericSuffix.Length)];
        if (!double.TryParse(numberText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            return BadNumber(numberText, start, diagnostics);

        bool explicitlyReal = numericSuffix.Equals("F", StringComparison.OrdinalIgnoreCase)
            || numericSuffix.Equals("D", StringComparison.OrdinalIgnoreCase);
        bool realLiteral = seenDot || seenExponent || explicitlyReal;
        if (realLiteral ? !IsRealSuffix(numericSuffix) : !IsIntegerSuffix(numericSuffix))
            return BadNumber(rawNumberText, start, diagnostics);

        object boxed;
        if (numericSuffix.Equals("F", StringComparison.OrdinalIgnoreCase))
            boxed = (float)value;
        else if (numericSuffix.Equals("D", StringComparison.OrdinalIgnoreCase))
            boxed = value;
        else if (!seenDot && !seenExponent && ulong.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong integer))
        {
            try
            {
                boxed = BoxInteger(integer, numericSuffix);
            }
            catch (OverflowException)
            {
                return BadNumber(rawNumberText, start, diagnostics);
            }
        }
        else
            boxed = value;
        return Token(TokenKind.Number, rawNumberText, boxed, start);
    }

    private string ReadNumericSuffix()
    {
        int start = position;
        while (position < text.Length && text[position] is 'u' or 'U' or 'l' or 'L' or 'f' or 'F' or 'd' or 'D')
            position++;
        return text[start..position];
    }

    private static bool IsIntegerSuffix(string suffix)
        => suffix.ToUpperInvariant() is "" or "U" or "L" or "UL" or "LU";

    private static bool IsRealSuffix(string suffix)
        => suffix.ToUpperInvariant() is "" or "F" or "D";

    private static object BoxInteger(ulong value, string suffix)
    {
        bool unsigned = suffix.Contains('u') || suffix.Contains('U');
        bool wide = suffix.Contains('l') || suffix.Contains('L');
        if (unsigned && wide)
            return value;
        if (unsigned)
            return checked((uint)value);
        if (wide)
            return checked((long)value);
        if (value > long.MaxValue)
            return value;
        return (long)value;
    }

    private SyntaxToken LexString(bool interpolated, DiagnosticBag diagnostics, int tokenStart)
    {
        char opening = text[position++];
        StringBuilder value = new();
        bool terminated = false;
        while (position < text.Length)
        {
            char character = text[position++];
            if (IsMatchingQuote(opening, character))
            {
                if (position < text.Length && IsMatchingQuote(opening, text[position]))
                {
                    value.Append('"');
                    position++;
                    continue;
                }

                terminated = true;
                break;
            }

            if (character == '\\' && position < text.Length)
            {
                char escaped = text[position++];
                value.Append(escaped switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '\\' => '\\',
                    '"' => '"',
                    _ => escaped,
                });
                continue;
            }

            value.Append(character);
        }

        if (!terminated)
        {
            diagnostics.Add(new Diagnostic(
                "EPL1002",
                DiagnosticSeverity.Error,
                Span(tokenStart, Math.Max(1, position - tokenStart)),
                "This string has no closing quote.",
                "这个文本没有结束引号。",
                "Add a closing quote on the same line.",
                "请在同一行补上结束引号。"));
        }

        string raw = text[tokenStart..position];
        return Token(interpolated ? TokenKind.InterpolatedString : TokenKind.String, raw, value.ToString(), tokenStart);
    }

    private SyntaxToken LexDateTime(int start, DiagnosticBag diagnostics)
    {
        position++;
        while (position < text.Length && text[position] != ']')
            position++;
        bool terminated = position < text.Length;
        if (position < text.Length)
            position++;
        else
        {
            diagnostics.Add(new Diagnostic(
                "EPL1003",
                DiagnosticSeverity.Error,
                Span(start, position - start),
                "This date/time literal has no closing bracket.",
                "这个日期时间常量没有右方括号。"));
        }

        string value = text[start..position];
        if (terminated && !DateTime.TryParseExact(
                value.Trim('[', ']'),
                DateTimeFormats,
                CultureInfo.GetCultureInfo("zh-CN"),
                DateTimeStyles.AllowWhiteSpaces,
                out _))
        {
            diagnostics.Add(new Diagnostic(
                "EPL1004",
                DiagnosticSeverity.Error,
                Span(start, Math.Max(1, position - start)),
                $"'{value}' is not a valid date/time literal.",
                $"“{value}”不是有效的日期时间常量。",
                "Check the year, month, day, and time values.",
                "请检查年、月、日和时间数值。"));
        }
        return Token(TokenKind.DateTime, value, value, start);
    }

    private bool LooksLikeDateTimeLiteral()
    {
        int end = text.IndexOf(']', position + 1);
        if (end <= position + 1)
            return false;
        string value = text[(position + 1)..end].Trim();
        if (value.Length == 0 || !char.IsDigit(value[0]))
            return false;
        return value.Contains('年')
            || value.Contains('/')
            || value.Contains('-')
            || value.Length is 8 or 14;
    }

    private SyntaxToken BadNumber(string value, int start, DiagnosticBag diagnostics)
    {
        diagnostics.Add(new Diagnostic(
            "EPL1001",
            DiagnosticSeverity.Error,
            Span(start, value.Length),
            $"'{value}' is not a valid number.",
            $"“{value}”不是有效的数字。"));
        return Token(TokenKind.Number, value, 0L, start);
    }

    private SyntaxToken Unknown(char character, int start, DiagnosticBag diagnostics)
    {
        diagnostics.Add(new Diagnostic(
            "EPL1005",
            DiagnosticSeverity.Error,
            Span(start),
            $"The character '{character}' is not understood here.",
            $"这里无法识别字符“{character}”。"));
        return Token(TokenKind.Unknown, character.ToString(), null, start);
    }

    private SyntaxToken Token(TokenKind kind, string tokenText, object? value, int start)
        => new(kind, tokenText, value, Span(start, Math.Max(1, tokenText.Length)));

    private SourceSpan Span(int start, int length = 1) => new(filePath, line, start + 1, length);

    private void SkipWhitespace()
    {
        while (position < text.Length && char.IsWhiteSpace(text[position]))
            position++;
    }

    private static bool IsIdentifierStart(char character)
        => character == '_' || char.IsLetter(character) || CharUnicodeInfo.GetUnicodeCategory(character) is UnicodeCategory.LetterNumber;

    private static bool IsIdentifierPart(char character)
        => IsIdentifierStart(character) || char.IsDigit(character);

    private static bool IsQuote(char character) => character is '"' or '“' or '”';

    private static bool IsMatchingQuote(char opening, char candidate)
        => opening == '"' ? candidate == '"' : candidate is '“' or '”';

    private static readonly string[] DateTimeFormats =
    {
        "yyyy年M月d日H时m分s秒", "yyyy年M月d日",
        "yyyy/M/d/H/m/s", "yyyy/M/d/H:m:s", "yyyy/M/d H:m:s", "yyyy/M/d",
        "yyyy-M-d-H-m-s", "yyyy-M-d-H:m:s", "yyyy-M-d H:m:s", "yyyy-M-d",
        "yyyyMMddHHmmss", "yyyyMMdd",
    };

    private static readonly (string Text, TokenKind Kind)[] MultiCharacterTokens =
    {
        ("??", TokenKind.Coalesce),
        ("?.", TokenKind.NullConditionalDot),
        ("？。", TokenKind.NullConditionalDot),
        ("=>", TokenKind.Arrow),
        ("==", TokenKind.Equal),
        ("!=", TokenKind.NotEqual),
        ("<>", TokenKind.NotEqual),
        ("≠", TokenKind.NotEqual),
        ("<=", TokenKind.LessOrEqual),
        ("≤", TokenKind.LessOrEqual),
        (">=", TokenKind.GreaterOrEqual),
        ("≥", TokenKind.GreaterOrEqual),
        ("~=", TokenKind.ApproximatelyEqual),
        ("≈", TokenKind.ApproximatelyEqual),
        ("&&", TokenKind.And),
        ("||", TokenKind.Or),
        ("<<", TokenKind.ShiftLeft),
        (">>", TokenKind.ShiftRight),
    };
}

internal static class SourceTextUtilities
{
    public static string StripComment(string text)
    {
        bool inString = false;
        char opening = '\0';
        int nesting = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];
            if (inString)
            {
                if (character == '\\')
                {
                    i++;
                    continue;
                }

                if (IsMatchingQuote(opening, character))
                    inString = false;
                continue;
            }

            if (character is '"' or '“' or '”')
            {
                inString = true;
                opening = character;
                continue;
            }

            if (character is '(' or '（' or '[' or '【' or '{' or '｛')
                nesting++;
            else if (character is ')' or '）' or ']' or '】' or '}' or '｝')
                nesting = Math.Max(0, nesting - 1);
            else if (character == '\'' && nesting == 0)
                return text[..i];
        }

        return text;
    }

    public static IReadOnlyList<string> SplitFields(string text)
    {
        List<string> fields = new();
        int start = 0;
        int nesting = 0;
        int genericNesting = 0;
        bool inString = false;
        char opening = '\0';
        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];
            if (inString)
            {
                if (character == '\\')
                {
                    i++;
                    continue;
                }

                if (IsMatchingQuote(opening, character))
                    inString = false;
                continue;
            }

            if (character is '"' or '“' or '”')
            {
                inString = true;
                opening = character;
            }
            else if (character is '(' or '（' or '[' or '【' or '{' or '｛')
                nesting++;
            else if (character is ')' or '）' or ']' or '】' or '}' or '｝')
                nesting = Math.Max(0, nesting - 1);
            else if (character is '<' or '《'
                && (genericNesting > 0 || IsPlausibleGenericOpen(text, i)))
                genericNesting++;
            else if (character is '>' or '》' && genericNesting > 0)
                genericNesting--;
            else if (character is ',' or '，' && nesting == 0 && genericNesting == 0)
            {
                fields.Add(text[start..i].Trim());
                start = i + 1;
            }
        }

        fields.Add(text[start..].Trim());
        return fields;
    }

    private static bool IsPlausibleGenericOpen(string text, int openIndex)
    {
        int nameEnd = openIndex - 1;
        while (nameEnd >= 0 && char.IsWhiteSpace(text[nameEnd]))
            nameEnd--;
        if (nameEnd < 0 || !IsTypeNamePart(text[nameEnd]))
            return false;

        bool nameHasLetter = false;
        for (int i = nameEnd; i >= 0 && IsTypeNamePart(text[i]); i--)
            nameHasLetter |= char.IsLetter(text[i]) || text[i] == '_';
        if (!nameHasLetter)
            return false;

        int depth = 1;
        bool hasArgumentComma = false;
        for (int i = openIndex + 1; i < text.Length; i++)
        {
            char character = text[i];
            if (character is '"' or '“' or '”'
                || character is '=' or '＝' or '+' or '＋' or '-' or '－'
                or '*' or '＊' or '/' or '／' or '%' or '&' or '|' or '!')
                return false;

            if (character is '<' or '《')
            {
                depth++;
                continue;
            }

            if (character is '>' or '》')
            {
                depth--;
                if (depth != 0)
                    continue;

                int next = i + 1;
                while (next < text.Length && char.IsWhiteSpace(text[next]))
                    next++;
                return hasArgumentComma
                    && (next >= text.Length || !IsTypeNamePart(text[next]));
            }

            if (character is ',' or '，')
                hasArgumentComma = true;
        }

        return false;
    }

    private static bool IsTypeNamePart(char character)
        => char.IsLetterOrDigit(character) || character is '_' or '.' or ':' or '`';

    public static string Unquote(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && value[0] is '"' or '“' or '”' && value[^1] is '"' or '“' or '”')
            return value[1..^1].Replace("\\\"", "\"");
        return value;
    }

    private static bool IsMatchingQuote(char opening, char candidate)
        => opening == '"' ? candidate == '"' : candidate is '“' or '”';
}
