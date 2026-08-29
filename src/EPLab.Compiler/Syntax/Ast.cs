namespace EPLab.Compiler.Syntax;

public abstract record SyntaxNode(SourceSpan Span);

public sealed record CompilationUnitSyntax(
    string FilePath,
    int Version,
    IReadOnlyList<ExtensionSyntax> Extensions,
    IReadOnlyList<string> NamespaceImports,
    IReadOnlyList<string> AssemblyImports,
    PluginSyntax? Plugin,
    IReadOnlyList<ConfigItemSyntax> ConfigItems,
    IReadOnlyList<EventBindingSyntax> EventBindings,
    IReadOnlyList<CommandBindingSyntax> CommandBindings,
    IReadOnlyList<DeclarationSyntax> Declarations,
    SourceSpan Span) : SyntaxNode(Span);

public sealed record ExtensionSyntax(string Name, int Version, SourceSpan Span) : SyntaxNode(Span);

public sealed record PluginSyntax(
    string ClassName,
    string? ConfigClassName,
    string Name,
    string Description,
    string Author,
    string Version,
    string? RequiredApiVersion,
    SourceSpan Span) : SyntaxNode(Span);

public sealed record ConfigItemSyntax(
    string Name,
    TypeSyntax Type,
    ExpressionSyntax DefaultValue,
    string? SerializedName,
    string? DescriptionEnglish,
    string? DescriptionChinese,
    ExpressionSyntax? Minimum,
    ExpressionSyntax? Maximum,
    SourceSpan Span) : SyntaxNode(Span);

public sealed record EventBindingSyntax(
    string EventPath,
    string HandlerName,
    SourceSpan Span) : SyntaxNode(Span);

public enum CommandKind
{
    RemoteAdmin,
    PlayerConsole,
    GameConsole,
}

public sealed record CommandBindingSyntax(
    CommandKind Kind,
    string Name,
    IReadOnlyList<string> Aliases,
    string Description,
    string HandlerName,
    string? Permission,
    SourceSpan Span) : SyntaxNode(Span);

public abstract record DeclarationSyntax(SourceSpan Span) : SyntaxNode(Span);

public sealed record ClassDeclarationSyntax(
    string Name,
    TypeSyntax? BaseType,
    Visibility Visibility,
    DeclarationModifiers Modifiers,
    IReadOnlyList<FieldDeclarationSyntax> Fields,
    IReadOnlyList<MethodDeclarationSyntax> Methods,
    SourceSpan Span) : DeclarationSyntax(Span);

public sealed record StructDeclarationSyntax(
    string Name,
    Visibility Visibility,
    IReadOnlyList<FieldDeclarationSyntax> Members,
    SourceSpan Span) : DeclarationSyntax(Span);

public sealed record EnumDeclarationSyntax(
    string Name,
    Visibility Visibility,
    IReadOnlyList<EnumMemberSyntax> Members,
    SourceSpan Span) : DeclarationSyntax(Span);

public sealed record EnumMemberSyntax(string Name, ExpressionSyntax? Value, SourceSpan Span) : SyntaxNode(Span);

public sealed record FieldDeclarationSyntax(
    string Name,
    TypeSyntax Type,
    Visibility Visibility,
    DeclarationModifiers Modifiers,
    ArrayShapeSyntax? ArrayShape,
    ExpressionSyntax? Initializer,
    SourceSpan Span) : DeclarationSyntax(Span);

public sealed record ConstantDeclarationSyntax(
    string Name,
    ExpressionSyntax Value,
    Visibility Visibility,
    SourceSpan Span) : DeclarationSyntax(Span);

public sealed record DllDeclarationSyntax(
    string Name,
    TypeSyntax ReturnType,
    string LibraryName,
    string EntryPoint,
    Visibility Visibility,
    IReadOnlyList<ParameterSyntax> Parameters,
    SourceSpan Span) : DeclarationSyntax(Span);

