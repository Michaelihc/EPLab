using EPLab.Compiler;
using EPLab.Compiler.CodeGen;
using EPLab.Compiler.Semantics;
using EPLab.Compiler.Syntax;

internal static class LifecycleSafetyTests
{
    public static Task Run()
    {
        GeneratedHostPublishesStateOnlyAfterSuccessfulEnable();
        EntryAndHandlerShapesHaveFriendlyDiagnostics();
        MetadataBoundsAndBindingsHaveFriendlyDiagnostics();
        return Task.CompletedTask;
    }

    private static void GeneratedHostPublishesStateOnlyAfterSuccessfulEnable()
    {
        const string source = """
            .版本 2
            .扩展 CLR 1
            .扩展 LabAPI 1
            .LabAPI插件 主程序集
            .插件版本 “1.0.0”
            .配置项 速度, 小数型, 5, “speed”, “Speed”, “速度”, -10, 10
            .订阅事件 Fake.Events.Ready, 就绪

            .程序集 主程序集, , 公开
            .构造子程序 创建

            .子程序 插件启动

            .子程序 插件停止

            .子程序 就绪
            """;

        (BoundProgram bound, DiagnosticBag diagnostics) = Bind(source);
        Require(!diagnostics.HasErrors, Explain(diagnostics));
        IReadOnlyList<GeneratedSource> generated = new CSharpEmitter(bound).Emit();
        string host = generated.Single(static item => item.FileName == "EPLab.LabApi.g.cs").Text;
        string program = generated.Single(static item => item.FileName == "EPLab.Program.g.cs").Text;

        Require(!host.Contains("Entry { get; } = new", StringComparison.Ordinal),
            "the plugin entry was still constructed eagerly before config publication");
        Require(program.Contains("internal 主程序集()", StringComparison.Ordinal),
            "a default-private explicit parameterless entry constructor was not promoted for the generated host");

        int enable = host.IndexOf("public override void Enable()", StringComparison.Ordinal);
        int rollbackTry = host.IndexOf("try", enable, StringComparison.Ordinal);
        int validate = host.IndexOf("Config.速度 =", enable, StringComparison.Ordinal);
        int publishConfig = host.IndexOf("global::__EplabHost.Config = Config;", enable, StringComparison.Ordinal);
        int createEntry = host.IndexOf("entry = new 主程序集();", enable, StringComparison.Ordinal);
        int lifecycle = host.IndexOf("Entry.插件启动();", enable, StringComparison.Ordinal);
        int publishInstance = host.IndexOf("Instance = this;", enable, StringComparison.Ordinal);
        int catchBlock = host.IndexOf("catch", enable, StringComparison.Ordinal);
        int failedStop = host.IndexOf("failedEntry.插件停止();", catchBlock, StringComparison.Ordinal);
        int clearConfig = host.IndexOf("global::__EplabHost.Config = null;", catchBlock, StringComparison.Ordinal);
        int rethrow = host.IndexOf("throw;", clearConfig, StringComparison.Ordinal);
        Require(enable >= 0
            && rollbackTry > enable
            && validate > rollbackTry
            && publishConfig > validate
            && createEntry > publishConfig
            && lifecycle > createEntry
            && publishInstance > lifecycle
            && catchBlock > publishInstance
            && failedStop > catchBlock
            && clearConfig > failedStop
            && rethrow > clearConfig,
            "Enable does not validate config, publish it for entry construction, run lifecycle, and publish Instance within a rollback boundary");
        Require(host.Contains("entry = null;", StringComparison.Ordinal)
            && host.Contains("finally", StringComparison.Ordinal),
            "entry/config rollback cleanup is missing");
        Require(host.Split("global::LabApi.Loader.CommandLoader.UnregisterCommands(this);", StringSplitOptions.None).Length - 1 == 2,
            "failed Enable and normal Disable do not both release LabAPI command registrations");
    }

