using System.Reflection;
using EPLab.Compiler;
using EPLab.Compiler.Build;
using EPLab.Compiler.Syntax;

List<(string Name, Func<Task> Run)> tests = new()
{
    ("lexer accepts Unicode and full-width punctuation", LexerUnicode),
    ("parser builds traditional declarations and blocks", ParserCore),
    ("field splitting distinguishes comparisons from generic types", ParserFieldSplitting),
    ("declaration members tolerate blank and comment-only lines", ParserDeclarationSpacing),
    ("one-based array index zero is diagnosed", ArrayZeroDiagnostic),
    ("generated C# builds and preserves E array semantics", BuildAndRunArray),
    ("approximately-equal follows verified prefix semantics", BuildAndRunPrefix),
    ("operator precedence survives parsing and code generation", BuildAndRunPrecedence),
    ("unary and integer-divide precedence follow the documented table", BuildAndRunPrecedenceEdges),
    ("all loop forms preserve Easy control flow", BuildAndRunLoops),
    ("directive parentheses trim only a complete outer pair", BuildAndRunDirectiveParentheses),
    ("default-only choices and handler-free tries are unconditional blocks", BuildAndRunDegenerateBlocks),
    ("range loops preserve integer and float counter types", BuildAndRunTypedRangeLoops),
    ("locals receive Easy default values", BuildAndRunDefaultInitialization),
    ("static local initializer captures first-call context once", BuildAndRunStaticLocalInitializer),
    ("numeric suffixes preserve their requested widths", BuildAndRunNumericSuffixes),
    ("overflowing U and L literals report diagnostics without crashing", NumericSuffixOverflowDiagnostics),
    ("numeric suffixes are restricted by literal kind", NumericSuffixKindDiagnostics),
    ("documented date literal forms parse consistently", BuildAndRunDates),
    ("invalid calendar dates report an EPL diagnostic", InvalidDateDiagnostic),
    ("multidimensional arrays also support flat one-based indexing", BuildAndRunFlatArray),
    ("array literals use their Easy destination element type", AuditRegressionTests.TypedArrayLiteralDestinations),
    ("optional arguments distinguish missing from supplied values", BuildAndRunOptionalMissing),
    ("full-width source punctuation compiles and executes", BuildAndRunFullWidthSource),
    ("ignored value expressions compile as safe statements", BuildAndRunIgnoredExpressions),
    ("interpolated strings embed Easy values", BuildAndRunInterpolation),
    ("malformed interpolation reports precise brace diagnostics", InterpolationDiagnostics),
    ("declared symbols shadow compiler convenience aliases", BuildAliasShadowing),
    ("declaration collisions report friendly diagnostics", AuditRegressionTests.FriendlyDeclarationCollisions),
    ("user symbols ignore case while CLR members preserve it", BuildAndRunCaseInsensitiveSymbols),
    ("params and CLR arrays cross the zero-based boundary explicitly", BuildAndRunClrArrays),
    ("LabAPI config event and RA adapters build for net48", BuildLabApiAdapters),
    ("LabAPI entry and handler spellings canonicalize case", BuildLabApiCaseInsensitiveBindings),
    ("LabAPI lifecycle publication and binding validation stay safe", LifecycleSafetyTests.Run),
    ("unsupported class and constructor modifiers report EPL diagnostics", ModifierDiagnostics),
    ("abstract methods report an EPL diagnostic", AbstractMethodDiagnostic),
    ("malformed project files fail safely before generating files", MalformedProjectsFailSafely),
    ("project-relative toolchain folders and copy-local references are stable", ProjectRelativeReferencesAndCopyLocal),
    ("project saves preserve unknown future JSON properties", ProjectSavePreservesUnknownProperties),
    ("project JSON comments are rejected instead of silently erased", ProjectCommentsAreRejected),
    ("backend project errors and opaque failures become diagnostics", BackendFailureDiagnostics),
    ("switching LabAPI plugin to library removes its stale host", RebuildPluginAsLibrary),
    ("plain net48 output uses framework-compatible runtime code", BuildAndRunNet48),
    ("C# errors map back to the 易 source", SourceMappedError),
};

int passed = 0;
foreach ((string name, Func<Task> run) in tests)
{
    try
    {
        await run();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"FAIL {name}");
        Console.Error.WriteLine(exception);
    }
}

Console.WriteLine($"{passed}/{tests.Count} tests passed");
return passed == tests.Count ? 0 : 1;

static Task LexerUnicode()
{
    DiagnosticBag diagnostics = new();
    IReadOnlyList<SyntaxToken> tokens = new ExpressionLexer("日志.信息（$“你好，{玩家.Nickname}”）", "词法.易", 3).Lex(diagnostics);
    Require(!diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.Select(static item => item.Format(true))));
    Require(tokens.Any(static token => token.Kind == TokenKind.InterpolatedString), "interpolated string token missing");
    Require(tokens.Count(static token => token.Kind == TokenKind.Identifier) >= 2, "Unicode identifiers missing");
    return Task.CompletedTask;
}

static Task ParserCore()
{
    const string source = """
        .版本 2
        .程序集 算术, , 公开
        .程序集变量 总数, 整数型

        .子程序 相加, 整数型, 公开
        .参数 左, 整数型
        .参数 右, 整数型
        .局部变量 次数, 整数型

        .如果（左 ＞ 右）
        返回（左 ＋ 右）
        .否则
        .计次循环首（2，次数）
        总数 ＝ 总数 ＋ 次数
        .计次循环尾（）
        返回（右 ＋ 左）
        .如果结束
        """;
    DiagnosticBag diagnostics = new();
    CompilationUnitSyntax unit = new SourceParser("算术.易", source, diagnostics).Parse();
    Require(!diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.Select(static item => item.Format(true))));
    ClassDeclarationSyntax type = unit.Declarations.OfType<ClassDeclarationSyntax>().Single();
    Require(type.Methods.Count == 1, "method was not parsed");
    Require(type.Methods[0].Statements.Single() is IfStatementSyntax, "if block was not parsed");
    return Task.CompletedTask;
}

static Task ParserFieldSplitting()
{
    const string source = """
        .版本 2
        .配置项 Flag, 逻辑型, 1 < 2, “flag”, “Comparison default”, “比较默认值”
        .程序集 字段, , 公开
        .程序集变量 映射, 字典<文本型, 列表<整数型>>, 公开
        """;
    DiagnosticBag diagnostics = new();
    CompilationUnitSyntax unit = new SourceParser("字段.易", source, diagnostics).Parse();
    Require(!diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.Select(static item => item.Format(true))));
    ConfigItemSyntax config = unit.ConfigItems.Single();
    Require(config.DefaultValue is BinaryExpressionSyntax { Operator: "<" },
        "the comparison default was swallowed as a generic angle-bracket field");
    Require(config.SerializedName == "flag" && config.DescriptionEnglish == "Comparison default",
        "fields after a comparison default were not split");
    FieldDeclarationSyntax field = unit.Declarations.OfType<ClassDeclarationSyntax>().Single().Fields.Single();
    Require(field.Type.Text == "字典<文本型, 列表<整数型>>",
        $"nested generic type was split into declaration fields: {field.Type.Text}");
    return Task.CompletedTask;
}

static Task ParserDeclarationSpacing()
{
    const string source = """
        .版本 2
        .数据类型 坐标, 公开

        ' X comes first.
        .成员 X, 整数型

        ' Y comes second.
        .成员 Y, 整数型

        .枚举 状态, 公开

        ' Values may be visually grouped too.
        .枚举值 开始, 1

        .枚举值 结束, 2

        .DLL命令 原生调用, 整数型, “native.dll”, “native_call”, 公开

        ' Parameters remain part of the DLL declaration.
        .参数 左, 整数型

        .参数 右, 整数型

        .程序集 占位, , 公开
        """;
    DiagnosticBag diagnostics = new();
    CompilationUnitSyntax unit = new SourceParser("间距.易", source, diagnostics).Parse();
    Require(!diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.Select(static item => item.Format(true))));
    Require(unit.Declarations.OfType<StructDeclarationSyntax>().Single().Members.Count == 2,
        "blank/comment lines ended the data-type member list");
    Require(unit.Declarations.OfType<EnumDeclarationSyntax>().Single().Members.Count == 2,
        "blank/comment lines ended the enum value list");
    Require(unit.Declarations.OfType<DllDeclarationSyntax>().Single().Parameters.Count == 2,
        "blank/comment lines ended the DLL parameter list");
    return Task.CompletedTask;
}