public sealed record MethodDeclarationSyntax(
    string Name,
    TypeSyntax ReturnType,
    Visibility Visibility,
    DeclarationModifiers Modifiers,
    IReadOnlyList<ParameterSyntax> Parameters,
    IReadOnlyList<LocalVariableSyntax> Locals,
    IReadOnlyList<StatementSyntax> Statements,
    SourceSpan Span) : DeclarationSyntax(Span);

public sealed record ParameterSyntax(
    string Name,
    TypeSyntax Type,
    ParameterModifiers Modifiers,
    ExpressionSyntax? DefaultValue,
    SourceSpan Span) : SyntaxNode(Span);

public sealed record LocalVariableSyntax(
    string Name,
    TypeSyntax Type,
    DeclarationModifiers Modifiers,
    ArrayShapeSyntax? ArrayShape,
    ExpressionSyntax? Initializer,
    SourceSpan Span) : SyntaxNode(Span);

public sealed record TypeSyntax(string Text, bool IsArray, bool IsNullable, SourceSpan Span) : SyntaxNode(Span);

public sealed record ArrayShapeSyntax(IReadOnlyList<ExpressionSyntax> Dimensions, bool Dynamic, SourceSpan Span) : SyntaxNode(Span);

public enum Visibility
{
    Private,
    Internal,
    Public,
    Protected,
}

[Flags]
public enum DeclarationModifiers
{
    None = 0,
    Static = 1 << 0,
    ReadOnly = 1 << 1,
    Virtual = 1 << 2,
    Override = 1 << 3,
    Abstract = 1 << 4,
    Sealed = 1 << 5,
    Async = 1 << 6,
    Partial = 1 << 7,
    Constructor = 1 << 8,
}

[Flags]
public enum ParameterModifiers
{
    None = 0,
    Optional = 1 << 0,
    ByReference = 1 << 1,
    Out = 1 << 2,
    Array = 1 << 3,
    Params = 1 << 4,
}

public abstract record StatementSyntax(SourceSpan Span) : SyntaxNode(Span);

public sealed record EmptyStatementSyntax(SourceSpan Span) : StatementSyntax(Span);

public sealed record ExpressionStatementSyntax(ExpressionSyntax Expression, SourceSpan Span) : StatementSyntax(Span);

public sealed record AssignmentStatementSyntax(ExpressionSyntax Target, ExpressionSyntax Value, SourceSpan Span) : StatementSyntax(Span);

