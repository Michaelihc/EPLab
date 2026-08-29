namespace EPLab.Compiler;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

public readonly record struct SourceSpan(
    string FilePath,
    int Line,
    int Column = 1,
    int Length = 1)
{
    public override string ToString() => $"{FilePath}({Line},{Column})";
}

public sealed record Diagnostic(
    string Id,
    DiagnosticSeverity Severity,
    SourceSpan Span,
    string EnglishMessage,
    string ChineseMessage,
    string? SuggestionEnglish = null,
    string? SuggestionChinese = null)
{
    public string Format(bool chinese)
    {
        string message = chinese ? ChineseMessage : EnglishMessage;
        string? suggestion = chinese ? SuggestionChinese : SuggestionEnglish;
        return suggestion is null
            ? $"{Id} {Span}: {message}"
            : $"{Id} {Span}: {message} {suggestion}";
    }
}

public sealed class DiagnosticBag : IReadOnlyCollection<Diagnostic>
{
    private readonly List<Diagnostic> diagnostics = new();

    public int Count => diagnostics.Count;

    public bool HasErrors => diagnostics.Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    public void Add(Diagnostic diagnostic) => diagnostics.Add(diagnostic);

    public void AddRange(IEnumerable<Diagnostic> values) => diagnostics.AddRange(values);

    public IEnumerator<Diagnostic> GetEnumerator() => diagnostics.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