static Task ArrayZeroDiagnostic()
{
    const string source = """
        .版本 2
        .程序集 数组测试
        .子程序 读取, 整数型
        .局部变量 项目, 整数型, , “3”
        返回（项目[0]）
        """;
    CompilationResult result = new ProjectCompiler().CheckSource(source, "数组.易");
    Require(result.Diagnostics.Any(static item => item.Id == "EPL1308"), "zero-index warning missing");
    return Task.CompletedTask;
}

static async Task BuildAndRunArray()
{
    const string source = """
        .版本 2
        .程序集 验证, , 公开
        .子程序 数组验证, 整数型, 公开, 静态
        .局部变量 数字, 整数型, , “2, 3”
        数字[2, 2] ＝ 9
        返回（数字[2, 2]）
        """;
    CompilationResult result = await CompileTemporary("ArrayFixture", source);
    Require(result.Success, Explain(result));
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    Type type = assembly.GetType("ArrayFixture.验证", throwOnError: true)!;
    object? value = type.GetMethod("数组验证", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
    Require(Convert.ToInt32(value) == 9, "one-based multidimensional array returned the wrong value");
}

static async Task BuildAndRunPrefix()
{
    const string source = """
        .版本 2
        .程序集 验证, , 公开
        .子程序 前缀, 逻辑型, 公开, 静态
        .参数 完整, 文本型
        .参数 开头, 文本型
        返回（完整 ≈ 开头）
        """;
    CompilationResult result = await CompileTemporary("PrefixFixture", source);
    Require(result.Success, Explain(result));
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    MethodInfo method = assembly.GetType("PrefixFixture.验证", throwOnError: true)!.GetMethod("前缀", BindingFlags.Public | BindingFlags.Static)!;
    Require((bool)method.Invoke(null, new object[] { "abcdef", "abc" })!, "prefix should match");
    Require(!(bool)method.Invoke(null, new object[] { "abcdef", "bcd" })!, "middle substring must not match");
}

static async Task BuildAndRunPrecedence()
{
    const string source = """
        .版本 2
        .程序集 优先级, , 公开

        .子程序 算术, 整数型, 公开, 静态
        返回（2 ＋ 3 × 4 － 8 ＼ 2）

        .子程序 逻辑, 逻辑型, 公开, 静态
        返回（真 或 假 且 假）

        .子程序 位运算, 整数型, 公开, 静态
        返回（1 左移 2 ＋ 1）
        """;
    CompilationResult result = await CompileTemporary("PrecedenceFixture", source);
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    Type type = assembly.GetType("PrecedenceFixture.优先级", throwOnError: true)!;
    Require(InvokeInt(type, "算术") == 10, "multiplication and integer division should bind more tightly than addition");
    Require(InvokeBool(type, "逻辑"), "AND should bind more tightly than OR");
    Require(InvokeInt(type, "位运算") == 8, "addition should bind more tightly than shift");
}

static async Task BuildAndRunPrecedenceEdges()
{
    const string source = """
        .版本 2
        .程序集 边缘优先级, , 公开

        .子程序 一元逻辑, 逻辑型, 公开, 静态
        返回（非 真 且 假）

        .子程序 一元位运算, 整数型, 公开, 静态
        返回（~1 ＋ 2）

        .子程序 意外整除, 整数型, 公开, 静态
        返回（20 ＼ 3 × 2）
        """;

    DiagnosticBag diagnostics = new();
    ExpressionSyntax parsed = new ExpressionParser("－1 × 2", "优先级.易", 1, diagnostics).Parse();
    Require(!diagnostics.HasErrors, string.Join(Environment.NewLine, diagnostics.Select(static item => item.Format(false))));
    Require(parsed is BinaryExpressionSyntax
        {
            Operator: "*",
            Left: UnaryExpressionSyntax { Operator: "-", Operand: LiteralExpressionSyntax }
        }, "unary minus should bind before multiplication in the syntax tree");

    CompilationResult result = await CompileTemporary("PrecedenceEdgeFixture", source);
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    Type type = assembly.GetType("PrecedenceEdgeFixture.边缘优先级", throwOnError: true)!;
    Require(!InvokeBool(type, "一元逻辑"), "unary NOT should bind before boolean AND");
    Require(InvokeInt(type, "一元位运算") == 0, "bitwise unary complement should bind before addition");
    Require(InvokeInt(type, "意外整除") == 3, "20 \\ 3 * 2 is documented as 20 \\ (3 * 2), which truncates to 3");
}

static async Task BuildAndRunLoops()
{
    const string source = """
        .版本 2
        .扩展 CLR 1
        .程序集 循环, , 公开

        .子程序 全部循环, 双精度小数型, 公开, 静态
        .局部变量 总数, 双精度小数型
        .局部变量 前次数, 整数型
        .局部变量 后次数, 整数型
        .局部变量 计次, 整数型
        .局部变量 变量值, 双精度小数型

        .判断循环首（前次数 ＜ 3）
        前次数 ＝ 前次数 ＋ 1
        总数 ＝ 总数 ＋ 前次数
        .判断循环尾（）

        .循环判断首（）
        后次数 ＝ 后次数 ＋ 1
        总数 ＝ 总数 ＋ 1
        .循环判断尾（后次数 ＜ 2）

        .计次循环首（3，计次）
        总数 ＝ 总数 ＋ 计次 × 10
        .计次循环尾（）

        .变量循环首（3，1，－1，变量值）
        总数 ＝ 总数 ＋ 变量值
        .变量循环尾（）

        .枚举循环首（成员，整数型，｛4，5｝）
        总数 ＝ 总数 ＋ 成员
        .枚举循环尾（）

        返回（总数）
        """;
    CompilationResult result = await CompileTemporary("LoopFixture", source);
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    Type type = assembly.GetType("LoopFixture.循环", throwOnError: true)!;
    double value = Convert.ToDouble(type.GetMethod("全部循环", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null));
    Require(Math.Abs(value - 83d) < 0.0001d, $"all loop forms produced {value}, expected 83");
}

static async Task BuildAndRunDirectiveParentheses()
{
    const string source = """
        .版本 2
        .程序集 指令括号, , 公开

        .子程序 运行, 整数型, 公开, 静态
        .局部变量 总数, 整数型
        .局部变量 游标, 整数型
        .如果 (真) 且 (真)
        总数 ＝ 1
        .否则
        总数 ＝ 100
        .如果结束
        .变量循环首 (1), (3), (1), 游标
        总数 ＝ 总数 ＋ 游标
        .变量循环尾
        .如果 ((“)” ＝ “)”) 且 (总数 ＝ 7))
        返回（总数）
        .否则
        返回（－1）
        .如果结束
        """;
    CompilationResult result = await CompileTemporary("DirectiveParenthesesFixture", source);
    Require(result.Success, Explain(result));
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    Type type = assembly.GetType("DirectiveParenthesesFixture.指令括号", throwOnError: true)!;
    Require(InvokeInt(type, "运行") == 7,
        "mixed directive expressions or individually parenthesized range fields were trimmed incorrectly");
}

static async Task BuildAndRunDegenerateBlocks()
{
    const string source = """
        .版本 2
        .程序集 简化块, , 公开

        .子程序 运行, 整数型, 公开, 静态
        .局部变量 值, 整数型
        值 ＝ 1
        .判断开始
        .默认
        值 ＝ 值 ＋ 2
        .判断结束
        .尝试
        值 ＝ 值 × 3
        .尝试结束
        .尝试
        值 ＝ 值 ＋ 1
        .最终
        .尝试结束
        返回（值）
        """;
    CompilationResult result = await CompileTemporary("DegenerateBlockFixture", source);
    Require(result.Success, Explain(result));
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    Type type = assembly.GetType("DegenerateBlockFixture.简化块", throwOnError: true)!;
    Require(InvokeInt(type, "运行") == 10,
        "default-only choice or handler-free try did not behave as an unconditional grouping block");
}

static async Task BuildAndRunTypedRangeLoops()
{
    const string source = """
        .版本 2
        .程序集 类型循环, , 公开

        .子程序 整数游标, 整数型, 公开, 静态
        .局部变量 总数, 整数型
        .局部变量 游标, 整数型
        .变量循环首（1，5，2，游标）
        总数 ＝ 总数 ＋ 游标
        .变量循环尾（）
        返回（总数）

        .子程序 小数游标, 小数型, 公开, 静态
        .局部变量 总数, 小数型
        .局部变量 游标, 小数型
        .变量循环首（0F，1F，0.5F，游标）
        总数 ＝ 总数 ＋ 游标
        .变量循环尾（）
        返回（总数）
        """;
    CompilationResult result = await CompileTemporary("TypedRangeFixture", source);
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    Type type = assembly.GetType("TypedRangeFixture.类型循环", throwOnError: true)!;
    Require(InvokeInt(type, "整数游标") == 9, "integer range should visit 1, 3, and 5 without a double assignment");
    float floatValue = Convert.ToSingle(Invoke(type, "小数游标"));
    Require(Math.Abs(floatValue - 1.5F) < 0.0001F, $"float range returned {floatValue}, expected 1.5");
}

static async Task BuildAndRunDefaultInitialization()
{
    const string source = """
        .版本 2
        .程序集 默认值, , 公开

        .子程序 默认整数, 整数型, 公开, 静态
        .局部变量 值, 整数型
        返回（值）

        .子程序 默认逻辑, 逻辑型, 公开, 静态
        .局部变量 值, 逻辑型
        返回（值）

        .子程序 默认文本, 文本型, 公开, 静态
        .局部变量 值, 文本型
        返回（值）

        .子程序 默认日期, 日期时间型, 公开, 静态
        .局部变量 值, 日期时间型
        返回（值）

        .子程序 默认数组成员, 整数型, 公开, 静态
        .局部变量 值, 整数型, , “2”
        返回（值[1]）

        .子程序 省略类型, 整数型, 公开, 静态
        .局部变量 值
        返回（值）
        """;
    CompilationResult result = await CompileTemporary("DefaultFixture", source);
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    Type type = assembly.GetType("DefaultFixture.默认值", throwOnError: true)!;
    Require(InvokeInt(type, "默认整数") == 0, "integer default should be zero");
    Require(!InvokeBool(type, "默认逻辑"), "logical default should be false");
    Require((string)Invoke(type, "默认文本")! == string.Empty, "text default should be empty text");
    Require((DateTime)Invoke(type, "默认日期")! == new DateTime(1899, 12, 30), "date default should be 1899-12-30");
    Require(InvokeInt(type, "默认数组成员") == 0, "a new numeric array member should be zero");
    Require(InvokeInt(type, "省略类型") == 0, "an omitted variable type should default to integer initialized to zero");
}

static async Task BuildAndRunStaticLocalInitializer()
{
    const string source = """
        .版本 2
        .程序集 静态局部, , 公开
        .程序集变量 Offset, 整数型, 公开

        .子程序 Remember, 整数型, 公开
        .参数 Seed, 整数型
        .局部变量 Saved, 整数型, 静态, , Seed ＋ Offset
        返回（Saved）

        .子程序 RememberSize, 整数型, 公开
        .参数 Size, 整数型
        .局部变量 Cache, 整数型, 静态, Size
        返回（取数组成员数（Cache））
        """;
    CompilationResult result = await CompileTemporary("StaticLocalFixture", source);
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    Type type = assembly.GetType("StaticLocalFixture.静态局部", throwOnError: true)!;
    object instance = Activator.CreateInstance(type)!;
    FieldInfo offset = type.GetField("Offset", BindingFlags.Public | BindingFlags.Instance)!;
    MethodInfo remember = type.GetMethod("Remember", BindingFlags.Public | BindingFlags.Instance)!;
    MethodInfo rememberSize = type.GetMethod("RememberSize", BindingFlags.Public | BindingFlags.Instance)!;

    offset.SetValue(instance, 10);
    Require(Convert.ToInt32(remember.Invoke(instance, new object[] { 5 })) == 15,
        "static local did not capture its first-call parameter and instance field");
    offset.SetValue(instance, 100);
    Require(Convert.ToInt32(remember.Invoke(instance, new object[] { 200 })) == 15,
        "static local initializer ran more than once");
    Require(Convert.ToInt32(rememberSize.Invoke(instance, new object[] { 3 })) == 3,
        "static array shape did not use its first-call parameter");
    Require(Convert.ToInt32(rememberSize.Invoke(instance, new object[] { 9 })) == 3,
        "static array shape was rebuilt after its first call");

    string program = await File.ReadAllTextAsync(Path.Combine(result.GeneratedDirectory!, "EPLab.Program.g.cs"));
    Require(program.Contains("_已初始化", StringComparison.Ordinal), "hoisted static locals did not emit initialization guards");
}

static async Task BuildAndRunNumericSuffixes()
{
    const string source = """
        .版本 2
        .程序集 数值后缀, , 公开

        .子程序 长整数, 通用型, 公开, 静态
        返回（1L）

        .子程序 无符号整数, 通用型, 公开, 静态
        返回（2U）

        .子程序 无符号长整数, 通用型, 公开, 静态
        返回（3UL）

        .子程序 单精度, 通用型, 公开, 静态
        返回（4F）

        .子程序 双精度, 通用型, 公开, 静态
        返回（5D）

        .子程序 十六进制长整数, 通用型, 公开, 静态
        返回（0x10L）
        """;
    CompilationResult result = await CompileTemporary("NumericSuffixFixture", source);
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    Type type = assembly.GetType("NumericSuffixFixture.数值后缀", throwOnError: true)!;
    Require(Invoke(type, "长整数") is long value1 && value1 == 1L, "L suffix should produce Int64 even for a small number");
    Require(Invoke(type, "无符号整数") is uint value2 && value2 == 2U, "U suffix should produce UInt32");
    Require(Invoke(type, "无符号长整数") is ulong value3 && value3 == 3UL, "UL suffix should produce UInt64");
    Require(Invoke(type, "单精度") is float value4 && value4 == 4F, "F suffix should produce Single");
    Require(Invoke(type, "双精度") is double value5 && value5 == 5D, "D suffix should produce Double");
    Require(Invoke(type, "十六进制长整数") is long value6 && value6 == 0x10L, "hexadecimal L suffix should produce Int64");
}

static Task NumericSuffixOverflowDiagnostics()
{
    AssertOverflow("4294967296U", "UInt32");
    AssertOverflow("9223372036854775808L", "Int64");
    return Task.CompletedTask;

    static void AssertOverflow(string literal, string expectedType)
    {
        string source = $"""
            .版本 2
            .程序集 溢出
            .子程序 读取, 通用型, 公开, 静态
            返回（{literal}）
            """;
        CompilationResult result;
        try
        {
            result = new ProjectCompiler().CheckSource(source, "数值溢出.易");
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"{literal} overflowed {expectedType} by crashing the compiler", exception);
        }

        Require(!result.Success, $"{literal} should not fit {expectedType}");
        Require(result.Diagnostics.Any(static item => item.Severity == DiagnosticSeverity.Error),
            $"{literal} returned no error diagnostic: {Explain(result)}");
    }
}