public sealed record IfStatementSyntax(
    ExpressionSyntax Condition,
    IReadOnlyList<StatementSyntax> ThenStatements,
    IReadOnlyList<StatementSyntax> ElseStatements,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record WhileStatementSyntax(
    ExpressionSyntax Condition,
    IReadOnlyList<StatementSyntax> Statements,
    bool TestAfterBody,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record CountLoopStatementSyntax(
    ExpressionSyntax Count,
    ExpressionSyntax? Counter,
    IReadOnlyList<StatementSyntax> Statements,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record RangeLoopStatementSyntax(
    ExpressionSyntax Start,
    ExpressionSyntax End,
    ExpressionSyntax? Step,
    ExpressionSyntax? Variable,
    IReadOnlyList<StatementSyntax> Statements,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record ForEachStatementSyntax(
    string VariableName,
    TypeSyntax? VariableType,
    ExpressionSyntax Collection,
    IReadOnlyList<StatementSyntax> Statements,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record ConditionalBranchSyntax(
    ExpressionSyntax Condition,
    IReadOnlyList<StatementSyntax> Statements,
    SourceSpan Span) : SyntaxNode(Span);

public sealed record ChooseStatementSyntax(
    IReadOnlyList<ConditionalBranchSyntax> Branches,
    IReadOnlyList<StatementSyntax> DefaultStatements,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record CatchClauseSyntax(
    string VariableName,
    TypeSyntax ExceptionType,
    IReadOnlyList<StatementSyntax> Statements,
    SourceSpan Span) : SyntaxNode(Span);

public sealed record TryStatementSyntax(
    IReadOnlyList<StatementSyntax> TryStatements,
    IReadOnlyList<CatchClauseSyntax> Catches,
    IReadOnlyList<StatementSyntax> FinallyStatements,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record TemporarySetStatementSyntax(
    ExpressionSyntax Target,
    ExpressionSyntax Value,
    IReadOnlyList<StatementSyntax> Statements,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record LockStatementSyntax(
    ExpressionSyntax Target,
    IReadOnlyList<StatementSyntax> Statements,
    SourceSpan Span) : StatementSyntax(Span);

public sealed record UsingStatementSyntax(
    string VariableName,
    ExpressionSyntax Value,
    IReadOnlyList<StatementSyntax> Statements,
    SourceSpan Span) : StatementSyntax(Span);

public abstract record ExpressionSyntax(SourceSpan Span) : SyntaxNode(Span);

public sealed record MissingExpressionSyntax(SourceSpan Span) : ExpressionSyntax(Span);

public sealed record LiteralExpressionSyntax(object? Value, string RawText, SourceSpan Span) : ExpressionSyntax(Span);

public sealed record InterpolatedStringExpressionSyntax(
    IReadOnlyList<InterpolatedPartSyntax> Parts,
    SourceSpan Span) : ExpressionSyntax(Span);

public abstract record InterpolatedPartSyntax(SourceSpan Span) : SyntaxNode(Span);

public sealed record InterpolatedTextSyntax(string Text, SourceSpan Span) : InterpolatedPartSyntax(Span);

public sealed record InterpolationSyntax(
    ExpressionSyntax Expression,
    string? Format,
    SourceSpan Span) : InterpolatedPartSyntax(Span);

public sealed record NameExpressionSyntax(string Name, SourceSpan Span) : ExpressionSyntax(Span);

public sealed record ConstantExpressionSyntax(string Name, string? TypeName, SourceSpan Span) : ExpressionSyntax(Span);

public sealed record UnaryExpressionSyntax(string Operator, ExpressionSyntax Operand, SourceSpan Span) : ExpressionSyntax(Span);

public sealed record BinaryExpressionSyntax(ExpressionSyntax Left, string Operator, ExpressionSyntax Right, SourceSpan Span) : ExpressionSyntax(Span);

public sealed record MemberAccessExpressionSyntax(ExpressionSyntax Target, string MemberName, bool NullConditional, SourceSpan Span) : ExpressionSyntax(Span);

public sealed record CallExpressionSyntax(ExpressionSyntax Target, IReadOnlyList<ArgumentSyntax> Arguments, SourceSpan Span) : ExpressionSyntax(Span);

public sealed record IndexExpressionSyntax(ExpressionSyntax Target, IReadOnlyList<ExpressionSyntax> Indices, SourceSpan Span) : ExpressionSyntax(Span);

public sealed record ArrayLiteralExpressionSyntax(IReadOnlyList<ExpressionSyntax> Items, SourceSpan Span) : ExpressionSyntax(Span);

public sealed record ArgumentSyntax(string? Name, ExpressionSyntax Value, ParameterModifiers Modifiers, SourceSpan Span) : SyntaxNode(Span);

public sealed record TypeTestExpressionSyntax(ExpressionSyntax Value, TypeSyntax Type, bool Negated, SourceSpan Span) : ExpressionSyntax(Span);

public sealed record ConversionExpressionSyntax(TypeSyntax Type, ExpressionSyntax Value, SourceSpan Span) : ExpressionSyntax(Span);

public sealed record NewExpressionSyntax(TypeSyntax Type, IReadOnlyList<ArgumentSyntax> Arguments, SourceSpan Span) : ExpressionSyntax(Span);

public sealed record DelegateExpressionSyntax(string MethodName, SourceSpan Span) : ExpressionSyntax(Span);
