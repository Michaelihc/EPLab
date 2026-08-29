using System.Text.Json;
using System.Text.Json.Serialization;

namespace EPLab.Compiler;

public sealed class EPLabProject
{
    [JsonPropertyName("format")]
    public int Format { get; set; } = 1;

    [JsonPropertyName("name")]
    public string Name { get; set; } = "MyPlugin";

    [JsonPropertyName("target")]
    public string Target { get; set; } = "labapi-net48";

    [JsonPropertyName("rootNamespace")]
    public string RootNamespace { get; set; } = "MyPlugin";

    [JsonPropertyName("sourceDirectories")]
    public List<string> SourceDirectories { get; set; } = new() { "src" };

    [JsonPropertyName("references")]
    public List<string> References { get; set; } = new();

    [JsonPropertyName("managedDirectory")]
    public string? ManagedDirectory { get; set; }

    [JsonPropertyName("globalDependenciesDirectory")]
    public string? GlobalDependenciesDirectory { get; set; }

    [JsonPropertyName("outputDirectory")]
    public string OutputDirectory { get; set; } = "bin";

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }

    public static EPLabProject Load(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<EPLabProject>(json, JsonOptions)
            ?? throw new InvalidDataException($"Project file is empty: {path}");
    }

    public void Save(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("The project file needs a parent directory.");
        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this, JsonOptions), new System.Text.UTF8Encoding(false));
            if (File.Exists(fullPath))
            {
                try
                {
                    File.Replace(temporaryPath, fullPath, null);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Move(temporaryPath, fullPath, overwrite: true);
                }
            }
            else
            {
                File.Move(temporaryPath, fullPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        WriteIndented = true,
    };
}

public sealed record CompilerOptions(
    bool EmitGeneratedCSharp = true,
    bool ChineseDiagnostics = false,
    bool CheckOnly = false,
    string Configuration = "Release",
    string? ManagedDirectory = null,
    string? GlobalDependenciesDirectory = null,
    string? OutputDirectory = null);

public sealed record CompilationResult(
    bool Success,
    IReadOnlyList<Diagnostic> Diagnostics,
    string? GeneratedDirectory,
    string? GeneratedCSharpPath,
    string? AssemblyPath,
    string BuildLog);