static Task NumericSuffixKindDiagnostics()
{
    foreach (string literal in new[] { "1FF", "1UU", "1.5UL", "1.0L", "1e2U" })
    {
        string source = $"""
            .版本 2
            .程序集 坏后缀
            .子程序 读取, 通用型, 公开, 静态
            返回（{literal}）
            """;
        CompilationResult result = new ProjectCompiler().CheckSource(source, "坏后缀.易");
        Require(!result.Success, $"invalid numeric suffix combination {literal} unexpectedly passed");
        Require(result.Diagnostics.Any(static item => item.Id == "EPL1001"),
            $"{literal} should report EPL1001 instead of leaking to C#: {Explain(result)}");
    }
    return Task.CompletedTask;
}

static async Task BuildAndRunDates()
{
    const string source = """
        .版本 2
        .程序集 日期, , 公开

        .子程序 中文完整, 日期时间型, 公开, 静态
        返回（[2026年8月29日1时2分3秒]）

        .子程序 中文日期, 日期时间型, 公开, 静态
        返回（[2026年8月29日]）

        .子程序 斜线分段, 日期时间型, 公开, 静态
        返回（[2026/8/29/1/2/3]）

        .子程序 斜线紧时间, 日期时间型, 公开, 静态
        返回（[2026/8/29/1:2:3]）

        .子程序 斜线时间, 日期时间型, 公开, 静态
        返回（[2026/8/29 1:2:3]）

        .子程序 斜线日期, 日期时间型, 公开, 静态
        返回（[2026/8/29]）

        .子程序 横线分段, 日期时间型, 公开, 静态
        返回（[2026-8-29-1-2-3]）

        .子程序 横线紧时间, 日期时间型, 公开, 静态
        返回（[2026-8-29-1:2:3]）

        .子程序 横线时间, 日期时间型, 公开, 静态
        返回（[2026-8-29 1:2:3]）

        .子程序 横线日期, 日期时间型, 公开, 静态
        返回（[2026-8-29]）

        .子程序 紧凑完整, 日期时间型, 公开, 静态
        返回（[20260829010203]）

        .子程序 紧凑日期, 日期时间型, 公开, 静态
        返回（[20260829]）
        """;
    CompilationResult result = await CompileTemporary("DateFixture", source);
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    Type type = assembly.GetType("DateFixture.日期", throwOnError: true)!;
    DateTime full = new(2026, 8, 29, 1, 2, 3);
    foreach (string method in new[] { "中文完整", "斜线分段", "斜线紧时间", "斜线时间", "横线分段", "横线紧时间", "横线时间", "紧凑完整" })
        Require((DateTime)Invoke(type, method)! == full, $"{method} did not parse as {full:O}");
    DateTime dateOnly = new(2026, 8, 29);
    foreach (string method in new[] { "中文日期", "斜线日期", "横线日期", "紧凑日期" })
        Require((DateTime)Invoke(type, method)! == dateOnly, $"{method} did not parse at midnight");
}

