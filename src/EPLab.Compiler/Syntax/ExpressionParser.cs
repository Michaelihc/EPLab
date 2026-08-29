namespace EPLab.Compiler.Syntax;

public sealed class ExpressionParser
{
    private readonly IReadOnlyList<SyntaxToken> tokens;
    private readonly DiagnosticBag diagnostics;
    private int position;

    public ExpressionParser(string text, string filePath, int line, DiagnosticBag diagnostics)
    {
        this.diagnostics = diagnostics;
        tokens = new ExpressionLexer(text, filePath, line).Lex(diagnostics);
    }

    public ExpressionSyntax Parse()
    {
        ExpressionSyntax expression = ParseBinaryExpression(0);
        if (Current.Kind != TokenKind.End)
        {
            diagnostics.Add(new Diagnostic(
                "EPL1101",
                DiagnosticSeverity.Error,
                Current.Span,
                $"Unexpected text '{Current.Text}' after this expression.",
                $"表达式后出现了意外内容“{Current.Text}”。"));
        }

        return expression;
    }

    public static ExpressionSyntax ParseOrMissing(string text, string filePath, int line, DiagnosticBag diagnostics)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new MissingExpressionSyntax(new SourceSpan(filePath, line));
        return new ExpressionParser(text, filePath, line, diagnostics).Parse();
    }

    private ExpressionSyntax ParseBinaryExpression(int parentPrecedence)
    {
        ExpressionSyntax left;
        int unaryPrecedence = GetUnaryPrecedence(Current.Kind);
        if (unaryPrecedence != 0 && unaryPrecedence >= parentPrecedence)
        {
            SyntaxToken operatorToken = NextToken();
            ExpressionSyntax operand = ParseBinaryExpression(unaryPrecedence);
            left = new UnaryExpressionSyntax(NormalizeOperator(operatorToken), operand, operatorToken.Span);
        }
        else
        {
            left = ParsePostfixExpression();
        }

        while (true)
        {
            int precedence = GetBinaryPrecedence(Current.Kind);
            if (precedence == 0 || precedence <= parentPrecedence)
                break;

            SyntaxToken operatorToken = NextToken();
            ExpressionSyntax right = ParseBinaryExpression(precedence);
            left = new BinaryExpressionSyntax(left, NormalizeOperator(operatorToken), right, operatorToken.Span);
        }

        return left;
    }

    private ExpressionSyntax ParsePostfixExpression()
    {
        ExpressionSyntax expression = ParsePrimaryExpression();
        while (true)
        {
            if (Current.Kind is TokenKind.Dot or TokenKind.NullConditionalDot)
            {
                bool conditional = NextToken().Kind == TokenKind.NullConditionalDot;
                SyntaxToken member = Match(TokenKind.Identifier);
                expression = new MemberAccessExpressionSyntax(expression, member.Text, conditional, member.Span);
                continue;
            }

            if (Current.Kind == TokenKind.LeftParen)
            {
                IReadOnlyList<ArgumentSyntax> arguments = ParseArguments();
                expression = new CallExpressionSyntax(expression, arguments, expression.Span);
                continue;
            }

            if (Current.Kind == TokenKind.LeftBracket)
            {
                SyntaxToken open = NextToken();
                List<ExpressionSyntax> indices = new();
                if (Current.Kind != TokenKind.RightBracket)
                {
                    do
                    {
                        indices.Add(ParseBinaryExpression(0));
                        if (Current.Kind != TokenKind.Comma)
                            break;
                        NextToken();
                    }
                    while (true);
                }

                Match(TokenKind.RightBracket);
                expression = new IndexExpressionSyntax(expression, indices, open.Span);
                continue;
            }

            break;
        }

        return expression;
    }

    private ExpressionSyntax ParsePrimaryExpression()
    {
        SyntaxToken token = Current;
        switch (token.Kind)
        {
            case TokenKind.LeftParen:
                NextToken();
                ExpressionSyntax nested = ParseBinaryExpression(0);
                Match(TokenKind.RightParen);
                return nested;

            case TokenKind.LeftBrace:
                return ParseArrayLiteral();

            case TokenKind.String:
                NextToken();
                return new LiteralExpressionSyntax(token.Value, token.Text, token.Span);

            case TokenKind.InterpolatedString:
                NextToken();
                return ParseInterpolatedString(token);

            case TokenKind.Number:
                NextToken();
                return new LiteralExpressionSyntax(token.Value, token.Text, token.Span);

            case TokenKind.DateTime:
                NextToken();
                return new LiteralExpressionSyntax(token.Value, token.Text, token.Span);

            case TokenKind.Hash:
                return ParseConstant();

            case TokenKind.Ampersand:
                NextToken();
                SyntaxToken method = Match(TokenKind.Identifier);
                return new DelegateExpressionSyntax(method.Text, token.Span);

            case TokenKind.Identifier:
                NextToken();
                return token.Text.ToUpperInvariant() switch
                {
                    "真" or "TRUE" => new LiteralExpressionSyntax(true, token.Text, token.Span),
                    "假" or "FALSE" => new LiteralExpressionSyntax(false, token.Text, token.Span),
                    "空" or "空对象" or "NULL" => new LiteralExpressionSyntax(null, token.Text, token.Span),
                    _ => new NameExpressionSyntax(token.Text, token.Span),
                };

            default:
                diagnostics.Add(new Diagnostic(
                    "EPL1102",
                    DiagnosticSeverity.Error,
                    token.Span,
                    $"Expected an expression, but found '{token.Text}'.",
                    $"这里需要一个表达式，但找到了“{token.Text}”。"));
                NextToken();
                return new MissingExpressionSyntax(token.Span);
        }
    }

    private InterpolatedStringExpressionSyntax ParseInterpolatedString(SyntaxToken token)
    {
        string value = (string?)token.Value ?? string.Empty;
        List<InterpolatedPartSyntax> parts = new();
        System.Text.StringBuilder text = new();
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] == '{' && index + 1 < value.Length && value[index + 1] == '{')
            {
                text.Append('{');
                index++;
                continue;
            }
            if (value[index] == '}' && index + 1 < value.Length && value[index + 1] == '}')
            {
                text.Append('}');
                index++;
                continue;
            }
            if (value[index] == '}')
            {
                diagnostics.Add(new Diagnostic(
                    "EPL1104",
                    DiagnosticSeverity.Error,
                    token.Span,
                    "An interpolation closing brace has no opening brace.",
                    "插值文本的右大括号没有对应的左大括号。"));
                text.Append('}');
                continue;
            }
            if (value[index] != '{')
            {
                text.Append(value[index]);
                continue;
            }

            if (text.Length > 0)
            {
                parts.Add(new InterpolatedTextSyntax(text.ToString(), token.Span));
                text.Clear();
            }
            int close = FindInterpolationClose(value, index + 1);
            if (close < 0)
            {
                diagnostics.Add(new Diagnostic(
                    "EPL1105",
                    DiagnosticSeverity.Error,
                    token.Span,
                    "An interpolation expression has no closing brace.",
                    "插值表达式缺少右大括号。"));
                text.Append(value[index..]);
                break;
            }

            string hole = value[(index + 1)..close];
            int formatSeparator = FindTopLevelFormatSeparator(hole);
            string expressionText = formatSeparator < 0 ? hole : hole[..formatSeparator];
            string? format = formatSeparator < 0 ? null : hole[(formatSeparator + 1)..];
            if (string.IsNullOrWhiteSpace(expressionText))
            {
                diagnostics.Add(new Diagnostic(
                    "EPL1106",
                    DiagnosticSeverity.Error,
                    token.Span,
                    "An interpolation expression cannot be empty.",
                    "插值表达式不能为空。"));
                parts.Add(new InterpolationSyntax(new MissingExpressionSyntax(token.Span), format, token.Span));
            }
            else
            {
                ExpressionSyntax expression = new ExpressionParser(
                    expressionText,
                    token.Span.FilePath,
                    token.Span.Line,
                    diagnostics).Parse();
                parts.Add(new InterpolationSyntax(expression, format, token.Span));
            }
            index = close;
        }

        if (text.Length > 0)
            parts.Add(new InterpolatedTextSyntax(text.ToString(), token.Span));
        return new InterpolatedStringExpressionSyntax(parts, token.Span);
    }

    private static int FindInterpolationClose(string value, int start)
    {
        int nesting = 0;
        bool inString = false;
        char opening = '\0';
        for (int index = start; index < value.Length; index++)
        {
            char character = value[index];
            if (inString)
            {
                if (character == '\\') { index++; continue; }
                if (opening == '"' ? character == '"' : character is '“' or '”') inString = false;
                continue;
            }
            if (character is '"' or '“' or '”') { inString = true; opening = character; continue; }
            if (character is '(' or '（' or '[' or '【' or '{' or '｛') nesting++;
            else if (character is ')' or '）' or ']' or '】' or '}' or '｝')
            {
                if (character == '}' && nesting == 0) return index;
                nesting = Math.Max(0, nesting - 1);
            }
        }
        return -1;
    }

    private static int FindTopLevelFormatSeparator(string value)
    {
        int nesting = 0;
        bool inString = false;
        char opening = '\0';
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (inString)
            {
                if (character == '\\') { index++; continue; }
                if (opening == '"' ? character == '"' : character is '“' or '”') inString = false;
                continue;
            }
            if (character is '"' or '“' or '”') { inString = true; opening = character; continue; }
            if (character is '(' or '（' or '[' or '【' or '{' or '｛') nesting++;
            else if (character is ')' or '）' or ']' or '】' or '}' or '｝') nesting = Math.Max(0, nesting - 1);
            else if (character is ':' or '：' && nesting == 0) return index;
        }
        return -1;
    }

    private ExpressionSyntax ParseConstant()
    {
        SyntaxToken hash = Match(TokenKind.Hash);
        SyntaxToken first = Match(TokenKind.Identifier);
        if (Current.Kind == TokenKind.Dot)
        {
            NextToken();
            SyntaxToken second = Match(TokenKind.Identifier);
            return new ConstantExpressionSyntax(second.Text, first.Text, hash.Span);
        }

        return new ConstantExpressionSyntax(first.Text, null, hash.Span);
    }

    private ExpressionSyntax ParseArrayLiteral()
    {
        SyntaxToken open = Match(TokenKind.LeftBrace);
        List<ExpressionSyntax> items = new();
        if (Current.Kind != TokenKind.RightBrace)
        {
            do
            {
                if (Current.Kind == TokenKind.Comma)
                    items.Add(new MissingExpressionSyntax(Current.Span));
                else
                    items.Add(ParseBinaryExpression(0));

                if (Current.Kind != TokenKind.Comma)
                    break;
                NextToken();
            }
            while (true);
        }

        Match(TokenKind.RightBrace);
        return new ArrayLiteralExpressionSyntax(items, open.Span);
    }

    private IReadOnlyList<ArgumentSyntax> ParseArguments()
    {
        Match(TokenKind.LeftParen);
        List<ArgumentSyntax> arguments = new();
        if (Current.Kind == TokenKind.RightParen)
        {
            NextToken();
            return arguments;
        }

        while (true)
        {
            if (Current.Kind == TokenKind.Comma)
            {
                arguments.Add(new ArgumentSyntax(null, new MissingExpressionSyntax(Current.Span), ParameterModifiers.None, Current.Span));
            }
            else
            {
                ParameterModifiers modifiers = ParameterModifiers.None;
                if (Current.Kind == TokenKind.Identifier && Current.Text is "参考" or "传址" or "输出")
                {
                    modifiers = Current.Text == "输出" ? ParameterModifiers.Out : ParameterModifiers.ByReference;
                    NextToken();
                }

                string? name = null;
                if (Current.Kind == TokenKind.Identifier && Peek(1).Kind == TokenKind.Colon)
                {
                    name = NextToken().Text;
                    NextToken();
                }

                SourceSpan span = Current.Span;
                ExpressionSyntax value = ParseBinaryExpression(0);
                arguments.Add(new ArgumentSyntax(name, value, modifiers, span));
            }

            if (Current.Kind != TokenKind.Comma)
                break;
            NextToken();
            if (Current.Kind == TokenKind.RightParen)
                arguments.Add(new ArgumentSyntax(null, new MissingExpressionSyntax(Current.Span), ParameterModifiers.None, Current.Span));
        }

        Match(TokenKind.RightParen);
        return arguments;
    }

    private SyntaxToken Match(TokenKind expected)
    {
        if (Current.Kind == expected)
            return NextToken();

        diagnostics.Add(new Diagnostic(
            "EPL1103",
            DiagnosticSeverity.Error,
            Current.Span,
            $"Expected {expected}, but found '{Current.Text}'.",
            $"这里需要 {expected}，但找到了“{Current.Text}”。"));
        return new SyntaxToken(expected, string.Empty, null, Current.Span);
    }

    private SyntaxToken NextToken()
    {
        SyntaxToken current = Current;
        if (position < tokens.Count - 1)
            position++;
        return current;
    }

    private SyntaxToken Peek(int offset)
    {
        int index = Math.Min(position + offset, tokens.Count - 1);
        return tokens[index];
    }

    private SyntaxToken Current => Peek(0);

    private static int GetUnaryPrecedence(TokenKind kind) => kind switch
    {
        TokenKind.Plus or TokenKind.Minus or TokenKind.Not or TokenKind.BitNot => 14,
        _ => 0,
    };

    private static int GetBinaryPrecedence(TokenKind kind) => kind switch
    {
        TokenKind.Star or TokenKind.Slash => 13,
        TokenKind.IntegerDivide => 12,
        TokenKind.Percent => 11,
        TokenKind.Plus or TokenKind.Minus => 10,
        TokenKind.ShiftLeft or TokenKind.ShiftRight => 9,
        TokenKind.Less or TokenKind.LessOrEqual or TokenKind.Greater or TokenKind.GreaterOrEqual
            or TokenKind.Assign or TokenKind.Equal or TokenKind.NotEqual or TokenKind.ApproximatelyEqual => 8,
        TokenKind.BitAnd or TokenKind.Ampersand => 6,
        TokenKind.BitXor => 5,
        TokenKind.BitOr => 4,
        TokenKind.And => 3,
        TokenKind.Or => 2,
        TokenKind.Coalesce => 1,
        _ => 0,
    };

    private static string NormalizeOperator(SyntaxToken token) => token.Kind switch
    {
        TokenKind.Plus => "+",
        TokenKind.Minus => "-",
        TokenKind.Star => "*",
        TokenKind.Slash => "/",
        TokenKind.IntegerDivide => "\\",
        TokenKind.Percent => "%",
        TokenKind.Assign or TokenKind.Equal => "==",
        TokenKind.NotEqual => "!=",
        TokenKind.Less => "<",
        TokenKind.Greater => ">",
        TokenKind.LessOrEqual => "<=",
        TokenKind.GreaterOrEqual => ">=",
        TokenKind.ApproximatelyEqual => "~=",
        TokenKind.And => "&&",
        TokenKind.Or => "||",
        TokenKind.Not => "!",
        TokenKind.BitAnd or TokenKind.Ampersand => "&",
        TokenKind.BitOr => "|",
        TokenKind.BitXor => "^",
        TokenKind.BitNot => "~",
        TokenKind.ShiftLeft => "<<",
        TokenKind.ShiftRight => ">>",
        TokenKind.Coalesce => "??",
        _ => token.Text,
    };
}
