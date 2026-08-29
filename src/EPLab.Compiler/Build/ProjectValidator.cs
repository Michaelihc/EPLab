using System.Globalization;

namespace EPLab.Compiler.Build;

internal static class ProjectValidator
{
    public static bool Validate(
        EPLabProject project,
        string projectFile,
        string projectDirectory,
        CompilerOptions options,
        DiagnosticBag diagnostics)
    {
        ValidateName(project.Name, projectFile, diagnostics);
        ValidateRootNamespace(project.RootNamespace, projectFile, diagnostics);
        ValidateOutputDirectory(project.OutputDirectory, projectFile, projectDirectory, diagnostics);
        ValidateSourceDirectories(project.SourceDirectories, projectFile, projectDirectory, diagnostics);
        ValidateReferences(project.References, projectFile, projectDirectory, diagnostics);
        ValidateOptionalProjectDirectory(project.ManagedDirectory, "managedDirectory", projectFile, projectDirectory, diagnostics);
        ValidateOptionalProjectDirectory(project.GlobalDependenciesDirectory, "globalDependenciesDirectory", projectFile, projectDirectory, diagnostics);
        ValidateOptions(options, projectFile, diagnostics);
        return !diagnostics.HasErrors;
    }

    private static void ValidateName(string? value, string projectFile, DiagnosticBag diagnostics)
    {
        if (!IsSafeFileSegment(value))
        {
            Add(
                diagnostics,
                "EPL0007",
                projectFile,
                "Project 'name' must be one safe file name, not empty, '.' or '..', and must not contain path separators.",
                "项目的“name”必须是一个安全的文件名，不能为空、不能是“.”或“..”，也不能包含路径分隔符。");
        }
    }

    private static void ValidateRootNamespace(string? value, string projectFile, DiagnosticBag diagnostics)
    {
        bool valid = !string.IsNullOrWhiteSpace(value)
            && value.Split('.').All(IsIdentifier);
        if (!valid)
        {
            Add(
                diagnostics,
                "EPL0008",
                projectFile,
                "Project 'rootNamespace' must be a dot-separated list of valid C# identifiers.",
                "项目的“rootNamespace”必须是用点分隔的有效 C# 名称。");
        }
    }

    private static void ValidateOutputDirectory(
        string? value,
        string projectFile,
        string projectDirectory,
        DiagnosticBag diagnostics)
    {
        if (!TryResolveContainedProjectPath(value, projectDirectory, allowCurrentDirectory: false, out _))
        {
            Add(
                diagnostics,
                "EPL0009",
                projectFile,
                "Project 'outputDirectory' must be a non-empty relative folder inside the project and cannot contain '.' or '..' segments.",
                "项目的“outputDirectory”必须是项目内的非空相对文件夹，且不能包含“.”或“..”路径段。");
        }
    }

    private static void ValidateSourceDirectories(
        List<string>? values,
        string projectFile,
        string projectDirectory,
        DiagnosticBag diagnostics)
    {
        if (values is null || values.Count == 0)
        {
            Add(
                diagnostics,
                "EPL0010",
                projectFile,
                "Project 'sourceDirectories' must contain at least one relative folder inside the project.",
                "项目的“sourceDirectories”必须至少包含一个项目内的相对文件夹。");
            return;
        }

        for (int index = 0; index < values.Count; index++)
        {
            if (TryResolveContainedProjectPath(values[index], projectDirectory, allowCurrentDirectory: false, out _))
                continue;
            Add(
                diagnostics,
                "EPL0010",
                projectFile,
                $"Project 'sourceDirectories[{index}]' must be a non-empty relative folder inside the project and cannot contain '.' or '..' segments.",
                $"项目的“sourceDirectories[{index}]”必须是项目内的非空相对文件夹，且不能包含“.”或“..”路径段。");
        }
    }

    private static void ValidateReferences(
        List<string>? values,
        string projectFile,
        string projectDirectory,
        DiagnosticBag diagnostics)
    {
        if (values is null)
        {
            Add(
                diagnostics,
                "EPL0011",
                projectFile,
                "Project 'references' must be an array. Use an empty array when no DLLs are needed.",
                "项目的“references”必须是数组。不需要 DLL 时请使用空数组。");
            return;
        }

        for (int index = 0; index < values.Count; index++)
        {
            string? value = values[index];
            if (IsValidReference(value, projectDirectory))
                continue;
            Add(
                diagnostics,
                "EPL0011",
                projectFile,
                $"Project 'references[{index}]' is empty or is not a valid DLL reference.",
                $"项目的“references[{index}]”为空或不是有效的 DLL 引用。");
        }
    }

