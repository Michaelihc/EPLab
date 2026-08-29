using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using EPLab.Compiler.CodeGen;
using EPLab.Compiler.Semantics;
using EPLab.Compiler.Syntax;

namespace EPLab.Compiler.Build;

public sealed partial class ProjectCompiler
{
    public async Task<CompilationResult> CompileAsync(
        string projectFile,
        CompilerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CompilerOptions();
        DiagnosticBag diagnostics = new();
        string absoluteProjectFile = Path.GetFullPath(projectFile);
        string projectDirectory = Path.GetDirectoryName(absoluteProjectFile)
            ?? throw new InvalidOperationException("The project file needs a parent directory.");

        EPLabProject project;
        try
        {
            project = EPLabProject.Load(absoluteProjectFile);
        }
        catch (Exception exception)
        {
            diagnostics.Add(new Diagnostic(
                "EPL0001",
                DiagnosticSeverity.Error,
                new SourceSpan(absoluteProjectFile, 1),
                $"Could not read the project: {exception.Message}",
                $"无法读取项目：{exception.Message}"));
            return Result(false, diagnostics, null, null, null, string.Empty);
        }

        if (project.Format != 1)
        {
            diagnostics.Add(new Diagnostic(
                "EPL0002",
                DiagnosticSeverity.Error,
                new SourceSpan(absoluteProjectFile, 1),
                $"Project format {project.Format} is not supported.",
                $"不支持第 {project.Format} 版项目格式。"));
            return Result(false, diagnostics, null, null, null, string.Empty);
        }

        if (!SupportedTargets.Contains(project.Target))
        {
            diagnostics.Add(new Diagnostic(
                "EPL0006",
                DiagnosticSeverity.Error,
                new SourceSpan(absoluteProjectFile, 1),
                $"Unknown target '{project.Target}'. Use labapi-net48, library-net48, library-net8, or syntax-only.",
                $"未知目标“{project.Target}”。请使用 labapi-net48、library-net48、library-net8 或 syntax-only。"));
            return Result(false, diagnostics, null, null, null, string.Empty);
        }

        if (!ProjectValidator.Validate(project, absoluteProjectFile, projectDirectory, options, diagnostics))
            return Result(false, diagnostics, null, null, null, string.Empty);

        IReadOnlyList<string> sourceFiles = FindSources(projectDirectory, project, diagnostics);
        List<CompilationUnitSyntax> units = new();
        foreach (string sourceFile in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string text;
            try
            {
                text = await File.ReadAllTextAsync(sourceFile, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                diagnostics.Add(new Diagnostic(
                    "EPL0003",
                    DiagnosticSeverity.Error,
                    new SourceSpan(sourceFile, 1),
                    $"Could not read the source file: {exception.Message}",
                    $"无法读取源文件：{exception.Message}"));
                continue;
            }
            units.Add(new SourceParser(sourceFile, text, diagnostics).Parse());
        }

        BoundProgram bound = new Binder(project, units, diagnostics).Bind();
        if (diagnostics.HasErrors)
            return Result(false, diagnostics, null, null, null, string.Empty);

        IReadOnlyList<ResolvedReference> references = new ReferenceResolver(projectDirectory, project, options, diagnostics)
            .Resolve(bound.AssemblyImports);
        if (diagnostics.HasErrors)
            return Result(false, diagnostics, null, null, null, string.Empty);

        IReadOnlyList<GeneratedSource> generatedSources = new CSharpEmitter(bound).Emit();
        GeneratedProject generated = GeneratedProjectWriter.Write(projectDirectory, project, options, generatedSources, references);
        if (options.CheckOnly && project.Target.Equals("syntax-only", StringComparison.OrdinalIgnoreCase))
            return Result(true, diagnostics, generated.Directory, generated.CombinedCSharpFile, null, string.Empty);

        int exitCode;
        string log;
        try
        {
            (exitCode, log) = await RunBuildAsync(generated, options, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            diagnostics.Add(new Diagnostic(
                "CLR2202",
                DiagnosticSeverity.Error,
                new SourceSpan(generated.ProjectFile, 1),
                $"Could not start the C# backend: {exception.Message}",
                $"无法启动 C# 后端：{exception.Message}",
                "Make sure the .NET SDK is installed and available as 'dotnet'.",
                "请确认已安装 .NET SDK，并且可以运行“dotnet”。"));
            return Result(false, diagnostics, generated.Directory, generated.CombinedCSharpFile, null, exception.ToString());
        }
        RecordBuildDiagnostics(log, exitCode, diagnostics, generated.ProjectFile);
        bool success = exitCode == 0 && !diagnostics.HasErrors && File.Exists(generated.AssemblyPath);
        if (exitCode == 0 && !File.Exists(generated.AssemblyPath))
        {
            diagnostics.Add(new Diagnostic(
                "CLR2203",
                DiagnosticSeverity.Error,
                new SourceSpan(generated.ProjectFile, 1),
                $"The C# backend reported success but did not create '{generated.AssemblyPath}'.",
                $"C# 后端报告成功，但没有生成“{generated.AssemblyPath}”。"));
            success = false;
        }
        return Result(
            success,
            diagnostics,
            generated.Directory,
            generated.CombinedCSharpFile,
            success ? generated.AssemblyPath : null,
            log);
    }

    public CompilationResult CheckSource(string source, string fileName = "memory.易")
    {
        DiagnosticBag diagnostics = new();
        CompilationUnitSyntax unit = new SourceParser(fileName, source, diagnostics).Parse();
        EPLabProject project = new()
        {
            Name = "MemoryCheck",
            Target = "syntax-only",
            RootNamespace = "MemoryCheck",
        };
        _ = new Binder(project, new[] { unit }, diagnostics).Bind();
        return Result(!diagnostics.HasErrors, diagnostics, null, null, null, string.Empty);
    }

    private static IReadOnlyList<string> FindSources(string projectDirectory, EPLabProject project, DiagnosticBag diagnostics)
    {
        List<string> files = new();
        foreach (string sourceDirectory in project.SourceDirectories)
        {
            string path = Path.GetFullPath(Path.Combine(projectDirectory, sourceDirectory));
            if (!Directory.Exists(path))
            {
                diagnostics.Add(new Diagnostic(
                    "EPL0004",
                    DiagnosticSeverity.Error,
                    new SourceSpan(Path.Combine(projectDirectory, "project.eplabproj"), 1),
                    $"Source directory '{path}' does not exist.",
                    $"源代码文件夹不存在：{path}"));
                continue;
            }
            try
            {
                files.AddRange(Directory.EnumerateFiles(path, "*.易", SearchOption.AllDirectories));
                files.AddRange(Directory.EnumerateFiles(path, "*.eplab", SearchOption.AllDirectories));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                diagnostics.Add(new Diagnostic(
                    "EPL0004",
                    DiagnosticSeverity.Error,
                    new SourceSpan(Path.Combine(projectDirectory, "project.eplabproj"), 1),
                    $"Could not scan source directory '{path}': {exception.Message}",
                    $"无法扫描源代码文件夹“{path}”：{exception.Message}"));
            }
        }

        string[] result = files.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static file => file, StringComparer.OrdinalIgnoreCase).ToArray();
        if (result.Length == 0)
        {
            diagnostics.Add(new Diagnostic(
                "EPL0005",
                DiagnosticSeverity.Error,
                new SourceSpan(Path.Combine(projectDirectory, "project.eplabproj"), 1),
                "No .易 or .eplab source files were found.",
                "没有找到 .易 或 .eplab 源文件。"));
        }
        return result;
    }

    private static async Task<(int ExitCode, string Log)> RunBuildAsync(
        GeneratedProject generated,
        CompilerOptions options,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = generated.Directory,
        };
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add(generated.ProjectFile);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add(options.Configuration);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(generated.OutputDirectory);
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--verbosity");
        startInfo.ArgumentList.Add("minimal");
        startInfo.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";

        using Process process = new() { StartInfo = startInfo };
        StringBuilder output = new();
        process.OutputDataReceived += (_, args) => { if (args.Data is not null) output.AppendLine(args.Data); };
        process.ErrorDataReceived += (_, args) => { if (args.Data is not null) output.AppendLine(args.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        process.WaitForExit();
        return (process.ExitCode, output.ToString());
    }

    internal static void RecordBuildDiagnostics(
        string log,
        int exitCode,
        DiagnosticBag diagnostics,
        string generatedProjectFile)
    {
        int errorsBefore = diagnostics.Count(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string line in log.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            Match match = BuildDiagnosticRegex().Match(line);
            bool projectLevel = false;
            if (!match.Success)
            {
                match = ProjectBuildDiagnosticRegex().Match(line);
                projectLevel = match.Success;
            }
            if (!match.Success)
                continue;
            string identity = match.Value;
            if (!seen.Add(identity))
                continue;
            string file = match.Groups["file"].Value.Trim();
            int.TryParse(match.Groups["line"].Value, out int sourceLine);
            int.TryParse(match.Groups["column"].Value, out int column);
            bool warning = match.Groups["severity"].Value.Equals("warning", StringComparison.OrdinalIgnoreCase);
            string code = match.Groups["code"].Value;
            string message = match.Groups["message"].Value.Trim();
            string diagnosticFile = string.IsNullOrWhiteSpace(file)
                || projectLevel && !file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                    ? generatedProjectFile
                    : file;
            diagnostics.Add(new Diagnostic(
                BackendDiagnosticId(code),
                warning ? DiagnosticSeverity.Warning : DiagnosticSeverity.Error,
                new SourceSpan(diagnosticFile, Math.Max(1, sourceLine), Math.Max(1, column)),
                message,
                $"C# 后端：{message}"));
        }

        if (exitCode != 0 && diagnostics.Count(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) == errorsBefore)
        {
            diagnostics.Add(new Diagnostic(
                "CLR2202",
                DiagnosticSeverity.Error,
                new SourceSpan(generatedProjectFile, 1),
                $"The C# backend build failed with exit code {exitCode}, but it did not report a detailed error.",
                $"C# 后端编译失败（退出码 {exitCode}），但它没有报告详细错误。",
                "Open Build Output for the full log and check that the .NET SDK and referenced DLLs are available.",
                "请打开“编译输出”查看完整日志，并检查 .NET SDK 和引用的 DLL 是否可用。"));
        }
    }

    private static string BackendDiagnosticId(string code)
    {
        if (code.StartsWith("EPL", StringComparison.OrdinalIgnoreCase))
            return code;
        return code.StartsWith("CS", StringComparison.OrdinalIgnoreCase)
            ? "CLR" + code[2..]
            : "CLR" + code;
    }

    private static CompilationResult Result(
        bool success,
        DiagnosticBag diagnostics,
        string? generatedDirectory,
        string? generatedCSharp,
        string? assembly,
        string log)
        => new(success, diagnostics.ToArray(), generatedDirectory, generatedCSharp, assembly, log);

    [GeneratedRegex(@"^\s*(?<file>.+?)\((?<line>\d+),(?<column>\d+)\):\s*(?<severity>error|warning)\s+(?<code>[A-Za-z]+\d+):\s*(?<message>.*?)(?:\s+\[.*\])?$", RegexOptions.IgnoreCase)]
    private static partial Regex BuildDiagnosticRegex();

    [GeneratedRegex(@"^\s*(?<file>.+?)\s+:\s*(?<severity>error|warning)\s+(?<code>[A-Za-z]+\d+):\s*(?<message>.*?)(?:\s+\[.*\])?$", RegexOptions.IgnoreCase)]
    private static partial Regex ProjectBuildDiagnosticRegex();

    private static readonly HashSet<string> SupportedTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "labapi-net48",
        "library-net48",
        "library-net8",
        "syntax-only",
    };
}