static Task InvalidDateDiagnostic()
{
    const string source = """
        .版本 2
        .程序集 坏日期
        .子程序 读取, 日期时间型, 公开, 静态
        返回（[2026-13-40]）
        """;
    CompilationResult result = new ProjectCompiler().CheckSource(source, "坏日期.易");
    Require(!result.Success, "an impossible calendar date unexpectedly passed checking");
    Require(result.Diagnostics.Any(static item => item.Id == "EPL1004"),
        $"invalid date should report EPL1004 before runtime: {Explain(result)}");
    return Task.CompletedTask;
}

static async Task BuildAndRunFlatArray()
{
    const string source = """
        .版本 2
        .程序集 平铺数组, , 公开
        .子程序 验证, 逻辑型, 公开, 静态
        .局部变量 数字, 整数型, , “2, 3”
        数字[1, 1] ＝ 11
        数字[1, 2] ＝ 12
        数字[1, 3] ＝ 13
        数字[2, 1] ＝ 21
        数字[2, 2] ＝ 22
        数字[2, 3] ＝ 23
        返回（数字[1] ＝ 11 且 数字[3] ＝ 13 且 数字[4] ＝ 21 且 数字[6] ＝ 23）
        """;
    CompilationResult result = await CompileTemporary("FlatArrayFixture", source);
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    Type type = assembly.GetType("FlatArrayFixture.平铺数组", throwOnError: true)!;
    Require(InvokeBool(type, "验证"), "flat one-based indexing should use last-dimension-fastest row-major order");
}

static async Task BuildAndRunOptionalMissing()
{
    const string source = """
        .版本 2
        .程序集 可空参数, , 公开

        .子程序 是否缺省, 逻辑型, 公开, 静态
        .参数 值, 整数型, 可空
        返回（是否为空（值））

        .子程序 未传, 逻辑型, 公开, 静态
        返回（是否缺省（））

        .子程序 已传, 逻辑型, 公开, 静态
        返回（是否缺省（7））
        """;
    CompilationResult result = await CompileTemporary("OptionalFixture", source);
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    Type type = assembly.GetType("OptionalFixture.可空参数", throwOnError: true)!;
    Require(InvokeBool(type, "未传"), "an omitted optional argument should keep the missing sentinel");
    Require(!InvokeBool(type, "已传"), "a supplied optional argument must not look missing");
}

static async Task BuildAndRunFullWidthSource()
{
    const string source = """
        .版本 2
        .程序集 全角，，公开
        .子程序 计算，整数型，公开，静态
        .局部变量 值，整数型
        值 ＝ （1 ＋ 2） × 3
        返回（值）
        """;
    CompilationResult result = await CompileTemporary("FullWidthFixture", source);
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    Type type = assembly.GetType("FullWidthFixture.全角", throwOnError: true)!;
    Require(InvokeInt(type, "计算") == 9, "full-width commas, parentheses, equals, plus, and multiply should execute normally");
}

static async Task BuildAndRunIgnoredExpressions()
{
    const string source = """
        .版本 2
        .程序集 忽略值, , 公开
        .子程序 运行, 整数型, 公开, 静态
        1
        1 ＋ 2
        到文本（1）
        默认值（整数型）
        返回（42）
        """;
    CompilationResult result = await CompileTemporary("IgnoredExpressionFixture", source);
    Require(result.Success, Explain(result));
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    Type type = assembly.GetType("IgnoredExpressionFixture.忽略值", throwOnError: true)!;
    Require(InvokeInt(type, "运行") == 42, "evaluating ignored value expressions changed later control flow");
    string program = await File.ReadAllTextAsync(Path.Combine(result.GeneratedDirectory!, "EPLab.Program.g.cs"));
    Require(program.Contains("GC.KeepAlive", StringComparison.Ordinal),
        "value-only statements were not lowered to safe C# call statements");
}

static async Task BuildAndRunInterpolation()
{
    const string source = """
        .版本 2
        .程序集 插值, , 公开
        .子程序 问候, 文本型, 公开, 静态
        .参数 名字, 文本型
        .参数 次数, 整数型
        返回（$“你好，{名字}！这是第 {次数 ＋ 1} 次。”）
        """;
    CompilationResult result = await CompileTemporary("InterpolationFixture", source);
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    Type type = assembly.GetType("InterpolationFixture.插值", throwOnError: true)!;
    object? value = type.GetMethod("问候", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { "小易", 3 });
    Require((string)value! == "你好，小易！这是第 4 次。", "interpolation did not parse and embed the Easy expression");
}

static Task InterpolationDiagnostics()
{
    AssertDiagnostic("$“多了右括号 }”", "EPL1104");
    AssertDiagnostic("$“少了右括号 {1”", "EPL1105");
    AssertDiagnostic("$“空表达式 {}”", "EPL1106");
    return Task.CompletedTask;

    static void AssertDiagnostic(string expression, string diagnosticId)
    {
        string source = $"""
            .版本 2
            .程序集 坏插值
            .子程序 运行, 文本型, 公开, 静态
            返回（{expression}）
            """;
        CompilationResult result = new ProjectCompiler().CheckSource(source, "坏插值.易");
        Require(!result.Success, $"malformed interpolation {expression} unexpectedly passed checking");
        Require(result.Diagnostics.Any(item => item.Id == diagnosticId),
            $"{expression} should report {diagnosticId}, got: {string.Join(", ", result.Diagnostics.Select(static item => item.Id))}");
    }
}

static async Task BuildAliasShadowing()
{
    const string source = """
        .版本 2
        .扩展 CLR 1
        .扩展 LabAPI 1

        .LabAPI插件 主程序集
        .插件名称 “名字遮蔽验证”
        .插件版本 “1.0.0”

        .程序集 数据盒, , 公开
        .程序集变量 数量, 整数型, 公开

        .程序集 主程序集, , 公开
        .子程序 验证, 整数型, 公开, 静态
        .局部变量 日志, 整数型, , , 1
        .局部变量 配置, 整数型, , , 2
        .局部变量 环境, 整数型, , , 3
        .局部变量 盒子, 数据盒
        盒子.数量 ＝ 4
        返回（日志 × 1000 ＋ 配置 × 100 ＋ 环境 × 10 ＋ 盒子.数量）
        """;
    CompilationResult result = await CompileTemporary(
        "AliasShadowFixture",
        source,
        target: "labapi-net48",
        references: new[] { "game:LabApi", "game:Assembly-CSharp", "game:CommandSystem.Core" },
        managedDirectory: DefaultManagedDirectory());
    string program = await File.ReadAllTextAsync(Path.Combine(result.GeneratedDirectory!, "EPLab.Program.g.cs"));
    Require(program.Contains("int 日志 =", StringComparison.Ordinal), "local 日志 was replaced by the LabAPI logger alias");
    Require(program.Contains("int 配置 =", StringComparison.Ordinal), "local 配置 was replaced by the generated config alias");
    Require(program.Contains("int 环境 =", StringComparison.Ordinal), "local 环境 was replaced by System.Environment");
    Require(program.Contains("盒子.数量", StringComparison.Ordinal), "user member 数量 was rewritten to the collection Count alias");
    Require(!program.Contains("盒子.Count", StringComparison.Ordinal), "user member 数量 became Count");
}