    private static void ValidateOptionalProjectDirectory(
        string? value,
        string propertyName,
        string projectFile,
        string projectDirectory,
        DiagnosticBag diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        try
        {
            string expanded = Environment.ExpandEnvironmentVariables(value);
            _ = Path.GetFullPath(Path.IsPathRooted(expanded) ? expanded : Path.Combine(projectDirectory, expanded));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Add(
                diagnostics,
                "EPL0012",
                projectFile,
                $"Project '{propertyName}' is not a valid folder path.",
                $"项目的“{propertyName}”不是有效的文件夹路径。");
        }
    }

    private static void ValidateOptions(CompilerOptions options, string projectFile, DiagnosticBag diagnostics)
    {
        if (!IsSafeFileSegment(options.Configuration))
        {
            Add(
                diagnostics,
                "EPL0013",
                projectFile,
                "Compiler configuration must be one safe folder name.",
                "编译配置必须是一个安全的文件夹名。");
        }

        if (options.OutputDirectory is not null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(options.OutputDirectory))
                    throw new ArgumentException("Empty output directory.");
                _ = Path.GetFullPath(options.OutputDirectory);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                Add(
                    diagnostics,
                    "EPL0013",
                    projectFile,
                    "Compiler output override is not a valid folder path.",
                    "编译器输出覆盖路径不是有效的文件夹路径。");
            }
        }

        ValidateOptionalOverrideDirectory(options.ManagedDirectory, "managed", projectFile, diagnostics);
        ValidateOptionalOverrideDirectory(options.GlobalDependenciesDirectory, "dependencies", projectFile, diagnostics);
    }

    private static void ValidateOptionalOverrideDirectory(
        string? value,
        string optionName,
        string projectFile,
        DiagnosticBag diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        try
        {
            _ = Path.GetFullPath(Environment.ExpandEnvironmentVariables(value));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            Add(
                diagnostics,
                "EPL0013",
                projectFile,
                $"Compiler '{optionName}' override is not a valid folder path.",
                $"编译器的“{optionName}”覆盖路径不是有效的文件夹路径。");
        }
    }

    private static bool IsValidReference(string? value, string projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string trimmed = value.Trim();
        string? prefixedName = trimmed.StartsWith("game:", StringComparison.OrdinalIgnoreCase)
            ? trimmed[5..]
            : trimmed.StartsWith("global:", StringComparison.OrdinalIgnoreCase)
                ? trimmed[7..]
                : null;
        if (prefixedName is not null)
        {
            string withoutDll = prefixedName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? prefixedName[..^4]
                : prefixedName;
            return IsSafeFileSegment(withoutDll);
        }

        try
        {
            string expanded = Environment.ExpandEnvironmentVariables(trimmed);
            _ = Path.GetFullPath(Path.IsPathRooted(expanded) ? expanded : Path.Combine(projectDirectory, expanded));
            string fileName = Path.GetFileName(expanded);
            return !Path.EndsInDirectorySeparator(expanded)
                && fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                && IsSafeFileSegment(fileName);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool TryResolveContainedProjectPath(
        string? value,
        string projectDirectory,
        bool allowCurrentDirectory,
        out string? resolved)
    {
        resolved = null;
        if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value))
            return false;
        string[] segments = value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".." || !IsSafeFileSegment(segment)))
            return false;
        try
        {
            resolved = Path.GetFullPath(Path.Combine(projectDirectory, value));
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectDirectory));
            if (allowCurrentDirectory && string.Equals(resolved, root, PathComparison))
                return true;
            return resolved.StartsWith(root + Path.DirectorySeparatorChar, PathComparison);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsSafeFileSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "." or "..")
            return false;
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal) || value.EndsWith('.'))
            return false;
        if (value.Any(character => character < ' '
            || character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*'))
            return false;
        string deviceName = value.Split('.')[0];
        return !ReservedDeviceNames.Contains(deviceName);
    }

    private static bool IsIdentifier(string value)
    {
        if (value.Length == 0 || !IsIdentifierStart(value[0]))
            return false;
        return value.Skip(1).All(IsIdentifierPart);
    }

    private static bool IsIdentifierStart(char character)
        => character == '_' || char.GetUnicodeCategory(character) is
            UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.LetterNumber;

    private static bool IsIdentifierPart(char character)
        => IsIdentifierStart(character) || char.GetUnicodeCategory(character) is
            UnicodeCategory.NonSpacingMark or
            UnicodeCategory.SpacingCombiningMark or
            UnicodeCategory.DecimalDigitNumber or
            UnicodeCategory.ConnectorPunctuation or
            UnicodeCategory.Format;

    private static void Add(
        DiagnosticBag diagnostics,
        string id,
        string projectFile,
        string english,
        string chinese)
        => diagnostics.Add(new Diagnostic(
            id,
            DiagnosticSeverity.Error,
            new SourceSpan(projectFile, 1),
            english,
            chinese));

    private static StringComparison PathComparison
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };
}
