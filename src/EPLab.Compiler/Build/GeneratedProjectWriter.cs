using System.Security;
using System.Text.Json;
using EPLab.Compiler.CodeGen;

namespace EPLab.Compiler.Build;

internal sealed record GeneratedProject(
    string Directory,
    string ProjectFile,
    string CombinedCSharpFile,
    string OutputDirectory,
    string AssemblyPath);

internal static class GeneratedProjectWriter
{
    public static GeneratedProject Write(
        string sourceProjectDirectory,
        EPLabProject project,
        CompilerOptions options,
        IReadOnlyList<GeneratedSource> sources,
        IReadOnlyList<ResolvedReference> references)
    {
        string configuration = SanitizeSegment(options.Configuration);
        string generatedDirectory = Path.Combine(sourceProjectDirectory, "obj", "eplab", configuration);
        Directory.CreateDirectory(generatedDirectory);

        // A project can change profiles between builds. Remove only EPLab-owned
        // generated source files so an old LabAPI host cannot linger in obj/.
        foreach (string staleSource in Directory.EnumerateFiles(generatedDirectory, "EPLab.*.g.cs", SearchOption.TopDirectoryOnly))
            File.Delete(staleSource);

        foreach (GeneratedSource source in sources)
            File.WriteAllText(Path.Combine(generatedDirectory, source.FileName), source.Text, new System.Text.UTF8Encoding(false));

        // This is a human-readable concatenation. It deliberately ends in .txt so the
        // generated project compiles the individual source files only once.
        string combined = Path.Combine(generatedDirectory, "EPLab.All.Generated.cs.txt");
        File.WriteAllText(
            combined,
            string.Join(Environment.NewLine + Environment.NewLine, sources.Select(source => $"// ===== {source.FileName} ====={Environment.NewLine}{source.Text}")),
            new System.Text.UTF8Encoding(false));

        string targetFramework = project.Target.Equals("labapi-net48", StringComparison.OrdinalIgnoreCase)
            ? "net48"
            : project.Target.Equals("library-net48", StringComparison.OrdinalIgnoreCase) ? "net48" : "net8.0";
        string outputDirectory = options.OutputDirectory is not null
            ? Path.GetFullPath(options.OutputDirectory)
            : Path.GetFullPath(Path.Combine(sourceProjectDirectory, project.OutputDirectory, options.Configuration));
        Directory.CreateDirectory(outputDirectory);

        string projectPath = Path.Combine(generatedDirectory, project.Name + ".generated.csproj");
        File.WriteAllText(projectPath, BuildProjectXml(project, targetFramework, references, sources), new System.Text.UTF8Encoding(false));
        File.WriteAllText(
            Path.Combine(generatedDirectory, "build-info.json"),
            JsonSerializer.Serialize(
                new
                {
                    compiler = "EPLab",
                    format = 1,
                    project = project.Name,
                    targetFramework,
                    generatedAtUtc = DateTimeOffset.UtcNow,
                    references = references.Select(reference => new
                    {
                        reference.Name,
                        reference.Path,
                        reference.Sha256,
                        origin = reference.Origin.ToString().ToLowerInvariant(),
                        copyLocal = reference.CopyLocal,
                    }),
                },
                new JsonSerializerOptions { WriteIndented = true }),
            new System.Text.UTF8Encoding(false));

        return new GeneratedProject(
            generatedDirectory,
            projectPath,
            combined,
            outputDirectory,
            Path.Combine(outputDirectory, project.Name + ".dll"));
    }

    private static string BuildProjectXml(
        EPLabProject project,
        string targetFramework,
        IReadOnlyList<ResolvedReference> references,
        IReadOnlyList<GeneratedSource> sources)
    {
        string package = targetFramework == "net48"
            ? "    <PackageReference Include=\"Microsoft.NETFramework.ReferenceAssemblies.net48\" Version=\"1.0.3\" PrivateAssets=\"all\" />\n"
            : string.Empty;
        string frameworkReferences = targetFramework == "net48"
            ? "    <Reference Include=\"Microsoft.CSharp\" />\n"
            : string.Empty;
        string referenceXml = string.Join(
            Environment.NewLine,
            references.Select(reference =>
                $"    <Reference Include=\"{Xml(reference.Name)}\" HintPath=\"{Xml(reference.Path)}\" Private=\"{reference.CopyLocal.ToString().ToLowerInvariant()}\" />"));
        string compileXml = string.Join(
            Environment.NewLine,
            sources.Select(source => $"    <Compile Include=\"{Xml(source.FileName)}\" />"));
        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>{targetFramework}</TargetFramework>
                <OutputType>Library</OutputType>
                <LangVersion>latest</LangVersion>
                <Nullable>enable</Nullable>
                <ImplicitUsings>disable</ImplicitUsings>
                <AssemblyName>{Xml(project.Name)}</AssemblyName>
                <RootNamespace>{Xml(project.RootNamespace)}</RootNamespace>
                <GenerateAssemblyInfo>true</GenerateAssemblyInfo>
                <Deterministic>true</Deterministic>
                <DebugType>portable</DebugType>
                <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup>
              <ItemGroup>
            {package}{frameworkReferences}{referenceXml}
              </ItemGroup>
              <ItemGroup>
            {compileXml}
              </ItemGroup>
            </Project>
            """;
    }

    private static string Xml(string value) => SecurityElement.Escape(value) ?? string.Empty;

    private static string SanitizeSegment(string value)
        => string.Concat(value.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
}