static async Task BuildAndRunCaseInsensitiveSymbols()
{
    const string source = """
        .版本 2
        .扩展 CLR 1

        .枚举 RunMode, 公开
        .枚举值 Ready, 7

        .程序集 ValueBox, , 公开
        .程序集变量 Amount, 整数型, 公开
        .子程序 AddOne, 整数型, 公开
        .参数 Input, 整数型
        返回（input ＋ 1）

        .程序集 CaseRunner, , 公开
        .子程序 Compute, 整数型, 公开, 静态
        .局部变量 Box, valuebox, , , 创建对象（VALUEBOX）
        box.amount ＝ 转换类型（整数型，#runmode.ready）
        返回（box.addone（box.amount））

        .子程序 Run, 整数型, 公开, 静态
        返回（compute（））

        .子程序 ClrCorrect, 整数型, 公开, 静态
        返回（Math.Abs（－3））
        """;
    const string wrongClrCase = """
        .版本 2
        .扩展 CLR 1
        .程序集 外部大小写, , 公开
        .子程序 Run, 整数型, 公开, 静态
        返回（Math.abs（－3））
        """;

    CompilationResult result = await CompileTemporary("CaseInsensitiveFixture", source);
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    Type runner = assembly.GetType("CaseInsensitiveFixture.CaseRunner", throwOnError: true)!;
    Require(InvokeInt(runner, "Run") == 8,
        "differently-cased user method/type/field/member/enum references did not resolve to their declarations");
    Require(InvokeInt(runner, "ClrCorrect") == 3, "correctly-cased external CLR member stopped working");

    CompilationResult wrongCase = await CompileTemporary("ClrCaseSensitiveFixture", wrongClrCase, expectSuccess: false);
    Require(!wrongCase.Success, "external CLR member Math.abs was incorrectly canonicalized to Math.Abs");
    Require(wrongCase.Diagnostics.Any(static item => item.Severity == DiagnosticSeverity.Error
        && item.Span.FilePath.EndsWith("主程序.易", StringComparison.OrdinalIgnoreCase)), Explain(wrongCase));
}

static async Task BuildAndRunClrArrays()
{
    const string easyArrayParameter = """
        .版本 2
        .程序集 易数组参数
        .子程序 读取, 整数型, 公开, 静态
        .参数 值, 整数型, 数组
        返回（值[0]）
        """;
    const string source = """
        .版本 2
        .扩展 CLR 1
        .程序集 原生数组, , 公开

        .子程序 参数求和, 整数型, 公开, 静态
        .参数 值, 整数型, 参数数组
        返回（值[0] ＋ 值[1] ＋ 值[2]）

        .子程序 验证, 整数型, 公开, 静态
        .局部变量 易数组, 整数型, , “3”
        .局部变量 原生, CLR数组<整数型>
        .局部变量 回来, 整数型, , “0”
        易数组[1] ＝ 1
        易数组[2] ＝ 2
        易数组[3] ＝ 3
        原生 ＝ 到CLR数组（易数组）
        回来 ＝ 从CLR数组（原生）
        返回（参数求和（原生） ＋ 原生[0] × 10 ＋ 回来[1] × 100）
        """;
    CompilationResult easyArrayCheck = new ProjectCompiler().CheckSource(easyArrayParameter, "易数组参数.易");
    Require(easyArrayCheck.Diagnostics.Any(static item => item.Id == "EPL1308"),
        "an ordinary EArray parameter lost its one-based index-zero warning");
    CompilationResult result = await CompileTemporary("ClrArrayFixture", source);
    Require(!result.Diagnostics.Any(static item => item.Id == "EPL1308"),
        "a params/CLR array index 0 was incorrectly diagnosed as a one-based EArray access");
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    Type type = assembly.GetType("ClrArrayFixture.原生数组", throwOnError: true)!;
    Require(InvokeInt(type, "验证") == 116, "params or explicit EArray/CLR-array copying used the wrong index base");
    string program = await File.ReadAllTextAsync(Path.Combine(result.GeneratedDirectory!, "EPLab.Program.g.cs"));
    Require(program.Contains("params int[] 值", StringComparison.Ordinal), "参数数组 did not emit a CLR params array");
    Require(program.Contains("int[] 原生", StringComparison.Ordinal), "CLR数组<整数型> did not emit int[]");
}

static async Task BuildLabApiAdapters()
{
    const string source = """
        .版本 2
        .扩展 CLR 1
        .扩展 LabAPI 1
        .引用命名空间 LabApi.Events.Handlers

        .LabAPI插件 主程序集
        .插件名称 “适配器验证”
        .插件说明 “编译器集成测试”
        .插件作者 “EPLab”
        .插件版本 “1.2.3”
        .配置项 启用, 逻辑型, 真, “is_enabled”, “Enable this test”, “启用这个测试”
        .订阅事件 ServerEvents.WaitingForPlayers, 等待玩家
        .RA命令 “易验证”, “echeck|易查”, “Checks the adapter”, 执行命令

        .程序集 主程序集, , 公开
        .程序集变量 等待次数, 整数型

        .子程序 插件启动, , 公开
        等待次数 ＝ 0

        .子程序 等待玩家, , 公开
        等待次数 ＝ 等待次数 ＋ 1

        .子程序 配置已启用, 逻辑型, 公开
        返回（配置.启用）

        .子程序 执行命令, 逻辑型, 公开
        .参数 上下文, CommandContext
        上下文.回复 ＝ “adapter ok”
        上下文.成功 ＝ 真
        返回（真）
        """;
    string managed = DefaultManagedDirectory();
    Require(File.Exists(Path.Combine(managed, "LabApi.dll")), $"LabApi.dll is required for the LabAPI conformance test: {managed}");
    CompilationResult result = await CompileTemporary(
        "LabApiAdapterFixture",
        source,
        target: "labapi-net48",
        references: new[] { "game:LabApi", "game:Assembly-CSharp", "game:CommandSystem.Core" },
        managedDirectory: managed);
    Require(result.Success, Explain(result));
    string hostPath = Path.Combine(result.GeneratedDirectory!, "EPLab.LabApi.g.cs");
    string host = await File.ReadAllTextAsync(hostPath);
    Require(host.Contains("public sealed class __EplabConfig", StringComparison.Ordinal), "generated config class missing");
    Require(host.Contains("public bool 启用 { get; set; } =", StringComparison.Ordinal)
        && host.Contains("(true)", StringComparison.Ordinal), "generated config property/default missing");
    Require(host.Contains("YamlMember(Alias = \"is_enabled\")", StringComparison.Ordinal), "serialized config name missing");
    Require(host.Contains("ServerEvents.WaitingForPlayers += Entry.等待玩家;", StringComparison.Ordinal), "event subscription adapter missing");
    Require(host.Contains("ServerEvents.WaitingForPlayers -= failedEntry.等待玩家;", StringComparison.Ordinal)
        && host.Contains("ServerEvents.WaitingForPlayers -= activeEntry.等待玩家;", StringComparison.Ordinal),
        "rollback or normal event unsubscription adapter missing");
    Require(host.Contains("[CommandHandler(typeof(RemoteAdminCommandHandler))]", StringComparison.Ordinal), "RA command handler attribute missing");
    Require(host.Contains("public string Command => \"易验证\";", StringComparison.Ordinal), "RA command name missing");
    Require(host.Contains("new[] { \"echeck\", \"易查\" }", StringComparison.Ordinal), "RA command aliases missing");
    Require(host.Contains("plugin.Entry.执行命令(context)", StringComparison.Ordinal), "RA command dispatch missing");
    string generatedProject = await File.ReadAllTextAsync(Directory.GetFiles(result.GeneratedDirectory!, "*.generated.csproj").Single());
    Require(generatedProject.Contains("<TargetFramework>net48</TargetFramework>", StringComparison.Ordinal), "LabAPI output did not target net48");
}