    private static void EntryAndHandlerShapesHaveFriendlyDiagnostics()
    {
        const string abstractEntry = """
            .版本 2
            .扩展 LabAPI 1
            .LabAPI插件 主程序集
            .程序集 主程序集, , 公开 抽象
            """;
        RequireDiagnostic(abstractEntry, "LAB3114");

        const string parameterizedEntry = """
            .版本 2
            .扩展 LabAPI 1
            .LabAPI插件 主程序集
            .程序集 主程序集, , 公开
            .构造子程序 创建, , 公开
            .参数 值, 整数型
            """;
        RequireDiagnostic(parameterizedEntry, "LAB3114");

        const string badHandlers = """
            .版本 2
            .扩展 LabAPI 1
            .LabAPI插件 主程序集
            .RA命令 “test”, “”, “test”, 执行
            .程序集 主程序集, , 公开
            .子程序 插件启动, , 公开, 异步

            .子程序 执行, 逻辑型, 公开, 异步
            .参数 上下文, CommandContext, 参考
            """;
        (_, DiagnosticBag handlerDiagnostics) = Bind(badHandlers);
        RequireIds(handlerDiagnostics, "LAB3115", "LAB3116", "LAB3117");

        const string nullableShapes = """
            .版本 2
            .扩展 LabAPI 1
            .LabAPI插件 主程序集
            .RA命令 “test”, “”, “test”, 执行
            .程序集 主程序集, , 公开
            .子程序 插件启动, 无返回值?, 公开

            .子程序 执行, 逻辑型?, 公开
            .参数 上下文, CommandContext
            """;
        (_, DiagnosticBag nullableDiagnostics) = Bind(nullableShapes);
        RequireIds(nullableDiagnostics, "LAB3108", "LAB3113");
    }

    private static void MetadataBoundsAndBindingsHaveFriendlyDiagnostics()
    {
        const string badMetadata = """
            .版本 2
            .扩展 LabAPI 1
            .LabAPI插件 主程序集
            .插件版本 “banana”
            .所需API版本 “1.-2”
            .配置项 速度, 小数型, 1, “speed”, “Speed”, “速度”, 不是数字, 10
            .程序集 主程序集, , 公开
            """;
        (_, DiagnosticBag metadataDiagnostics) = Bind(badMetadata);
        RequireIds(metadataDiagnostics, "LAB3118", "LAB3119", "LAB3120");

        const string conflicts = """
            .版本 2
            .扩展 LabAPI 1
            .LabAPI插件 主程序集
            .订阅事件 Fake.Events.Ready, 处理
            .订阅事件 fake.events.ready, 处理
            .订阅事件 , 处理
            .RA命令 “main”, “alias”, “one”, 命令甲
            .RA命令 “ALIAS”, “”, “two”, 命令乙
            .RA命令 “”, “”, “empty”,
            .玩家命令 “alias”, “”, “different command kind is allowed”, 玩家命令

            .程序集 主程序集, , 公开
            .子程序 处理

            .子程序 命令甲, 逻辑型
            .参数 上下文, CommandContext

            .子程序 命令乙, 逻辑型
            .参数 上下文, CommandContext

            .子程序 玩家命令, 逻辑型
            .参数 上下文, CommandContext
            """;
        (_, DiagnosticBag conflictDiagnostics) = Bind(conflicts);
        RequireIds(conflictDiagnostics, "LAB3121", "LAB3122", "LAB3123", "LAB3124");
    }

    private static (BoundProgram Bound, DiagnosticBag Diagnostics) Bind(string source)
    {
        DiagnosticBag diagnostics = new();
        CompilationUnitSyntax unit = new SourceParser("生命周期验证.易", source, diagnostics).Parse();
        EPLabProject project = new()
        {
            Name = "LifecycleSafetyFixture",
            RootNamespace = "LifecycleSafetyFixture",
            Target = "labapi-net48",
        };
        BoundProgram bound = new Binder(project, new[] { unit }, diagnostics).Bind();
        return (bound, diagnostics);
    }

    private static void RequireDiagnostic(string source, string id)
    {
        (_, DiagnosticBag diagnostics) = Bind(source);
        RequireIds(diagnostics, id);
    }

    private static void RequireIds(DiagnosticBag diagnostics, params string[] ids)
    {
        foreach (string id in ids)
            Require(diagnostics.Any(item => item.Id == id), $"expected {id}; got {Explain(diagnostics)}");
    }

    private static string Explain(DiagnosticBag diagnostics)
        => string.Join(Environment.NewLine, diagnostics.Select(static item => item.Format(false)));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
