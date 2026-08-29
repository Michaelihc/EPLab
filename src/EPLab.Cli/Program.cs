using EPLab.Compiler;
using EPLab.Compiler.Build;

return await MainAsync(args);

static async Task<int> MainAsync(string[] arguments)
{
    bool chinese = arguments.Contains("--cn", StringComparer.OrdinalIgnoreCase);
    if (arguments.Length == 0 || arguments[0] is "help" or "--help" or "-h")
    {
        PrintHelp(chinese);
        return 0;
    }

    try
    {
        string command = arguments[0].ToLowerInvariant();
        switch (command)
        {
            case "new":
                if (arguments.Length < 2) return Need("new needs a folder.", "new 需要一个文件夹。", chinese);
                if (!ValidateOptions(arguments, 2, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--cn" }, chinese, out int newError))
                    return newError;
                ProjectTemplates.CreateHelloProject(arguments[1], chinese: chinese);
                Console.WriteLine(chinese ? $"已创建：{Path.GetFullPath(arguments[1])}" : $"Created: {Path.GetFullPath(arguments[1])}");
                return 0;

            case "build":
            case "check":
                if (arguments.Length < 2) return Need("build/check needs project.eplabproj.", "build/check 需要 project.eplabproj。", chinese);
                HashSet<string> valueOptions = new(StringComparer.OrdinalIgnoreCase) { "--managed", "--dependencies" };
                if (!ValidateOptions(arguments, 2, new HashSet<string>(valueOptions, StringComparer.OrdinalIgnoreCase) { "--cn" }, chinese, out int optionError, valueOptions))
                    return optionError;
                string? managed = ValueAfter(arguments, "--managed");
                string? dependencies = ValueAfter(arguments, "--dependencies");
                CompilationResult result = await new ProjectCompiler().CompileAsync(
                    arguments[1],
                    new CompilerOptions(
                        EmitGeneratedCSharp: true,
                        ChineseDiagnostics: chinese,
                        CheckOnly: command == "check",
                        ManagedDirectory: managed,
                        GlobalDependenciesDirectory: dependencies));
                foreach (Diagnostic diagnostic in result.Diagnostics)
                    Console.Error.WriteLine(diagnostic.Format(chinese));
                if (!string.IsNullOrWhiteSpace(result.BuildLog))
                    Console.WriteLine(result.BuildLog.TrimEnd());
                if (result.Success)
                {
                    Console.WriteLine(command == "check"
                        ? (chinese ? "检查通过。" : "Check passed.")
                        : (chinese ? "编译成功。" : "Build succeeded."));
                    if (command == "build" && result.AssemblyPath is not null) Console.WriteLine(result.AssemblyPath);
                    if (result.GeneratedCSharpPath is not null)
                        Console.WriteLine(chinese ? $"生成的 C#：{result.GeneratedCSharpPath}" : $"Generated C#: {result.GeneratedCSharpPath}");
                    return 0;
                }
                Console.Error.WriteLine(command == "check"
                    ? (chinese ? "检查未通过。" : "Check failed.")
                    : (chinese ? "编译失败。" : "Build failed."));
                return 1;

            default:
                return Need($"Unknown command: {arguments[0]}", $"未知命令：{arguments[0]}", chinese);
        }
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine(chinese ? $"发生了意外错误：{exception.Message}" : $"Unexpected error: {exception.Message}");
        return 2;
    }
}

static bool ValidateOptions(
    string[] arguments,
    int start,
    ISet<string> allowed,
    bool chinese,
    out int error,
    ISet<string>? valueOptions = null)
{
    for (int index = start; index < arguments.Length; index++)
    {
        string option = arguments[index];
        if (!option.StartsWith('-') || !allowed.Contains(option))
        {
            error = Need($"Unknown option: {option}", $"未知选项：{option}", chinese);
            return false;
        }

        if (valueOptions?.Contains(option) == true)
        {
            if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith('-'))
            {
                error = Need($"{option} needs a folder path.", $"{option} 后面需要文件夹路径。", chinese);
                return false;
            }
            index++;
        }
    }

    error = 0;
    return true;
}

static string? ValueAfter(string[] arguments, string option)
{
    int index = Array.FindIndex(arguments, value => value.Equals(option, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}

static int Need(string english, string chineseMessage, bool chinese)
{
    Console.Error.WriteLine(chinese ? chineseMessage : english);
    PrintHelp(chinese);
    return 2;
}

static void PrintHelp(bool chinese)
{
    Console.WriteLine(chinese
        ? """
          EPLab — 用亲切的易风格源代码编写 LabAPI 插件

          eplab new <文件夹>                         创建小例子
          eplab check <project.eplabproj> [--cn]    检查并类型编译
          eplab build <project.eplabproj> [--cn]    编译 DLL

          可选：
            --managed <文件夹>       SCP:SL SCPSL_Data/Managed
            --dependencies <文件夹>  LabAPI dependencies/global
          """
        : """
          EPLab — write LabAPI plugins with friendly 易-style source

          eplab new <folder>                         Create a tiny example
          eplab check <project.eplabproj> [--cn]    Check and type-build
          eplab build <project.eplabproj> [--cn]    Build the DLL

          Optional:
            --managed <folder>       SCP:SL SCPSL_Data/Managed
            --dependencies <folder>  LabAPI dependencies/global
          """);
}