static async Task BuildLabApiCaseInsensitiveBindings()
{
    const string source = """
        .版本 2
        .扩展 CLR 1
        .扩展 LabAPI 1
        .引用命名空间 LabApi.Events.Handlers

        .LabAPI插件 pluginentry
        .插件名称 “LabAPI 大小写验证”
        .插件版本 “1.0.0”
        .订阅事件 ServerEvents.WaitingForPlayers, onwaiting
        .RA命令 “casecheck”, “”, “Checks canonical names”, runcommand

        .程序集 PluginEntry, , 公开
        .子程序 OnWaiting, , 公开

        .子程序 RunCommand, 逻辑型, 公开
        .参数 Context, CommandContext
        context.回复 ＝ “ok”
        返回（真）
        """;
    CompilationResult result = await CompileTemporary(
        "LabApiCaseFixture",
        source,
        target: "labapi-net48",
        references: new[] { "game:LabApi", "game:Assembly-CSharp", "game:CommandSystem.Core" },
        managedDirectory: DefaultManagedDirectory());
    string host = await File.ReadAllTextAsync(Path.Combine(result.GeneratedDirectory!, "EPLab.LabApi.g.cs"));
    Require(host.Contains("private PluginEntry? entry;", StringComparison.Ordinal)
        && host.Contains("internal PluginEntry Entry => entry ??", StringComparison.Ordinal)
        && host.Contains("entry = new PluginEntry();", StringComparison.Ordinal),
        "case-insensitive .LabAPI插件 entry spelling was not canonicalized");
    Require(host.Contains("Entry.OnWaiting", StringComparison.Ordinal),
        "case-insensitive event handler spelling was not canonicalized");
    Require(host.Contains("plugin.Entry.RunCommand(context)", StringComparison.Ordinal),
        "case-insensitive command handler spelling was not canonicalized");
    Require(!host.Contains("Entry.onwaiting", StringComparison.Ordinal)
        && !host.Contains("Entry.runcommand", StringComparison.Ordinal),
        "raw non-canonical LabAPI handler spellings leaked into generated C#");
}

static Task ModifierDiagnostics()
{
    const string staticClass = """
        .版本 2
        .程序集 静态类, , 公开, 静态
        """;
    const string staticConstructor = """
        .版本 2
        .程序集 构造宿主, , 公开
        .构造子程序 初始化, , 公开, 静态
        """;
    CompilationResult classResult = new ProjectCompiler().CheckSource(staticClass, "静态类.易");
    Require(classResult.Diagnostics.Any(static item => item.Id == "CLR2105"),
        $"a silently dropped static class modifier should report CLR2105: {Explain(classResult)}");
    CompilationResult constructorResult = new ProjectCompiler().CheckSource(staticConstructor, "静态构造.易");
    Require(constructorResult.Diagnostics.Any(static item => item.Id == "CLR2106"),
        $"a silently dropped constructor modifier should report CLR2106: {Explain(constructorResult)}");
    return Task.CompletedTask;
}

static Task AbstractMethodDiagnostic()
{
    const string source = """
        .版本 2
        .程序集 抽象宿主, , 公开, 抽象
        .子程序 待实现, 整数型, 公开, 抽象
        """;
    CompilationResult result = new ProjectCompiler().CheckSource(source, "抽象.易");
    Require(!result.Success, "an abstract method with an emitted body unexpectedly passed checking");
    Require(result.Diagnostics.Any(static item => item.Id == "CLR2107"),
        $"abstract method should report CLR2107 before C# emission: {Explain(result)}");
    return Task.CompletedTask;
}

