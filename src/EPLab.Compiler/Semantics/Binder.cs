using EPLab.Compiler.Syntax;

namespace EPLab.Compiler.Semantics;

public sealed class Binder
{
    private readonly EPLabProject project;
    private readonly IReadOnlyList<CompilationUnitSyntax> units;
    private readonly DiagnosticBag diagnostics;
    private readonly Dictionary<string, TypeSyntax> knownTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ClassDeclarationSyntax> classes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MethodDeclarationSyntax> methods = new(StringComparer.OrdinalIgnoreCase);

    public Binder(EPLabProject project, IReadOnlyList<CompilationUnitSyntax> units, DiagnosticBag diagnostics)
    {
        this.project = project;
        this.units = units;
        this.diagnostics = diagnostics;
        RegisterBuiltInTypes();
    }

    public BoundProgram Bind()
    {
        ValidateExtensions();
        List<DeclarationSyntax> declarations = units.SelectMany(static unit => unit.Declarations).ToList();
        foreach (DeclarationSyntax declaration in declarations)
            Declare(declaration);

        PluginSyntax? plugin = SingleOrFirst(units.Where(static unit => unit.Plugin is not null).Select(static unit => unit.Plugin!), "LAB3101", "plugin declaration");
        List<ConfigItemSyntax> configItems = units.SelectMany(static unit => unit.ConfigItems).ToList();
        List<EventBindingSyntax> eventBindings = units.SelectMany(static unit => unit.EventBindings).ToList();
        List<CommandBindingSyntax> commandBindings = units.SelectMany(static unit => unit.CommandBindings).ToList();

        bool labApiEnabled = HasExtension("LabAPI");
        if (project.Target.Equals("labapi-net48", StringComparison.OrdinalIgnoreCase) && plugin is null)
        {
            diagnostics.Add(new Diagnostic(
                "LAB3109",
                DiagnosticSeverity.Error,
                units.FirstOrDefault()?.Span ?? new SourceSpan("project.eplabproj", 1),
                "A labapi-net48 project needs one '.LabAPI插件' declaration.",
                "labapi-net48 项目需要一条“.LabAPI插件”声明。"));
        }
        else if (plugin is not null && !project.Target.Equals("labapi-net48", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(new Diagnostic(
                "LAB3110",
                DiagnosticSeverity.Error,
                plugin.Span,
                "A '.LabAPI插件' declaration requires the labapi-net48 project target.",
                "使用“.LabAPI插件”时，项目目标必须是 labapi-net48。"));
        }
        if ((plugin is not null || configItems.Count > 0 || eventBindings.Count > 0 || commandBindings.Count > 0) && !labApiEnabled)
        {
            SourceSpan span = plugin?.Span
                ?? configItems.FirstOrDefault()?.Span
                ?? eventBindings.FirstOrDefault()?.Span
                ?? commandBindings.First().Span;
            diagnostics.Add(new Diagnostic(
                "LAB3102",
                DiagnosticSeverity.Error,
                span,
                "LabAPI features require '.扩展 LabAPI 1'.",
                "使用 LabAPI 功能前需要写“.扩展 LabAPI 1”。"));
        }

        ValidateDeclarations(declarations);
        ValidateBindings(plugin, eventBindings, commandBindings);
        ValidateConfig(configItems);

        return new BoundProgram(
            project,
            units,
            units.SelectMany(static unit => unit.NamespaceImports).Distinct(StringComparer.Ordinal).ToArray(),
            units.SelectMany(static unit => unit.AssemblyImports).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            plugin,
            configItems,
            eventBindings,
            commandBindings,
            declarations,
            classes,
            knownTypes);
    }

    private void RegisterBuiltInTypes()
    {
        foreach (string name in BuiltInTypeNames)
            knownTypes[name] = new TypeSyntax(name, false, false, new SourceSpan("<built-in>", 1));
    }

    private void ValidateExtensions()
    {
        foreach (CompilationUnitSyntax unit in units)
        {
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            foreach (ExtensionSyntax extension in unit.Extensions)
            {
                if (!KnownExtensions.Contains(extension.Name))
                {
                    diagnostics.Add(new Diagnostic(
                        "EPL1301",
                        DiagnosticSeverity.Error,
                        extension.Span,
                        $"Unknown extension '{extension.Name}'.",
                        $"未知扩展“{extension.Name}”。",
                        "Available extensions are CLR and LabAPI.",
                        "可用扩展为 CLR 和 LabAPI。"));
                }
                else if (extension.Version != 1)
                {
                    diagnostics.Add(new Diagnostic(
                        "EPL1302",
                        DiagnosticSeverity.Error,
                        extension.Span,
                        $"Extension '{extension.Name}' version {extension.Version} is not supported.",
                        $"不支持扩展“{extension.Name}”的第 {extension.Version} 版。"));
                }

                if (!seen.Add(extension.Name))
                {
                    diagnostics.Add(new Diagnostic(
                        "EPL1303",
                        DiagnosticSeverity.Warning,
                        extension.Span,
                        $"Extension '{extension.Name}' is already enabled in this file.",
                        $"这个文件已经启用了扩展“{extension.Name}”。"));
                }
            }
        }
    }

    private void Declare(DeclarationSyntax declaration)
    {
        switch (declaration)
        {
            case ClassDeclarationSyntax @class:
                if (!classes.TryAdd(@class.Name, @class))
                    Duplicate("EPL1304", @class.Name, @class.Span, "class/assembly", "程序集");
                else if (knownTypes.ContainsKey(@class.Name))
                {
                    classes.Remove(@class.Name);
                    Duplicate("EPL1305", @class.Name, @class.Span, "type", "类型");
                }
                else
                    knownTypes[@class.Name] = new TypeSyntax(@class.Name, false, false, @class.Span);
                foreach (MethodDeclarationSyntax method in @class.Methods)
                {
                    string qualified = $"{@class.Name}.{method.Name}";
                    methods.TryAdd(qualified, method);
                    methods.TryAdd(method.Name, method);
                }
                break;

            case StructDeclarationSyntax structure:
                if (knownTypes.ContainsKey(structure.Name))
                    Duplicate("EPL1305", structure.Name, structure.Span, "type", "类型");
                else
                    knownTypes[structure.Name] = new TypeSyntax(structure.Name, false, false, structure.Span);
                break;

            case EnumDeclarationSyntax enumeration:
                if (knownTypes.ContainsKey(enumeration.Name))
                    Duplicate("EPL1305", enumeration.Name, enumeration.Span, "type", "类型");
                else
                    knownTypes[enumeration.Name] = new TypeSyntax(enumeration.Name, false, false, enumeration.Span);
                break;
        }
    }

    private void ValidateDeclarations(IEnumerable<DeclarationSyntax> declarations)
    {
        DeclarationSyntax[] declarationArray = declarations.ToArray();
        ValidateUnique(
            declarationArray.OfType<FieldDeclarationSyntax>().Select(static field => (field.Name, field.Span))
                .Concat(declarationArray.OfType<ConstantDeclarationSyntax>().Select(static constant => (constant.Name, constant.Span)))
                .Concat(declarationArray.OfType<DllDeclarationSyntax>().Select(static dll => (dll.Name, dll.Span))),
            "global symbol",
            "全局符号");

        foreach (DeclarationSyntax declaration in declarationArray)
        {
            switch (declaration)
            {
                case ClassDeclarationSyntax @class:
                    ValidateTopLevelDeclaration(@class.Name, @class.Visibility, @class.Modifiers, @class.Span);
                    ValidateType(@class.BaseType);
                    ValidateUnique(@class.Fields.Select(static field => (field.Name, field.Span)), "field", "程序集变量");
                    ValidateUnique(@class.Methods.Select(static method => (method.Name, method.Span)), "method", "子程序", allowOverloads: true);
                    ValidateMemberCollisions(@class);
                    ValidateMethodSignatures(@class.Methods);
                    foreach (FieldDeclarationSyntax field in @class.Fields)
                        ValidateField(field);
                    foreach (MethodDeclarationSyntax method in @class.Methods)
                        ValidateMethod(method, @class.Fields);
                    break;

                case StructDeclarationSyntax structure:
                    ValidateTopLevelDeclaration(structure.Name, structure.Visibility, DeclarationModifiers.None, structure.Span);
                    ValidateUnique(structure.Members.Select(static member => (member.Name, member.Span)), "member", "成员");
                    foreach (FieldDeclarationSyntax member in structure.Members)
                        ValidateField(member);
                    break;

                case EnumDeclarationSyntax enumeration:
                    ValidateTopLevelDeclaration(enumeration.Name, enumeration.Visibility, DeclarationModifiers.None, enumeration.Span);
                    ValidateUnique(enumeration.Members.Select(static member => (member.Name, member.Span)), "enum member", "枚举值");
                    break;

                case FieldDeclarationSyntax field:
                    ValidateField(field);
                    break;

                case DllDeclarationSyntax dll:
                    ValidateType(dll.ReturnType);
                    foreach (ParameterSyntax parameter in dll.Parameters)
                        ValidateType(parameter.Type);
                    if (string.IsNullOrWhiteSpace(dll.LibraryName))
                    {
                        diagnostics.Add(new Diagnostic(
                            "CLR2101",
                            DiagnosticSeverity.Error,
                            dll.Span,
                            "A DLL command needs a library name.",
                            "DLL 命令需要填写库文件名。"));
                    }
                    break;
            }
        }
    }

    private void ValidateMethod(MethodDeclarationSyntax method, IReadOnlyList<FieldDeclarationSyntax> fields)
    {
        ValidateType(method.ReturnType);
        if (method.Modifiers.HasFlag(DeclarationModifiers.Constructor))
        {
            DeclarationModifiers unsupported = method.Modifiers & ~DeclarationModifiers.Constructor;
            if (unsupported != DeclarationModifiers.None)
            {
                diagnostics.Add(new Diagnostic(
                    "CLR2106",
                    DiagnosticSeverity.Error,
                    method.Span,
                    $"Constructor '{method.Name}' uses unsupported modifiers: {unsupported}.",
                    $"构造子程序“{method.Name}”使用了不支持的修饰词：{unsupported}。",
                    "Constructors may specify visibility, but not static, async, abstract, or other method modifiers.",
                "构造子程序可以填写可见性，但不能填写静态、异步、抽象或其他子程序修饰词。"));
            }
        }
        else if (method.Modifiers.HasFlag(DeclarationModifiers.Abstract))
        {
            diagnostics.Add(new Diagnostic(
                "CLR2107",
                DiagnosticSeverity.Error,
                method.Span,
                $"Abstract method '{method.Name}' is not supported yet.",
                $"暂不支持抽象子程序“{method.Name}”。",
                "Use a virtual method with a body, or implement the method in a CLR bridge.",
                "请改用带代码体的虚子程序，或在 CLR 桥接中实现该子程序。"));
        }
        ValidateUnique(
            method.Parameters.Select(static parameter => (parameter.Name, parameter.Span))
                .Concat(method.Locals.Select(static local => (local.Name, local.Span))),
            "local name",
            "参数或局部变量");
        foreach (ParameterSyntax parameter in method.Parameters)
            ValidateType(parameter.Type);
        foreach (LocalVariableSyntax local in method.Locals)
            ValidateType(local.Type);
        ValidateParameters(method);

        HashSet<string> eArrays = method.Parameters
            .Where(static parameter => !parameter.Modifiers.HasFlag(ParameterModifiers.Params)
                && (parameter.Type.IsArray || parameter.Modifiers.HasFlag(ParameterModifiers.Array)))
            .Select(static parameter => parameter.Name)
            .Concat(method.Locals.Where(static local => local.ArrayShape is not null || local.Type.IsArray).Select(static local => local.Name))
            .Concat(fields.Where(static field => field.ArrayShape is not null || field.Type.IsArray).Select(static field => field.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (StatementSyntax statement in method.Statements)
            CheckArrayIndices(statement, eArrays);
    }

    private void ValidateMemberCollisions(ClassDeclarationSyntax @class)
    {
        HashSet<string> fieldNames = @class.Fields.Select(static field => field.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (MethodDeclarationSyntax method in @class.Methods)
        {
            if (fieldNames.Contains(method.Name))
                Duplicate("EPL1309", method.Name, method.Span, "class member", "程序集成员");
        }
    }

    private void ValidateMethodSignatures(IReadOnlyList<MethodDeclarationSyntax> methodsToCheck)
    {
        HashSet<string> signatures = new(StringComparer.OrdinalIgnoreCase);
        foreach (MethodDeclarationSyntax method in methodsToCheck)
        {
            string methodName = IsConstructor(method) ? ".ctor" : method.Name;
            string parameters = string.Join(",", method.Parameters.Select(ParameterSignature));
            if (!signatures.Add(methodName + "(" + parameters + ")"))
            {
                diagnostics.Add(new Diagnostic(
                    "EPL1312",
                    DiagnosticSeverity.Error,
                    method.Span,
                    $"Method '{method.Name}' has the same parameter signature as an earlier method.",
                    $"子程序“{method.Name}”的参数签名与前面的子程序相同。"));
            }
        }
    }

    private static string ParameterSignature(ParameterSyntax parameter)
    {
        string modifier = parameter.Modifiers.HasFlag(ParameterModifiers.Out)
            || parameter.Modifiers.HasFlag(ParameterModifiers.ByReference) ? "ref:" : string.Empty;
        if (parameter.Modifiers.HasFlag(ParameterModifiers.Params))
            return modifier + SignatureTypeName(parameter.Type.Text) + "[]";
        if (parameter.Modifiers.HasFlag(ParameterModifiers.Array) || parameter.Type.IsArray)
            return modifier + "EArray<" + SignatureTypeName(parameter.Type.Text) + ">";
        return modifier + SignatureTypeName(parameter.Type.Text) + (parameter.Type.IsNullable ? "?" : string.Empty);
    }

    private static string SignatureTypeName(string value)
        => RemoveGenericArguments(value).Trim().ToUpperInvariant() switch
        {
            "字节型" or "BYTE" => "BYTE",
            "短整数型" or "SHORT" or "INT16" => "INT16",
            "整数型" or "INT" or "INT32" => "INT32",
            "无符号整数型" or "UINT" or "UINT32" => "UINT32",
            "长整数型" or "LONG" or "INT64" => "INT64",
            "小数型" or "FLOAT" or "SINGLE" => "SINGLE",
            "双精度小数型" or "DOUBLE" => "DOUBLE",
            "逻辑型" or "BOOL" or "BOOLEAN" => "BOOLEAN",
            "文本型" or "STRING" => "STRING",
            "通用型" or "OBJECT" => "OBJECT",
            "日期时间型" or "DATETIME" => "DATETIME",
            _ => value.Trim().Replace('《', '<').Replace('》', '>'),
        };

    private void ValidateParameters(MethodDeclarationSyntax method)
    {
        bool sawOptional = false;
        for (int index = 0; index < method.Parameters.Count; index++)
        {
            ParameterSyntax parameter = method.Parameters[index];
            ParameterModifiers modifiers = parameter.Modifiers;
            bool optional = modifiers.HasFlag(ParameterModifiers.Optional) || parameter.DefaultValue is not null;
            bool byReference = modifiers.HasFlag(ParameterModifiers.ByReference);
            bool output = modifiers.HasFlag(ParameterModifiers.Out);
            bool parameterArray = modifiers.HasFlag(ParameterModifiers.Params);

            if (byReference && output
                || optional && (byReference || output || parameterArray)
                || parameterArray && index != method.Parameters.Count - 1
                || parameterArray && parameter.DefaultValue is not null)
            {
                diagnostics.Add(new Diagnostic(
                    "CLR2103",
                    DiagnosticSeverity.Error,
                    parameter.Span,
                    $"Parameter modifiers for '{parameter.Name}' cannot be combined in this way.",
                    $"参数“{parameter.Name}”的修饰词不能这样组合。",
                    "ref/out cannot be optional; params must be last and cannot have a default.",
                    "参考/输出参数不能可空；参数数组必须放在最后且不能有默认值。"));
            }

            if (modifiers.HasFlag(ParameterModifiers.Optional) && parameter.DefaultValue is not null)
            {
                diagnostics.Add(new Diagnostic(
                    "EPL1311",
                    DiagnosticSeverity.Error,
                    parameter.Span,
                    $"Optional parameter '{parameter.Name}' uses the missing-value sentinel and cannot also declare a default.",
                    $"可空参数“{parameter.Name}”使用“缺省”标记，不能同时填写默认值。"));
            }

            if (sawOptional && !optional && !parameterArray)
            {
                diagnostics.Add(new Diagnostic(
                    "CLR2104",
                    DiagnosticSeverity.Error,
                    parameter.Span,
                    $"Required parameter '{parameter.Name}' cannot follow an optional parameter.",
                    $"必填参数“{parameter.Name}”不能放在可选参数后面。"));
            }
            sawOptional |= optional;
        }
    }

    private void ValidateTopLevelDeclaration(
        string name,
        Visibility visibility,
        DeclarationModifiers modifiers,
        SourceSpan span)
    {
        if (visibility is Visibility.Private or Visibility.Protected)
        {
            diagnostics.Add(new Diagnostic(
                "EPL1310",
                DiagnosticSeverity.Error,
                span,
                $"Top-level type '{name}' must be internal or public.",
                $"最外层类型“{name}”必须是内部或公开。"));
        }
        if (modifiers.HasFlag(DeclarationModifiers.Partial))
        {
            diagnostics.Add(new Diagnostic(
                "CLR2102",
                DiagnosticSeverity.Error,
                span,
                "Partial classes are not supported in EPLab 1 yet.",
                "EPLab 1 暂不支持分部类。"));
        }
        DeclarationModifiers unsupported = modifiers
            & ~(DeclarationModifiers.Abstract | DeclarationModifiers.Sealed | DeclarationModifiers.Partial);
        if (unsupported != DeclarationModifiers.None
            || modifiers.HasFlag(DeclarationModifiers.Abstract) && modifiers.HasFlag(DeclarationModifiers.Sealed))
        {
            diagnostics.Add(new Diagnostic(
                "CLR2105",
                DiagnosticSeverity.Error,
                span,
                $"Top-level type '{name}' uses unsupported modifiers: {unsupported}.",
                $"最外层类型“{name}”使用了不支持的修饰词：{unsupported}。",
                "Assemblies/classes may be abstract or sealed. Static classes are not supported yet.",
                "程序集/类可以是抽象或密封；暂不支持静态类。"));
        }
    }

    private void ValidateField(FieldDeclarationSyntax field)
    {
        ValidateType(field.Type);
        if (field.ArrayShape is { Dynamic: false, Dimensions.Count: 0 })
        {
            diagnostics.Add(new Diagnostic(
                "EPL1306",
                DiagnosticSeverity.Error,
                field.Span,
                $"Array '{field.Name}' needs at least one dimension.",
                $"数组“{field.Name}”至少需要一个维数。"));
        }
    }

    private void ValidateType(TypeSyntax? type)
    {
        if (type is null || knownTypes.ContainsKey(RemoveGenericArguments(type.Text)))
            return;

        if (!HasExtension("CLR"))
        {
            diagnostics.Add(new Diagnostic(
                "EPL1307",
                DiagnosticSeverity.Error,
                type.Span,
                $"Unknown type '{type.Text}'. CLR types require '.扩展 CLR 1'.",
                $"未知类型“{type.Text}”。使用 CLR 类型前需要写“.扩展 CLR 1”。"));
        }
    }

    private void ValidateBindings(
        PluginSyntax? plugin,
        IReadOnlyList<EventBindingSyntax> eventBindings,
        IReadOnlyList<CommandBindingSyntax> commandBindings)
    {
        ValidateBindingConflicts(eventBindings, commandBindings);

        ClassDeclarationSyntax? entryClass = null;
        if (plugin is not null)
            ValidatePluginMetadata(plugin);
        if (plugin is not null && !classes.TryGetValue(plugin.ClassName, out entryClass))
        {
            diagnostics.Add(new Diagnostic(
                "LAB3103",
                DiagnosticSeverity.Error,
                plugin.Span,
                $"Plugin assembly '{plugin.ClassName}' was not found.",
                $"找不到插件程序集“{plugin.ClassName}”。"));
        }
        else if (plugin is not null && entryClass is not null)
        {
            ValidatePluginEntry(plugin, entryClass);
        }

        foreach (EventBindingSyntax binding in eventBindings)
        {
            if (string.IsNullOrWhiteSpace(binding.EventPath) || string.IsNullOrWhiteSpace(binding.HandlerName))
                continue;

            MethodDeclarationSyntax[] handlers = entryClass?.Methods
                .Where(method => method.Name.Equals(binding.HandlerName, StringComparison.OrdinalIgnoreCase)).ToArray()
                ?? Array.Empty<MethodDeclarationSyntax>();
            if (handlers.Length == 0)
            {
                diagnostics.Add(new Diagnostic(
                    "LAB3104",
                    DiagnosticSeverity.Error,
                    binding.Span,
                    $"Event handler '{binding.HandlerName}' was not found.",
                    $"找不到事件处理子程序“{binding.HandlerName}”。"));
            }
            else if (handlers.Length != 1 || handlers[0].Modifiers.HasFlag(DeclarationModifiers.Static))
            {
                diagnostics.Add(new Diagnostic(
                    "LAB3111",
                    DiagnosticSeverity.Error,
                    binding.Span,
                    $"Event handler '{binding.HandlerName}' must name exactly one instance method on the plugin assembly.",
                    $"事件处理子程序“{binding.HandlerName}”必须是插件程序集里唯一的实例子程序。"));
            }
        }

        foreach (CommandBindingSyntax binding in commandBindings)
        {
            if (string.IsNullOrWhiteSpace(binding.HandlerName))
                continue;

            MethodDeclarationSyntax[] handlers = entryClass?.Methods
                .Where(method => method.Name.Equals(binding.HandlerName, StringComparison.OrdinalIgnoreCase)).ToArray()
                ?? Array.Empty<MethodDeclarationSyntax>();
            MethodDeclarationSyntax? handler = handlers.Length == 1 ? handlers[0] : null;
            if (handlers.Length == 0)
            {
                diagnostics.Add(new Diagnostic(
                    "LAB3105",
                    DiagnosticSeverity.Error,
                    binding.Span,
                    $"Command handler '{binding.HandlerName}' was not found.",
                    $"找不到命令处理子程序“{binding.HandlerName}”。"));
            }
            else if (handler is null || handler.Modifiers.HasFlag(DeclarationModifiers.Static))
            {
                diagnostics.Add(new Diagnostic(
                    "LAB3112",
                    DiagnosticSeverity.Error,
                    binding.Span,
                    $"Command handler '{binding.HandlerName}' must name exactly one instance method on the plugin assembly.",
                    $"命令处理子程序“{binding.HandlerName}”必须是插件程序集里唯一的实例子程序。"));
            }
            else
            {
                bool contextParameter = handler.Parameters.Count == 1
                    && CommandContextTypeNames.Contains(handler.Parameters[0].Type.Text)
                    && !handler.Parameters[0].Type.IsArray
                    && !handler.Parameters[0].Type.IsNullable;
                bool supportedReturn = VoidTypeNames.Contains(handler.ReturnType.Text)
                    || BooleanTypeNames.Contains(handler.ReturnType.Text);
                supportedReturn = supportedReturn
                    && !handler.ReturnType.IsArray
                    && !handler.ReturnType.IsNullable;
                if (!contextParameter || !supportedReturn)
                {
                    diagnostics.Add(new Diagnostic(
                        "LAB3108",
                        DiagnosticSeverity.Error,
                        handler.Span,
                        $"Command handler '{handler.Name}' must take one CommandContext/命令上下文 parameter and return void or bool.",
                        $"命令处理子程序“{handler.Name}”必须有一个“命令上下文”参数，并返回空或逻辑型。"));
                }
                else
                {
                    ParameterSyntax parameter = handler.Parameters[0];
                    bool plainParameter = parameter.Modifiers == ParameterModifiers.None
                        && parameter.DefaultValue is null
                        && !parameter.Type.IsNullable;
                    if (!plainParameter)
                    {
                        diagnostics.Add(new Diagnostic(
                            "LAB3117",
                            DiagnosticSeverity.Error,
                            parameter.Span,
                            $"Command handler '{handler.Name}' needs a plain CommandContext parameter without ref, out, optional, array, params, nullable, or a default value.",
                            $"命令处理子程序“{handler.Name}”的“命令上下文”参数必须是普通参数，不能有参考、输出、可空、数组、参数数组、可空类型或默认值。"));
                    }
                }

                if (handler.Modifiers.HasFlag(DeclarationModifiers.Async))
                {
                    diagnostics.Add(new Diagnostic(
                        "LAB3116",
                        DiagnosticSeverity.Error,
                        handler.Span,
                        $"Command handler '{handler.Name}' cannot be async because LabAPI expects its result immediately.",
                        $"命令处理子程序“{handler.Name}”不能是异步子程序，因为 LabAPI 需要立即得到结果。"));
                }
            }
        }

        if (entryClass is not null)
        {
            ValidateLifecycleGroup(entryClass, new[] { "插件启动", "_插件启动", "Enable" }, "enable");
            ValidateLifecycleGroup(entryClass, new[] { "插件停止", "_插件停止", "Disable" }, "disable");
        }
    }

    private void ValidateLifecycleGroup(ClassDeclarationSyntax entryClass, IReadOnlyList<string> names, string kind)
    {
        MethodDeclarationSyntax[] lifecycle = entryClass.Methods
            .Where(method => names.Any(name => method.Name.Equals(name, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (lifecycle.Length == 0)
            return;
        foreach (MethodDeclarationSyntax asyncMethod in lifecycle.Where(static method => method.Modifiers.HasFlag(DeclarationModifiers.Async)))
        {
            diagnostics.Add(new Diagnostic(
                "LAB3115",
                DiagnosticSeverity.Error,
                asyncMethod.Span,
                $"Plugin {kind} lifecycle cannot be async because LabAPI cannot await it.",
                $"插件{(kind == "enable" ? "启动" : "停止")}生命周期不能是异步子程序，因为 LabAPI 无法等待它完成。"));
        }
        MethodDeclarationSyntax method = lifecycle[0];
        if (lifecycle.Length != 1
            || method.Modifiers.HasFlag(DeclarationModifiers.Static)
            || method.Parameters.Count != 0
            || !VoidTypeNames.Contains(method.ReturnType.Text)
            || method.ReturnType.IsArray
            || method.ReturnType.IsNullable)
        {
            diagnostics.Add(new Diagnostic(
                "LAB3113",
                DiagnosticSeverity.Error,
                method.Span,
                $"Plugin {kind} lifecycle must be one parameterless instance method returning void.",
                $"插件{(kind == "enable" ? "启动" : "停止")}生命周期必须是唯一、无参数、无返回值的实例子程序。"));
        }
    }

    private void ValidatePluginMetadata(PluginSyntax plugin)
    {
        if (!Version.TryParse(plugin.Version, out _))
        {
            diagnostics.Add(new Diagnostic(
                "LAB3118",
                DiagnosticSeverity.Error,
                plugin.Span,
                $"Plugin version '{plugin.Version}' is not a valid CLR version.",
                $"插件版本“{plugin.Version}”不是有效的 CLR 版本号。",
                "Use two to four non-negative number parts, for example 1.0 or 1.2.3.",
                "请使用 2 到 4 段非负数字，例如 1.0 或 1.2.3。"));
        }

        if (plugin.RequiredApiVersion is not null && !Version.TryParse(plugin.RequiredApiVersion, out _))
        {
            diagnostics.Add(new Diagnostic(
                "LAB3119",
                DiagnosticSeverity.Error,
                plugin.Span,
                $"Required API version '{plugin.RequiredApiVersion}' is not a valid CLR version.",
                $"所需 API 版本“{plugin.RequiredApiVersion}”不是有效的 CLR 版本号。",
                "Use two to four non-negative number parts, for example 1.0 or 1.2.3.",
                "请使用 2 到 4 段非负数字，例如 1.0 或 1.2.3。"));
        }
    }

    private void ValidatePluginEntry(PluginSyntax plugin, ClassDeclarationSyntax entryClass)
    {
        if (entryClass.Modifiers.HasFlag(DeclarationModifiers.Abstract)
            || entryClass.Modifiers.HasFlag(DeclarationModifiers.Static))
        {
            diagnostics.Add(new Diagnostic(
                "LAB3114",
                DiagnosticSeverity.Error,
                entryClass.Span,
                $"Plugin assembly '{entryClass.Name}' must be a concrete, instantiable class.",
                $"插件程序集“{entryClass.Name}”必须是可以创建对象的非抽象类。"));
        }

        MethodDeclarationSyntax[] constructors = entryClass.Methods.Where(IsConstructor).ToArray();
        if (constructors.Length == 0)
            return;

        MethodDeclarationSyntax[] parameterless = constructors.Where(static method => method.Parameters.Count == 0).ToArray();
        if (parameterless.Length != 1)
        {
            SourceSpan span = parameterless.Skip(1).FirstOrDefault()?.Span
                ?? constructors.FirstOrDefault()?.Span
                ?? plugin.Span;
            diagnostics.Add(new Diagnostic(
                "LAB3114",
                DiagnosticSeverity.Error,
                span,
                $"Plugin assembly '{entryClass.Name}' needs exactly one explicit parameterless constructor when constructors are declared.",
                $"插件程序集“{entryClass.Name}”声明了构造子程序时，必须恰好有一个无参数构造子程序。"));
        }

        foreach (MethodDeclarationSyntax constructor in constructors)
        {
            DeclarationModifiers unsupported = constructor.Modifiers & ~DeclarationModifiers.Constructor;
            bool voidReturn = VoidTypeNames.Contains(constructor.ReturnType.Text)
                && !constructor.ReturnType.IsArray
                && !constructor.ReturnType.IsNullable;
            if (unsupported == DeclarationModifiers.None && voidReturn)
                continue;

            diagnostics.Add(new Diagnostic(
                "LAB3114",
                DiagnosticSeverity.Error,
                constructor.Span,
                $"Constructor '{constructor.Name}' on plugin assembly '{entryClass.Name}' uses an unsupported modifier or return type.",
                $"插件程序集“{entryClass.Name}”的构造子程序“{constructor.Name}”使用了不支持的修饰词或返回类型。"));
        }
    }

    private void ValidateBindingConflicts(
        IReadOnlyList<EventBindingSyntax> eventBindings,
        IReadOnlyList<CommandBindingSyntax> commandBindings)
    {
        HashSet<string> events = new(StringComparer.OrdinalIgnoreCase);
        foreach (EventBindingSyntax binding in eventBindings)
        {
            if (string.IsNullOrWhiteSpace(binding.EventPath) || string.IsNullOrWhiteSpace(binding.HandlerName))
            {
                diagnostics.Add(new Diagnostic(
                    "LAB3121",
                    DiagnosticSeverity.Error,
                    binding.Span,
                    "An event binding needs a non-empty event path and handler name.",
                    "事件绑定需要非空的事件路径和处理子程序名称。"));
                continue;
            }

            string key = binding.EventPath + "\0" + binding.HandlerName;
            if (!events.Add(key))
            {
                diagnostics.Add(new Diagnostic(
                    "LAB3122",
                    DiagnosticSeverity.Error,
                    binding.Span,
                    $"Event '{binding.EventPath}' is already bound to handler '{binding.HandlerName}'.",
                    $"事件“{binding.EventPath}”已经绑定到处理子程序“{binding.HandlerName}”。"));
            }
        }

        foreach (IGrouping<CommandKind, CommandBindingSyntax> group in commandBindings.GroupBy(static binding => binding.Kind))
        {
            HashSet<string> claimedNames = new(StringComparer.OrdinalIgnoreCase);
            foreach (CommandBindingSyntax binding in group)
            {
                if (string.IsNullOrWhiteSpace(binding.Name) || string.IsNullOrWhiteSpace(binding.HandlerName))
                {
                    diagnostics.Add(new Diagnostic(
                        "LAB3123",
                        DiagnosticSeverity.Error,
                        binding.Span,
                        "A command binding needs a non-empty primary name and handler name.",
                        "命令绑定需要非空的主名称和处理子程序名称。"));
                }

                foreach (string name in new[] { binding.Name }.Concat(binding.Aliases).Where(static name => !string.IsNullOrWhiteSpace(name)))
                {
                    if (claimedNames.Add(name))
                        continue;
                    diagnostics.Add(new Diagnostic(
                        "LAB3124",
                        DiagnosticSeverity.Error,
                        binding.Span,
                        $"Command name or alias '{name}' is used more than once for {binding.Kind} commands.",
                        $"命令名或别名“{name}”在 {binding.Kind} 命令中重复使用了。"));
                }
            }
        }
    }

    private static bool IsConstructor(MethodDeclarationSyntax method)
        => method.Modifiers.HasFlag(DeclarationModifiers.Constructor)
            || method.Name.Equals("_初始化", StringComparison.OrdinalIgnoreCase);

    private void ValidateConfig(IReadOnlyList<ConfigItemSyntax> configItems)
    {
        ValidateUnique(configItems.Select(static item => (item.Name, item.Span)), "config item", "配置项");
        ValidateUnique(
            configItems.Where(static item => item.SerializedName is not null)
                .Select(static item => (item.SerializedName!, item.Span)),
            "serialized config name",
            "配置文件名称");

        foreach (ConfigItemSyntax item in configItems)
        {
            if (item.Minimum is null && item.Maximum is null)
                continue;
            if (!NumericTypeNames.Contains(RemoveGenericArguments(item.Type.Text)))
            {
                diagnostics.Add(new Diagnostic(
                    "LAB3106",
                    DiagnosticSeverity.Error,
                    item.Span,
                    $"Config range limits can only be used with a numeric type, not '{item.Type.Text}'.",
                    $"配置范围只能用于数值类型，不能用于“{item.Type.Text}”。"));
                continue;
            }

            bool minimumValid = item.Minimum is null || TryNumber(item.Minimum, out _);
            bool maximumValid = item.Maximum is null || TryNumber(item.Maximum, out _);
            if (!minimumValid || !maximumValid)
            {
                diagnostics.Add(new Diagnostic(
                    "LAB3120",
                    DiagnosticSeverity.Error,
                    item.Span,
                    $"Config range limits for '{item.Name}' must be finite numeric constants.",
                    $"配置项“{item.Name}”的范围限制必须是有限数值常量。",
                    "Use a number such as 0, 1.5, or -10.",
                    "请使用 0、1.5 或 -10 这样的数字。"));
                continue;
            }

            if (TryNumber(item.Minimum, out double minimum)
                && TryNumber(item.Maximum, out double maximum)
                && minimum > maximum)
            {
                diagnostics.Add(new Diagnostic(
                    "LAB3107",
                    DiagnosticSeverity.Error,
                    item.Span,
                    $"Config item '{item.Name}' has a minimum greater than its maximum.",
                    $"配置项“{item.Name}”的最小值大于最大值。"));
            }
        }
    }

    private static bool TryNumber(ExpressionSyntax? expression, out double value)
    {
        if (expression is UnaryExpressionSyntax unary && TryNumber(unary.Operand, out double operand))
        {
            if (unary.Operator is "+" or "＋")
            {
                value = operand;
                return double.IsFinite(value);
            }
            if (unary.Operator is "-" or "－")
            {
                value = -operand;
                return double.IsFinite(value);
            }
        }
        else if (expression is LiteralExpressionSyntax { Value: IConvertible convertible } literal
            && literal.Value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
        {
            try
            {
                value = convertible.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
                return double.IsFinite(value);
            }
            catch
            {
                // A backend diagnostic will describe non-numeric expressions.
            }
        }
        value = default;
        return false;
    }

    private void CheckArrayIndices(StatementSyntax statement, HashSet<string> eArrays)
    {
        foreach (ExpressionSyntax expression in DescendantExpressions(statement))
        {
            if (expression is not IndexExpressionSyntax { Target: NameExpressionSyntax target } index || !eArrays.Contains(target.Name))
                continue;

            foreach (ExpressionSyntax item in index.Indices)
            {
                if (item is LiteralExpressionSyntax { Value: long value } && value == 0)
                {
                    diagnostics.Add(new Diagnostic(
                        "EPL1308",
                        DiagnosticSeverity.Warning,
                        item.Span,
                        $"E array '{target.Name}' starts at 1, so index 0 will fail.",
                        $"易数组“{target.Name}”从 1 开始，因此下标 0 会失败。"));
                }
            }
        }
    }

    private static IEnumerable<ExpressionSyntax> DescendantExpressions(SyntaxNode node)
    {
        switch (node)
        {
            case ExpressionSyntax expression:
                yield return expression;
                foreach (SyntaxNode child in ExpressionChildren(expression))
                foreach (ExpressionSyntax descendant in DescendantExpressions(child))
                    yield return descendant;
                break;

            case ExpressionStatementSyntax expressionStatement:
                foreach (ExpressionSyntax item in DescendantExpressions(expressionStatement.Expression)) yield return item;
                break;
            case AssignmentStatementSyntax assignment:
                foreach (ExpressionSyntax item in DescendantExpressions(assignment.Target)) yield return item;
                foreach (ExpressionSyntax item in DescendantExpressions(assignment.Value)) yield return item;
                break;
            case IfStatementSyntax conditional:
                foreach (ExpressionSyntax item in DescendantExpressions(conditional.Condition)) yield return item;
                foreach (StatementSyntax child in conditional.ThenStatements.Concat(conditional.ElseStatements))
                foreach (ExpressionSyntax item in DescendantExpressions(child)) yield return item;
                break;
            case WhileStatementSyntax loop:
                foreach (ExpressionSyntax item in DescendantExpressions(loop.Condition)) yield return item;
                foreach (StatementSyntax child in loop.Statements)
                foreach (ExpressionSyntax item in DescendantExpressions(child)) yield return item;
                break;
            case CountLoopStatementSyntax count:
                foreach (ExpressionSyntax item in DescendantExpressions(count.Count)) yield return item;
                if (count.Counter is not null) foreach (ExpressionSyntax item in DescendantExpressions(count.Counter)) yield return item;
                foreach (StatementSyntax child in count.Statements)
                foreach (ExpressionSyntax item in DescendantExpressions(child)) yield return item;
                break;
            case RangeLoopStatementSyntax range:
                foreach (ExpressionSyntax item in DescendantExpressions(range.Start)) yield return item;
                foreach (ExpressionSyntax item in DescendantExpressions(range.End)) yield return item;
                if (range.Step is not null) foreach (ExpressionSyntax item in DescendantExpressions(range.Step)) yield return item;
                if (range.Variable is not null) foreach (ExpressionSyntax item in DescendantExpressions(range.Variable)) yield return item;
                foreach (StatementSyntax child in range.Statements)
                foreach (ExpressionSyntax item in DescendantExpressions(child)) yield return item;
                break;
            case ForEachStatementSyntax each:
                foreach (ExpressionSyntax item in DescendantExpressions(each.Collection)) yield return item;
                foreach (StatementSyntax child in each.Statements)
                foreach (ExpressionSyntax item in DescendantExpressions(child)) yield return item;
                break;
            case TryStatementSyntax attempt:
                foreach (StatementSyntax child in attempt.TryStatements
                    .Concat(attempt.Catches.SelectMany(static clause => clause.Statements))
                    .Concat(attempt.FinallyStatements))
                foreach (ExpressionSyntax item in DescendantExpressions(child)) yield return item;
                break;
            case TemporarySetStatementSyntax temporary:
                foreach (ExpressionSyntax item in DescendantExpressions(temporary.Target)) yield return item;
                foreach (ExpressionSyntax item in DescendantExpressions(temporary.Value)) yield return item;
                foreach (StatementSyntax child in temporary.Statements)
                foreach (ExpressionSyntax item in DescendantExpressions(child)) yield return item;
                break;
            case LockStatementSyntax locked:
                foreach (ExpressionSyntax item in DescendantExpressions(locked.Target)) yield return item;
                foreach (StatementSyntax child in locked.Statements)
                foreach (ExpressionSyntax item in DescendantExpressions(child)) yield return item;
                break;
            case UsingStatementSyntax resource:
                foreach (ExpressionSyntax item in DescendantExpressions(resource.Value)) yield return item;
                foreach (StatementSyntax child in resource.Statements)
                foreach (ExpressionSyntax item in DescendantExpressions(child)) yield return item;
                break;
        }
    }

    private static IEnumerable<SyntaxNode> ExpressionChildren(ExpressionSyntax expression) => expression switch
    {
        UnaryExpressionSyntax unary => new SyntaxNode[] { unary.Operand },
        BinaryExpressionSyntax binary => new SyntaxNode[] { binary.Left, binary.Right },
        InterpolatedStringExpressionSyntax interpolated => interpolated.Parts
            .OfType<InterpolationSyntax>()
            .Select(static part => (SyntaxNode)part.Expression),
        MemberAccessExpressionSyntax member => new SyntaxNode[] { member.Target },
        CallExpressionSyntax call => new SyntaxNode[] { call.Target }.Concat(call.Arguments.Select(static argument => (SyntaxNode)argument.Value)),
        IndexExpressionSyntax index => new SyntaxNode[] { index.Target }.Concat(index.Indices),
        ArrayLiteralExpressionSyntax array => array.Items,
        TypeTestExpressionSyntax test => new SyntaxNode[] { test.Value },
        ConversionExpressionSyntax conversion => new SyntaxNode[] { conversion.Value },
        NewExpressionSyntax creation => creation.Arguments.Select(static argument => (SyntaxNode)argument.Value),
        _ => Array.Empty<SyntaxNode>(),
    };

    private bool HasExtension(string name)
        => units.Any(unit => unit.Extensions.Any(extension => extension.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));

    private T? SingleOrFirst<T>(IEnumerable<T> values, string id, string itemName)
        where T : SyntaxNode
    {
        T[] array = values.ToArray();
        if (array.Length <= 1)
            return array.FirstOrDefault();
        foreach (T item in array.Skip(1))
        {
            diagnostics.Add(new Diagnostic(
                id,
                DiagnosticSeverity.Error,
                item.Span,
                $"Only one {itemName} is allowed in a project.",
                "一个项目只能有一份插件声明。"));
        }
        return array[0];
    }

    private void ValidateUnique(
        IEnumerable<(string Name, SourceSpan Span)> values,
        string englishKind,
        string chineseKind,
        bool allowOverloads = false)
    {
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, SourceSpan span) in values)
        {
            if (!names.Add(name) && !allowOverloads)
                Duplicate("EPL1309", name, span, englishKind, chineseKind);
        }
    }

    private void Duplicate(string id, string name, SourceSpan span, string englishKind, string chineseKind)
        => diagnostics.Add(new Diagnostic(
            id,
            DiagnosticSeverity.Error,
            span,
            $"Duplicate {englishKind} name '{name}'.",
            $"{chineseKind}名称“{name}”重复了。"));

    private static string RemoveGenericArguments(string value)
    {
        int genericStart = value.IndexOf('<');
        return genericStart >= 0 ? value[..genericStart] : value;
    }

    private static readonly HashSet<string> KnownExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "CLR",
        "LabAPI",
    };

    private static readonly string[] BuiltInTypeNames =
    {
        "无返回值", "void",
        "字节型", "byte",
        "短整数型", "short", "Int16",
        "整数型", "int", "Int32",
        "无符号整数型", "uint", "UInt32",
        "长整数型", "long", "Int64",
        "小数型", "float", "Single",
        "双精度小数型", "double", "Double",
        "逻辑型", "bool", "Boolean",
        "文本型", "string", "String",
        "字节集", "byte[]",
        "日期时间型", "DateTime",
        "通用型", "object", "Object",
        "子程序指针", "Delegate",
        "命令上下文", "CommandContext", "EPLab.Runtime.CommandContext",
        "Exception",
        "列表", "List",
        "字典", "Dictionary",
        "集合", "HashSet",
    };

    private static readonly HashSet<string> NumericTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "字节型", "byte",
        "短整数型", "short", "Int16",
        "整数型", "int", "Int32",
        "无符号整数型", "uint", "UInt32",
        "长整数型", "long", "Int64",
        "小数型", "float", "Single",
        "双精度小数型", "double", "Double",
    };

    private static readonly HashSet<string> CommandContextTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "命令上下文", "CommandContext", "EPLab.Runtime.CommandContext",
    };

    private static readonly HashSet<string> VoidTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "无返回值", "void",
    };

    private static readonly HashSet<string> BooleanTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "逻辑型", "bool", "Boolean",
    };
}
