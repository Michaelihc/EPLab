using System.Reflection;
using EPLab.Compiler;
using EPLab.Compiler.Build;

public static class AuditRegressionTests
{
    public static async Task TypedArrayLiteralDestinations()
    {
        const string source = """
            .版本 2
            .扩展 CLR 1

            .程序集 Widget, , 公开
            .程序集变量 Value, 整数型, 公开

            .程序集 ArrayCases, , 公开
            .子程序 ByteValue, 字节型, 公开, 静态
            .局部变量 Values, 字节型, , “2”, ｛1，2｝
            返回（Values[2]）

            .子程序 ShortEmptyCount, 整数型, 公开, 静态
            .局部变量 Values, 短整数型, , “0”, ｛｝
            返回（取数组成员数（Values））

            .子程序 LongValue, 长整数型, 公开, 静态
            .局部变量 Values, 长整数型, , “2”, ｛3，4｝
            返回（Values[2]）

            .子程序 FloatValue, 小数型, 公开, 静态
            .局部变量 Values, 小数型, , “2”, ｛5，6｝
            返回（Values[2]）

            .子程序 UserValue, 整数型, 公开, 静态
            .局部变量 Values, widget, , “1”, ｛创建对象（WIDGET）｝
            Values[1].value ＝ 7
            返回（Values[1].value）

            .子程序 EmptyUserCount, 整数型, 公开, 静态
            .局部变量 Values, WIDGET, , “0”, ｛｝
            返回（取数组成员数（Values））

            .子程序 AssignedLong, 长整数型, 公开, 静态
            .局部变量 Values, 长整数型, , “0”
            Values ＝ ｛8，9｝
            返回（Values[2]）
            """;

        CompilationResult result = await CompileTemporary("TypedArrayLiteralFixture", source);
        Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
        Type type = assembly.GetType("TypedArrayLiteralFixture.ArrayCases", throwOnError: true)!;
        Require((byte)Invoke(type, "ByteValue")! == 2, "byte EArray did not accept integer literals");
        Require(Convert.ToInt32(Invoke(type, "ShortEmptyCount")) == 0, "empty short EArray inferred object instead of short");
        Require((long)Invoke(type, "LongValue")! == 4L, "long EArray did not use its destination element type");
        Require(Math.Abs((float)Invoke(type, "FloatValue")! - 6F) < 0.0001F, "float EArray did not accept integer literals");
        Require(Convert.ToInt32(Invoke(type, "UserValue")) == 7, "user-type EArray did not canonicalize its destination type/member");
        Require(Convert.ToInt32(Invoke(type, "EmptyUserCount")) == 0, "empty user-type EArray inferred object");
        Require((long)Invoke(type, "AssignedLong")! == 9L, "array assignment ignored its destination element type");

        string program = await File.ReadAllTextAsync(Path.Combine(result.GeneratedDirectory!, "EPLab.Program.g.cs"));
        foreach (string elementType in new[] { "byte", "short", "long", "float", "Widget" })
        {
            Require(program.Contains($"EplRuntime.Array<{elementType}>", StringComparison.Ordinal),
                $"generated C# did not specialize an array literal as {elementType}");
        }
    }

    public static Task FriendlyDeclarationCollisions()
    {
        AssertDiagnostic(
            """
            .版本 2
            .程序集 重复签名
            .子程序 Same, 整数型, 公开
            .参数 First, 整数型
            返回（First）
            .子程序 Same, 整数型, 公开
            .参数 Second, int
            返回（Second）
            """,
            "EPL1312",
            "identical method signatures");

        AssertDiagnostic(
            """
            .版本 2
            .程序集 成员冲突
            .程序集变量 Shared, 整数型
            .子程序 shared, 整数型
            返回（1）
            """,
            "EPL1309",
            "field/method namespace collision");

        AssertDiagnostic(
            """
            .版本 2
            .全局变量 Shared, 整数型
            .常量 shared, 1
            .DLL命令 SHARED, 整数型, “kernel32.dll”, “GetTickCount”
            """,
            "EPL1309",
            "global/constant/DLL namespace collision");

        AssertDiagnostic(
            """
            .版本 2
            .数据类型 Mixed, 公开
            .成员 Value, 整数型
            .程序集 mixed, , 公开
            """,
            "EPL1305",
            "struct/class top-level type collision");

        const string validOverload = """
            .版本 2
            .程序集 有效重载
            .子程序 Choose, 整数型, 公开
            .参数 Value, 整数型
            返回（Value）
            .子程序 Choose, 文本型, 公开
            .参数 Value, 文本型
            返回（Value）
            """;
        CompilationResult valid = new ProjectCompiler().CheckSource(validOverload, "有效重载.易");
        Require(valid.Success, "distinct overloads were rejected:" + Environment.NewLine + Explain(valid));
        return Task.CompletedTask;
    }

    private static void AssertDiagnostic(string source, string id, string scenario)
    {
        CompilationResult result = new ProjectCompiler().CheckSource(source, "声明冲突.易");
        Require(!result.Success, $"{scenario} unexpectedly passed checking");
        Require(result.Diagnostics.Any(item => item.Id == id),
            $"{scenario} should report {id}, got: {string.Join(", ", result.Diagnostics.Select(static item => item.Id))}");
        Require(!result.Diagnostics.Any(static item => item.Id.StartsWith("CLR", StringComparison.Ordinal)),
            $"{scenario} leaked to backend-style diagnostics: {Explain(result)}");
    }

    private static async Task<CompilationResult> CompileTemporary(string name, string source)
    {
        string directory = Path.Combine(Path.GetTempPath(), "eplab-tests", name + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "src"));
        EPLabProject project = new()
        {
            Name = name,
            RootNamespace = name,
            Target = "library-net8",
            SourceDirectories = new List<string> { "src" },
            OutputDirectory = "bin",
        };
        string projectFile = Path.Combine(directory, "project.eplabproj");
        project.Save(projectFile);
        await File.WriteAllTextAsync(Path.Combine(directory, "src", "主程序.易"), source, new System.Text.UTF8Encoding(false));
        CompilationResult result = await new ProjectCompiler().CompileAsync(projectFile, new CompilerOptions());
        Require(result.Success, Explain(result));
        return result;
    }

    private static object? Invoke(Type type, string method)
        => type.GetMethod(method, BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);

    private static string Explain(CompilationResult result)
        => string.Join(Environment.NewLine, result.Diagnostics.Select(static item => item.Format(false)))
            + Environment.NewLine + result.BuildLog;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