static async Task RebuildPluginAsLibrary()
{
    const string pluginSource = """
        .版本 2
        .扩展 CLR 1
        .扩展 LabAPI 1
        .LabAPI插件 主程序集
        .插件名称 “先是插件”
        .插件版本 “1.0.0”
        .程序集 主程序集, , 公开
        """;
    const string librarySource = """
        .版本 2
        .程序集 普通库, , 公开
        .子程序 答案, 整数型, 公开, 静态
        返回（42）
        """;

    string directory = Path.Combine(Path.GetTempPath(), "eplab-tests", "PluginToLibraryFixture-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(directory, "src"));
    string projectFile = Path.Combine(directory, "project.eplabproj");
    string sourceFile = Path.Combine(directory, "src", "主程序.易");
    EPLabProject project = new()
    {
        Name = "PluginToLibraryFixture",
        RootNamespace = "PluginToLibraryFixture",
        Target = "labapi-net48",
        SourceDirectories = new List<string> { "src" },
        References = new List<string> { "game:LabApi", "game:Assembly-CSharp", "game:CommandSystem.Core" },
        ManagedDirectory = DefaultManagedDirectory(),
        OutputDirectory = "bin",
    };
    project.Save(projectFile);
    await File.WriteAllTextAsync(sourceFile, pluginSource, new System.Text.UTF8Encoding(false));
    CompilationResult pluginResult = await new ProjectCompiler().CompileAsync(projectFile, new CompilerOptions());
    Require(pluginResult.Success, Explain(pluginResult));
    string staleHost = Path.Combine(pluginResult.GeneratedDirectory!, "EPLab.LabApi.g.cs");
    Require(File.Exists(staleHost), "the first plugin build did not generate its LabAPI host");

    project.Target = "library-net8";
    project.References.Clear();
    project.ManagedDirectory = null;
    project.Save(projectFile);
    await File.WriteAllTextAsync(sourceFile, librarySource, new System.Text.UTF8Encoding(false));
    CompilationResult libraryResult = await new ProjectCompiler().CompileAsync(projectFile, new CompilerOptions());
    Require(libraryResult.Success, Explain(libraryResult));
    Require(!File.Exists(staleHost), "EPLab.LabApi.g.cs survived after the project stopped being a LabAPI plugin");
    Assembly assembly = Assembly.LoadFile(libraryResult.AssemblyPath!);
    Type type = assembly.GetType("PluginToLibraryFixture.普通库", throwOnError: true)!;
    Require(InvokeInt(type, "答案") == 42, "rebuilt plain library did not contain its new source");
}

static async Task BuildAndRunNet48()
{
    const string source = """
        .版本 2
        .程序集 框架验证, , 公开
        .子程序 运行, 整数型, 公开, 静态
        .局部变量 数字, 整数型, , “2”
        数字[1] ＝ 41
        返回（数字[1] ＋ 1）
        """;
    CompilationResult result = await CompileTemporary("Net48Fixture", source, target: "library-net48");
    string generatedProject = await File.ReadAllTextAsync(Directory.GetFiles(result.GeneratedDirectory!, "*.generated.csproj").Single());
    Require(generatedProject.Contains("<TargetFramework>net48</TargetFramework>", StringComparison.Ordinal), "plain framework output did not target net48");
    Assembly assembly = Assembly.LoadFile(result.AssemblyPath!);
    Type type = assembly.GetType("Net48Fixture.框架验证", throwOnError: true)!;
    Require(InvokeInt(type, "运行") == 42, "net48 output could not execute its generated runtime");
}

static async Task SourceMappedError()
{
    const string source = """
        .版本 2
        .扩展 CLR 1
        .程序集 错误样例, , 公开
        .子程序 运行, , 公开, 静态
        完全不存在的方法（）
        """;
    CompilationResult result = await CompileTemporary("MappedError", source, expectSuccess: false);
    Require(!result.Success, "invalid CLR call unexpectedly built");
    Diagnostic? diagnostic = result.Diagnostics.FirstOrDefault(static item => item.Severity == DiagnosticSeverity.Error && item.Span.FilePath.EndsWith("主程序.易", StringComparison.OrdinalIgnoreCase));
    Require(diagnostic is not null, Explain(result));
    Require(diagnostic!.Span.Line == 5, $"expected mapped line 5, got {diagnostic.Span.Line}");
}

static async Task MalformedProjectsFailSafely()
{
    (string Label, string Name, string RootNamespace, string OutputDirectory, string SourceDirectories, string References, string Diagnostic)[] cases =
    {
        ("null name", "null", "\"SafeProject\"", "\"bin\"", "[\"src\"]", "[]", "EPL0007"),
        ("empty name", "\"\"", "\"SafeProject\"", "\"bin\"", "[\"src\"]", "[]", "EPL0007"),
        ("dot name", "\".\"", "\"SafeProject\"", "\"bin\"", "[\"src\"]", "[]", "EPL0007"),
        ("parent name", "\"..\"", "\"SafeProject\"", "\"bin\"", "[\"src\"]", "[]", "EPL0007"),
        ("traversing name", "\"../escaped\"", "\"SafeProject\"", "\"bin\"", "[\"src\"]", "[]", "EPL0007"),
        ("null namespace", "\"SafeProject\"", "null", "\"bin\"", "[\"src\"]", "[]", "EPL0008"),
        ("invalid namespace", "\"SafeProject\"", "\"not a namespace\"", "\"bin\"", "[\"src\"]", "[]", "EPL0008"),
        ("null output", "\"SafeProject\"", "\"SafeProject\"", "null", "[\"src\"]", "[]", "EPL0009"),
        ("empty output", "\"SafeProject\"", "\"SafeProject\"", "\"\"", "[\"src\"]", "[]", "EPL0009"),
        ("dot output", "\"SafeProject\"", "\"SafeProject\"", "\".\"", "[\"src\"]", "[]", "EPL0009"),
        ("traversing output", "\"SafeProject\"", "\"SafeProject\"", "\"../escaped\"", "[\"src\"]", "[]", "EPL0009"),
        ("normalized traversal output", "\"SafeProject\"", "\"SafeProject\"", "\"bin/../escaped\"", "[\"src\"]", "[]", "EPL0009"),
        ("null sources", "\"SafeProject\"", "\"SafeProject\"", "\"bin\"", "null", "[]", "EPL0010"),
        ("empty sources", "\"SafeProject\"", "\"SafeProject\"", "\"bin\"", "[]", "[]", "EPL0010"),
        ("null source", "\"SafeProject\"", "\"SafeProject\"", "\"bin\"", "[null]", "[]", "EPL0010"),
        ("traversing source", "\"SafeProject\"", "\"SafeProject\"", "\"bin\"", "[\"../src\"]", "[]", "EPL0010"),
        ("null references", "\"SafeProject\"", "\"SafeProject\"", "\"bin\"", "[\"src\"]", "null", "EPL0011"),
        ("null reference", "\"SafeProject\"", "\"SafeProject\"", "\"bin\"", "[\"src\"]", "[null]", "EPL0011"),
        ("empty reference", "\"SafeProject\"", "\"SafeProject\"", "\"bin\"", "[\"src\"]", "[\"\"]", "EPL0011"),
        ("traversing game reference", "\"SafeProject\"", "\"SafeProject\"", "\"bin\"", "[\"src\"]", "[\"game:../outside\"]", "EPL0011"),
        ("empty global reference", "\"SafeProject\"", "\"SafeProject\"", "\"bin\"", "[\"src\"]", "[\"global:\"]", "EPL0011"),
    };

    foreach ((string label, string name, string rootNamespace, string outputDirectory, string sourceDirectories, string references, string diagnostic) in cases)
    {
        string directory = Path.Combine(Path.GetTempPath(), "eplab-tests", "BadProject-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "src"));
        await File.WriteAllTextAsync(
            Path.Combine(directory, "src", "主程序.易"),
            ".版本 2\n.程序集 安全项目, , 公开\n",
            new System.Text.UTF8Encoding(false));
        string json = $$"""
            {
              "format": 1,
              "name": {{name}},
              "target": "syntax-only",
              "rootNamespace": {{rootNamespace}},
              "sourceDirectories": {{sourceDirectories}},
              "references": {{references}},
              "outputDirectory": {{outputDirectory}}
            }
            """;
        string projectFile = Path.Combine(directory, "project.eplabproj");
        await File.WriteAllTextAsync(projectFile, json, new System.Text.UTF8Encoding(false));

        CompilationResult result;
        try
        {
            result = await new ProjectCompiler().CompileAsync(projectFile, new CompilerOptions(CheckOnly: true));
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"Malformed project case '{label}' crashed instead of reporting a diagnostic.", exception);
        }

        Require(!result.Success, $"Malformed project case '{label}' unexpectedly passed.");
        Require(result.Diagnostics.Any(item => item.Id == diagnostic),
            $"Malformed project case '{label}' expected {diagnostic}: {Explain(result)}");
        Require(result.GeneratedDirectory is null, $"Malformed project case '{label}' generated files before validation.");
        Require(!Directory.Exists(Path.Combine(directory, "obj")), $"Malformed project case '{label}' wrote obj files.");
        Require(!Directory.Exists(Path.Combine(directory, "bin")), $"Malformed project case '{label}' wrote bin files.");
    }
}

static async Task ProjectRelativeReferencesAndCopyLocal()
{
    const string helperSource = """
        .版本 2
        .程序集 帮助程序, , 公开
        .子程序 答案, 整数型, 公开, 静态
        返回（42）
        """;
    CompilationResult helper = await CompileTemporary("CopyLocalProbe", helperSource);

    string directory = Path.Combine(Path.GetTempPath(), "eplab-tests", "ProjectReferences-" + Guid.NewGuid().ToString("N"));
    string sourceDirectory = Path.Combine(directory, "src");
    string managedDirectory = Path.Combine(directory, "toolchain", "managed");
    string globalDirectory = Path.Combine(directory, "toolchain", "global");
    string helperDirectory = Path.Combine(directory, "helpers");
    Directory.CreateDirectory(sourceDirectory);
    Directory.CreateDirectory(managedDirectory);
    Directory.CreateDirectory(globalDirectory);
    Directory.CreateDirectory(helperDirectory);

    string gameAssemblyName = Path.GetFileName(typeof(System.Text.Json.JsonSerializer).Assembly.Location);
    string globalAssemblyName = Path.GetFileName(typeof(System.Collections.Concurrent.ConcurrentDictionary<,>).Assembly.Location);
    File.Copy(typeof(System.Text.Json.JsonSerializer).Assembly.Location, Path.Combine(managedDirectory, gameAssemblyName));
    File.Copy(typeof(System.Collections.Concurrent.ConcurrentDictionary<,>).Assembly.Location, Path.Combine(globalDirectory, globalAssemblyName));
    string localHelperPath = Path.Combine(helperDirectory, "CopyLocalProbe.dll");
    File.Copy(helper.AssemblyPath!, localHelperPath);

    await File.WriteAllTextAsync(
        Path.Combine(sourceDirectory, "主程序.易"),
        ".版本 2\n.程序集 引用验证, , 公开\n.子程序 答案, 整数型, 公开, 静态\n返回（42）\n",
        new System.Text.UTF8Encoding(false));
    EPLabProject project = new()
    {
        Name = "ProjectReferenceFixture",
        RootNamespace = "ProjectReferenceFixture",
        Target = "library-net8",
        SourceDirectories = new List<string> { "src" },
        References = new List<string>
        {
            "game:" + Path.GetFileNameWithoutExtension(gameAssemblyName),
            "global:" + Path.GetFileNameWithoutExtension(globalAssemblyName),
            "helpers/CopyLocalProbe.dll",
        },
        ManagedDirectory = "toolchain/managed",
        GlobalDependenciesDirectory = "toolchain/global",
        OutputDirectory = "bin",
    };
    string projectFile = Path.Combine(directory, "project.eplabproj");
    project.Save(projectFile);

    string originalCurrentDirectory = Environment.CurrentDirectory;
    CompilationResult result;
    try
    {
        Environment.CurrentDirectory = Path.GetTempPath();
        result = await new ProjectCompiler().CompileAsync(projectFile, new CompilerOptions());
    }
    finally
    {
        Environment.CurrentDirectory = originalCurrentDirectory;
    }

    Require(result.Success, Explain(result));
    string generatedProjectPath = Directory.GetFiles(result.GeneratedDirectory!, "*.generated.csproj").Single();
    System.Xml.Linq.XDocument generatedProject = System.Xml.Linq.XDocument.Load(generatedProjectPath);
    Dictionary<string, System.Xml.Linq.XElement> references = generatedProject
        .Descendants("Reference")
        .ToDictionary(element => element.Attribute("Include")!.Value, StringComparer.OrdinalIgnoreCase);

    AssertReference(Path.GetFileNameWithoutExtension(gameAssemblyName), Path.Combine(managedDirectory, gameAssemblyName), copyLocal: false);
    AssertReference(Path.GetFileNameWithoutExtension(globalAssemblyName), Path.Combine(globalDirectory, globalAssemblyName), copyLocal: false);
    AssertReference("CopyLocalProbe", localHelperPath, copyLocal: true);

    string outputDirectory = Path.GetDirectoryName(result.AssemblyPath!)!;
    Require(File.Exists(Path.Combine(outputDirectory, "CopyLocalProbe.dll")), "ordinary local helper DLL was not copied beside the build output");
    Require(!File.Exists(Path.Combine(outputDirectory, gameAssemblyName)), "game: DLL was incorrectly copied beside the build output");
    Require(!File.Exists(Path.Combine(outputDirectory, globalAssemblyName)), "global: DLL was incorrectly copied beside the build output");

    using System.Text.Json.JsonDocument buildInfo = System.Text.Json.JsonDocument.Parse(
        await File.ReadAllTextAsync(Path.Combine(result.GeneratedDirectory!, "build-info.json")));
    Dictionary<string, System.Text.Json.JsonElement> referenceInfo = buildInfo.RootElement.GetProperty("references")
        .EnumerateArray()
        .ToDictionary(element => element.GetProperty("Name").GetString()!, StringComparer.OrdinalIgnoreCase);
    Require(referenceInfo["CopyLocalProbe"].GetProperty("origin").GetString() == "local"
        && referenceInfo["CopyLocalProbe"].GetProperty("copyLocal").GetBoolean(), "build-info did not record local copy behavior");

    void AssertReference(string name, string expectedPath, bool copyLocal)
    {
        Require(references.TryGetValue(name, out System.Xml.Linq.XElement? reference), $"generated project omitted reference {name}");
        Require(string.Equals(reference!.Attribute("HintPath")?.Value, Path.GetFullPath(expectedPath), StringComparison.OrdinalIgnoreCase),
            $"reference {name} resolved against the process directory instead of the project directory");
        Require(string.Equals(reference.Attribute("Private")?.Value, copyLocal.ToString().ToLowerInvariant(), StringComparison.Ordinal),
            $"reference {name} had the wrong Copy Local value");
    }
}

static async Task ProjectSavePreservesUnknownProperties()
{
    string directory = Path.Combine(Path.GetTempPath(), "eplab-tests", "ProjectSave-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    string projectFile = Path.Combine(directory, "project.eplabproj");
    await File.WriteAllTextAsync(projectFile, """
        {
          "format": 1,
          "name": "FutureProject",
          "target": "library-net8",
          "rootNamespace": "FutureProject",
          "sourceDirectories": ["src"],
          "references": [],
          "outputDirectory": "bin",
          "futureSetting": { "answer": 42, "enabled": true }
        }
        """, new System.Text.UTF8Encoding(false));

    EPLabProject project = EPLabProject.Load(projectFile);
    project.ManagedDirectory = "relative/managed";
    project.Save(projectFile);

    using System.Text.Json.JsonDocument saved = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(projectFile));
    System.Text.Json.JsonElement future = saved.RootElement.GetProperty("futureSetting");
    Require(future.GetProperty("answer").GetInt32() == 42 && future.GetProperty("enabled").GetBoolean(),
        "saving project settings discarded an unknown future JSON property");
    Require(!Directory.EnumerateFiles(directory, ".project.eplabproj.*.tmp").Any(), "atomic project save left a temporary file behind");
}

static async Task ProjectCommentsAreRejected()
{
    string directory = Path.Combine(Path.GetTempPath(), "eplab-tests", "ProjectComments-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(directory, "src"));
    await File.WriteAllTextAsync(Path.Combine(directory, "src", "主程序.易"), ".版本 2\n.程序集 注释项目\n");
    string projectFile = Path.Combine(directory, "project.eplabproj");
    const string jsonWithComment = """
        {
          // EPLab project files use strict JSON so settings saves cannot silently erase comments.
          "format": 1,
          "name": "CommentedProject",
          "target": "syntax-only",
          "rootNamespace": "CommentedProject",
          "sourceDirectories": ["src"],
          "references": [],
          "outputDirectory": "bin"
        }
        """;
    await File.WriteAllTextAsync(projectFile, jsonWithComment, new System.Text.UTF8Encoding(false));

    CompilationResult result = await new ProjectCompiler().CompileAsync(projectFile, new CompilerOptions(CheckOnly: true));
    Require(!result.Success && result.Diagnostics.Any(static diagnostic => diagnostic.Id == "EPL0001"),
        "comment-containing project was accepted instead of reporting a project-read diagnostic");
    Require(await File.ReadAllTextAsync(projectFile) == jsonWithComment, "rejected comment-containing project was unexpectedly rewritten");
    Require(!Directory.Exists(Path.Combine(directory, "obj")), "rejected comment-containing project generated files");
}

static Task BackendFailureDiagnostics()
{
    const string projectPath = @"C:\fixture\Fixture.generated.csproj";
    DiagnosticBag projectErrors = new();
    ProjectCompiler.RecordBuildDiagnostics(
        @"C:\fixture\Fixture.generated.csproj : error NU1301: Unable to load the service index. [C:\fixture\Fixture.generated.csproj]",
        1,
        projectErrors,
        projectPath);
    Diagnostic projectError = projectErrors.Single(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    Require(projectError.Id == "CLRNU1301", $"project-level backend error got unexpected ID {projectError.Id}");
    Require(projectError.Span.FilePath == projectPath, "project-level backend error did not point to the generated project");
    Require(!projectErrors.Any(static diagnostic => diagnostic.Id == "CLR2202"), "parsed project error also received the opaque-failure fallback");

    DiagnosticBag opaqueFailure = new();
    ProjectCompiler.RecordBuildDiagnostics("Build stopped without a structured diagnostic.", 9, opaqueFailure, projectPath);
    Diagnostic fallback = opaqueFailure.Single();
    Require(fallback.Id == "CLR2202" && fallback.Severity == DiagnosticSeverity.Error, "opaque non-zero backend exit did not get a fallback error");
    Require(!string.IsNullOrWhiteSpace(fallback.SuggestionEnglish), "backend fallback error was not actionable");
    return Task.CompletedTask;
}

static async Task<CompilationResult> CompileTemporary(
    string name,
    string source,
    bool expectSuccess = true,
    string target = "library-net8",
    IEnumerable<string>? references = null,
    string? managedDirectory = null)
{
    string directory = Path.Combine(Path.GetTempPath(), "eplab-tests", name + "-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(directory, "src"));
    EPLabProject project = new()
    {
        Name = name,
        RootNamespace = name,
        Target = target,
        SourceDirectories = new List<string> { "src" },
        References = references?.ToList() ?? new List<string>(),
        ManagedDirectory = managedDirectory,
        OutputDirectory = "bin",
    };
    string projectFile = Path.Combine(directory, "project.eplabproj");
    project.Save(projectFile);
    await File.WriteAllTextAsync(Path.Combine(directory, "src", "主程序.易"), source, new System.Text.UTF8Encoding(false));
    CompilationResult result = await new ProjectCompiler().CompileAsync(projectFile, new CompilerOptions());
    if (expectSuccess)
        Require(result.Success, Explain(result));
    return result;
}

static string DefaultManagedDirectory()
    => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        "Steam",
        "steamapps",
        "common",
        "SCP Secret Laboratory Dedicated Server",
        "SCPSL_Data",
        "Managed");

static object? Invoke(Type type, string method)
    => type.GetMethod(method, BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);

static int InvokeInt(Type type, string method) => Convert.ToInt32(Invoke(type, method));

static bool InvokeBool(Type type, string method) => Convert.ToBoolean(Invoke(type, method));

static string Explain(CompilationResult result)
    => string.Join(Environment.NewLine, result.Diagnostics.Select(static item => item.Format(false))) + Environment.NewLine + result.BuildLog;

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
