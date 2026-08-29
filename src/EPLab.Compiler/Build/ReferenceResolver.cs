using System.Security.Cryptography;

namespace EPLab.Compiler.Build;

internal enum ReferenceOrigin
{
    Local,
    Game,
    Global,
}

internal sealed record ResolvedReference(string Name, string Path, string Sha256, ReferenceOrigin Origin)
{
    public bool CopyLocal => Origin == ReferenceOrigin.Local;
}

internal sealed class ReferenceResolver
{
    private readonly string projectDirectory;
    private readonly EPLabProject project;
    private readonly CompilerOptions options;
    private readonly DiagnosticBag diagnostics;

    public ReferenceResolver(string projectDirectory, EPLabProject project, CompilerOptions options, DiagnosticBag diagnostics)
    {
        this.projectDirectory = projectDirectory;
        this.project = project;
        this.options = options;
        this.diagnostics = diagnostics;
    }

    public IReadOnlyList<ResolvedReference> Resolve(IEnumerable<string> sourceReferences)
    {
        string managed = ResolveManagedDirectory();
        string global = ResolveGlobalDirectory();
        List<ResolvedReference> result = new();
        IEnumerable<string> requested = project.References.Concat(sourceReferences);
        if (project.Target.Equals("labapi-net48", StringComparison.OrdinalIgnoreCase))
        {
            requested = requested.Concat(new[]
            {
                "game:LabApi",
                "game:Assembly-CSharp",
                "game:CommandSystem.Core",
                "game:YamlDotNet",
            });
        }
        foreach (string reference in requested.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string path;
            string name;
            ReferenceOrigin origin;
            if (reference.StartsWith("game:", StringComparison.OrdinalIgnoreCase))
            {
                name = EnsureDll(reference[5..]);
                path = Path.Combine(managed, name);
                origin = ReferenceOrigin.Game;
            }
            else if (reference.StartsWith("global:", StringComparison.OrdinalIgnoreCase))
            {
                name = EnsureDll(reference[7..]);
                path = Path.Combine(global, name);
                origin = ReferenceOrigin.Global;
            }
            else
            {
                string expanded = Environment.ExpandEnvironmentVariables(reference);
                path = Path.IsPathRooted(expanded) ? expanded : Path.Combine(projectDirectory, expanded);
                name = Path.GetFileName(path);
                origin = ReferenceOrigin.Local;
            }

            path = Path.GetFullPath(path);
            if (!File.Exists(path))
            {
                diagnostics.Add(new Diagnostic(
                    "CLR2201",
                    DiagnosticSeverity.Error,
                    new SourceSpan(Path.Combine(projectDirectory, "project.eplabproj"), 1),
                    $"Reference '{reference}' was not found at '{path}'.",
                    $"找不到引用“{reference}”：{path}",
                    "Choose the correct SCP:SL Managed/dependencies folder in the project or GUI.",
                    "请在项目或图形界面中选择正确的 SCP:SL Managed/依赖目录。"));
                continue;
            }

            result.Add(new ResolvedReference(Path.GetFileNameWithoutExtension(name), path, Hash(path), origin));
        }
        return result;
    }

    private string ResolveManagedDirectory()
    {
        if (!string.IsNullOrWhiteSpace(options.ManagedDirectory))
            return ResolveFromCurrentDirectory(options.ManagedDirectory);
        if (!string.IsNullOrWhiteSpace(project.ManagedDirectory))
            return ResolveFromProjectDirectory(project.ManagedDirectory);
        string? environmentValue = Environment.GetEnvironmentVariable("SCP_SL_MANAGED");
        if (!string.IsNullOrWhiteSpace(environmentValue))
            return ResolveFromCurrentDirectory(environmentValue);

        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return Path.Combine(programFilesX86, "Steam", "steamapps", "common", "SCP Secret Laboratory Dedicated Server", "SCPSL_Data", "Managed");
    }

    private string ResolveGlobalDirectory()
    {
        if (!string.IsNullOrWhiteSpace(options.GlobalDependenciesDirectory))
            return ResolveFromCurrentDirectory(options.GlobalDependenciesDirectory);
        if (!string.IsNullOrWhiteSpace(project.GlobalDependenciesDirectory))
            return ResolveFromProjectDirectory(project.GlobalDependenciesDirectory);

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SCP Secret Laboratory",
            "LabAPI",
            "dependencies",
            "global");
    }

    private string ResolveFromProjectDirectory(string value)
    {
        string expanded = Environment.ExpandEnvironmentVariables(value);
        return Path.GetFullPath(Path.IsPathRooted(expanded) ? expanded : Path.Combine(projectDirectory, expanded));
    }

    private static string ResolveFromCurrentDirectory(string value)
        => Path.GetFullPath(Environment.ExpandEnvironmentVariables(value));

    private static string EnsureDll(string value)
        => value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? value : value + ".dll";

    private static string Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
