using System.Reflection;
using System.Runtime.Loader;

string exampleDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
string assemblyPath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.Combine(exampleDirectory, "bin", "Release", "EPLabSpinBot.dll");

if (!File.Exists(assemblyPath))
    throw new FileNotFoundException("Build the Easy project before running the validation harness.", assemblyPath);

List<string> searchDirectories = new() { Path.GetDirectoryName(assemblyPath)! };
foreach (string value in args.Skip(1))
{
    string fullPath = Path.GetFullPath(value);
    string? directory = Directory.Exists(fullPath)
        ? fullPath
        : File.Exists(fullPath) ? Path.GetDirectoryName(fullPath) : null;
    if (directory is not null && !searchDirectories.Contains(directory, StringComparer.OrdinalIgnoreCase))
        searchDirectories.Add(directory);
}

Dictionary<string, string> dependencyFiles = new(StringComparer.OrdinalIgnoreCase);
foreach (string directory in searchDirectories)
{
    foreach (string file in Directory.EnumerateFiles(directory, "*.dll", SearchOption.TopDirectoryOnly))
        dependencyFiles.TryAdd(Path.GetFileNameWithoutExtension(file), file);
}

Assembly? Resolve(AssemblyLoadContext context, AssemblyName name)
{
    if (name.Name is null || !dependencyFiles.TryGetValue(name.Name, out string? path))
        return null;

    Assembly? loaded = context.Assemblies.FirstOrDefault(candidate =>
        string.Equals(candidate.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase));
    return loaded ?? context.LoadFromAssemblyPath(Path.GetFullPath(path));
}

AssemblyLoadContext.Default.Resolving += Resolve;
try
{
    Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);

    Type testType = RequireType(assembly, "EPLabSpinBot.旋转数学验证");
    MethodInfo run = testType.GetMethod("运行", BindingFlags.Public | BindingFlags.Static)
        ?? throw new MissingMethodException(testType.FullName, "运行");
    object? result = run.Invoke(null, null);
    int checks = Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    if (checks != 17)
        throw new InvalidOperationException($"Expected 17 Easy-side checks, but the DLL reported {checks}.");

    Type pluginType = RequireType(assembly, "EPLabSpinBot.__EplabPlugin");
    string pluginBase = pluginType.BaseType?.FullName ?? string.Empty;
    if (!pluginBase.StartsWith("LabApi.Loader.Features.Plugins.Plugin`1", StringComparison.Ordinal))
        throw new TypeLoadException($"Generated plugin has the wrong base type: {pluginBase}");
    if (pluginType.GetConstructor(Type.EmptyTypes) is null)
        throw new MissingMethodException(pluginType.FullName, ".ctor()");

    Type commandType = RequireType(assembly, "EPLabSpinBot.__EplabCommand0");
    if (!commandType.GetInterfaces().Any(type => type.FullName == "CommandSystem.ICommand"))
        throw new TypeLoadException("Generated RA adapter does not implement CommandSystem.ICommand.");
    PropertyInfo commandProperty = commandType.GetProperty("Command", BindingFlags.Public | BindingFlags.Instance)
        ?? throw new MissingMemberException(commandType.FullName, "Command");
    object command = Activator.CreateInstance(commandType)
        ?? throw new MissingMethodException(commandType.FullName, ".ctor()");
    string? commandName = commandProperty.GetValue(command) as string;
    if (!string.Equals(commandName, "spinbot", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Generated RA adapter reported the wrong command name: {commandName ?? "<null>"}");
    }

    Console.WriteLine($"SpinBot EPLab validation passed: math={checks}, reflected-types=2");
}
finally
{
    AssemblyLoadContext.Default.Resolving -= Resolve;
}

static Type RequireType(Assembly assembly, string fullName) =>
    assembly.GetType(fullName, throwOnError: true, ignoreCase: false)
    ?? throw new TypeLoadException(fullName);
