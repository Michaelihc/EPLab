namespace EPLab.Compiler.Syntax;

public sealed class SourceParser
{
    private readonly string filePath;
    private readonly SourceLine[] lines;
    private readonly DiagnosticBag diagnostics;
    private readonly List<ExtensionSyntax> extensions = new();
    private readonly List<string> namespaceImports = new();
    private readonly List<string> assemblyImports = new();
    private readonly List<ConfigItemSyntax> configItems = new();
    private readonly List<EventBindingSyntax> eventBindings = new();
    private readonly List<CommandBindingSyntax> commandBindings = new();
    private readonly List<DeclarationSyntax> declarations = new();
    private readonly PluginBuilder plugin = new();
    private ClassBuilder? currentClass;
    private int position;
    private int version;

    public SourceParser(string filePath, string text, DiagnosticBag diagnostics)
    {
        this.filePath = filePath;
        this.diagnostics = diagnostics;
        lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select((line, index) => new SourceLine(line, index + 1))
            .ToArray();
    }

    public CompilationUnitSyntax Parse()
    {
        while (!AtEnd)
        {
            SourceLine line = Current;
            string code = Clean(line.Text);
            if (code.Length == 0)
            {
                position++;
                continue;
            }

            if (TryParseFileDirective(code, line))
                continue;

            if (StartsWithDirective(code, ".程序集"))
            {
                FlushClass();
                currentClass = ParseClassHeader(code, line);
                position++;
                continue;
            }

            if (StartsWithDirective(code, ".数据类型"))
            {
                FlushClass();
                declarations.Add(ParseStruct());
                continue;
            }

            if (StartsWithDirective(code, ".枚举"))
            {
                FlushClass();
                declarations.Add(ParseEnum());
                continue;
            }

            if (StartsWithDirective(code, ".子程序") || StartsWithDirective(code, ".构造子程序"))
            {
                currentClass ??= new ClassBuilder("主程序集", null, Visibility.Internal, DeclarationModifiers.None, Span(line));
                currentClass.Methods.Add(ParseMethod());
                continue;
            }

            if (StartsWithDirective(code, ".程序集变量"))
            {
                currentClass ??= new ClassBuilder("主程序集", null, Visibility.Internal, DeclarationModifiers.None, Span(line));
                currentClass.Fields.Add(ParseField(code, line, ".程序集变量", defaultVisibility: Visibility.Private));
                position++;
                continue;
            }

            if (StartsWithDirective(code, ".全局变量"))
            {
                declarations.Add(ParseField(code, line, ".全局变量", defaultVisibility: Visibility.Internal, forceStatic: true));
                position++;
                continue;
            }

            if (StartsWithDirective(code, ".常量"))
            {
                declarations.Add(ParseConstant(code, line));
                position++;
                continue;
            }

            if (StartsWithDirective(code, ".DLL命令"))
            {
                declarations.Add(ParseDllDeclaration());
                continue;
            }

            Report(
                "EPL1201",
                line,
                $"This line is not valid at the top level: {code}",
                $"这一行不能写在最外层：{code}");
            position++;
        }

        FlushClass();
        if (version == 0)
        {
            diagnostics.Add(new Diagnostic(
                "EPL1202",
                DiagnosticSeverity.Error,
                new SourceSpan(filePath, 1),
                "The file must start with '.版本 2'.",
                "文件必须以“.版本 2”开头。"));
        }

        return new CompilationUnitSyntax(
            filePath,
            version,
            extensions,
            namespaceImports,
            assemblyImports,
            plugin.Build(filePath),
            configItems,
            eventBindings,
            commandBindings,
            declarations,
            new SourceSpan(filePath, 1, 1, lines.Length));
    }

    private bool TryParseFileDirective(string code, SourceLine line)
    {
        if (StartsWithDirective(code, ".版本"))
        {
            string raw = AfterDirective(code, ".版本");
            if (!int.TryParse(raw, out version) || version != 2)
                Report("EPL1203", line, "EPLab currently supports '.版本 2' only.", "EPLab 目前只支持“.版本 2”。");
            position++;
            return true;
        }

        if (StartsWithDirective(code, ".扩展"))
        {
            IReadOnlyList<string> parts = SplitWords(AfterDirective(code, ".扩展"));
            int extensionVersion = parts.Count > 1 && int.TryParse(parts[1], out int parsed) ? parsed : 1;
            if (parts.Count == 0)
                Report("EPL1204", line, "An extension name is required.", "请填写扩展名称。");
            else
                extensions.Add(new ExtensionSyntax(parts[0], extensionVersion, Span(line)));
            position++;
            return true;
        }

        if (StartsWithDirective(code, ".引用命名空间"))
        {
            string value = AfterDirective(code, ".引用命名空间").Trim();
            if (value.Length > 0)
                namespaceImports.Add(value);
            else
                Report("CLR2001", line, "A namespace name is required.", "请填写命名空间名称。");
            position++;
            return true;
        }

        if (StartsWithDirective(code, ".引用程序集"))
        {
            string value = SourceTextUtilities.Unquote(AfterDirective(code, ".引用程序集"));
            if (value.Length > 0)
                assemblyImports.Add(value);
            else
                Report("CLR2002", line, "An assembly name or path is required.", "请填写程序集名称或路径。");
            position++;
            return true;
        }

        if (StartsWithDirective(code, ".LabAPI插件") || StartsWithDirective(code, ".插件"))
        {
            string directive = StartsWithDirective(code, ".LabAPI插件") ? ".LabAPI插件" : ".插件";
            IReadOnlyList<string> fields = FieldsAfter(code, directive);
            plugin.ClassName = Field(fields, 0, "主程序集");
            plugin.ConfigClassName = EmptyToNull(Field(fields, 1));
            plugin.Span = Span(line);
            position++;
            return true;
        }

        if (TryPluginTextDirective(code, line, ".插件名称", value => plugin.Name = value)
            || TryPluginTextDirective(code, line, ".插件说明", value => plugin.Description = value)
            || TryPluginTextDirective(code, line, ".插件作者", value => plugin.Author = value)
            || TryPluginTextDirective(code, line, ".插件版本", value => plugin.Version = value)
            || TryPluginTextDirective(code, line, ".所需API版本", value => plugin.RequiredApiVersion = value))
            return true;

        if (StartsWithDirective(code, ".配置项"))
        {
            IReadOnlyList<string> fields = FieldsAfter(code, ".配置项");
            if (fields.Count < 3 || string.IsNullOrWhiteSpace(fields[0]) || string.IsNullOrWhiteSpace(fields[1]))
            {
                Report("LAB3001", line, "A config item needs a name, type, and default value.", "配置项需要名称、类型和默认值。");
            }
            else
            {
                configItems.Add(new ConfigItemSyntax(
                    fields[0],
                    ParseType(fields[1], line),
                    ParseExpression(fields[2], line),
                    EmptyToNull(SourceTextUtilities.Unquote(Field(fields, 3))),
                    EmptyToNull(SourceTextUtilities.Unquote(Field(fields, 4))),
                    EmptyToNull(SourceTextUtilities.Unquote(Field(fields, 5))),
                    string.IsNullOrWhiteSpace(Field(fields, 6)) ? null : ParseExpression(fields[6], line),
                    string.IsNullOrWhiteSpace(Field(fields, 7)) ? null : ParseExpression(fields[7], line),
                    Span(line)));
            }

            position++;
            return true;
        }

        if (StartsWithDirective(code, ".订阅事件") || StartsWithDirective(code, ".事件"))
        {
            string directive = StartsWithDirective(code, ".订阅事件") ? ".订阅事件" : ".事件";
            IReadOnlyList<string> fields = FieldsAfter(code, directive);
            if (fields.Count < 2)
                Report("LAB3002", line, "An event binding needs an event path and handler name.", "事件绑定需要事件路径和处理子程序名称。");
            else
                eventBindings.Add(new EventBindingSyntax(fields[0], fields[1], Span(line)));
            position++;
            return true;
        }

        if (StartsWithDirective(code, ".RA命令")
            || StartsWithDirective(code, ".玩家命令")
            || StartsWithDirective(code, ".游戏控制台命令"))
        {
            string directive = StartsWithDirective(code, ".RA命令")
                ? ".RA命令"
                : StartsWithDirective(code, ".玩家命令") ? ".玩家命令" : ".游戏控制台命令";
            CommandKind kind = directive switch
            {
                ".RA命令" => CommandKind.RemoteAdmin,
                ".玩家命令" => CommandKind.PlayerConsole,
                _ => CommandKind.GameConsole,
            };
            IReadOnlyList<string> fields = FieldsAfter(code, directive);
            if (fields.Count < 4)
            {
                Report("LAB3003", line, "A command needs name, aliases, description, and handler.", "命令需要名称、别名、说明和处理子程序。");
            }
            else
            {
                string[] aliases = SourceTextUtilities.Unquote(fields[1])
                    .Split(new[] { '|', ';', '；' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                commandBindings.Add(new CommandBindingSyntax(
                    kind,
                    SourceTextUtilities.Unquote(fields[0]),
                    aliases,
                    SourceTextUtilities.Unquote(fields[2]),
                    fields[3],
                    EmptyToNull(SourceTextUtilities.Unquote(Field(fields, 4))),
                    Span(line)));
            }

            position++;
            return true;
        }

        return false;
    }

    private bool TryPluginTextDirective(string code, SourceLine line, string directive, Action<string> setter)
    {
        if (!StartsWithDirective(code, directive))
            return false;
        setter(SourceTextUtilities.Unquote(AfterDirective(code, directive)));
        plugin.Span ??= Span(line);
        position++;
        return true;
    }

    private ClassBuilder ParseClassHeader(string code, SourceLine line)
    {
        IReadOnlyList<string> fields = FieldsAfter(code, ".程序集");
        string name = Field(fields, 0, "未命名程序集");
        TypeSyntax? baseType = string.IsNullOrWhiteSpace(Field(fields, 1)) ? null : ParseType(fields[1], line);
        Visibility visibility = ParseVisibility(Field(fields, 2), Visibility.Internal);
        DeclarationModifiers modifiers = ParseDeclarationModifiers(string.Join(' ', fields.Skip(2)));
        return new ClassBuilder(name, baseType, visibility, modifiers, Span(line));
    }

    private StructDeclarationSyntax ParseStruct()
    {
        SourceLine header = Current;
        IReadOnlyList<string> fields = FieldsAfter(Clean(header.Text), ".数据类型");
        string name = Field(fields, 0, "未命名类型");
        Visibility visibility = ParseVisibility(Field(fields, 1), Visibility.Internal);
        position++;
        List<FieldDeclarationSyntax> members = new();
        while (!AtEnd)
        {
            SourceLine line = Current;
            string code = Clean(line.Text);
            if (code.Length == 0)
            {
                position++;
                continue;
            }
            if (!StartsWithDirective(code, ".成员"))
                break;
            members.Add(ParseField(code, line, ".成员", Visibility.Public));
            position++;
        }

        return new StructDeclarationSyntax(name, visibility, members, Span(header));
    }

    private EnumDeclarationSyntax ParseEnum()
    {
        SourceLine header = Current;
        IReadOnlyList<string> fields = FieldsAfter(Clean(header.Text), ".枚举");
        string name = Field(fields, 0, "未命名枚举");
        Visibility visibility = ParseVisibility(Field(fields, 1), Visibility.Internal);
        position++;
        List<EnumMemberSyntax> members = new();
        while (!AtEnd)
        {
            SourceLine line = Current;
            string code = Clean(line.Text);
            if (code.Length == 0)
            {
                position++;
                continue;
            }
            if (!StartsWithDirective(code, ".枚举值"))
                break;
            IReadOnlyList<string> memberFields = FieldsAfter(code, ".枚举值");
            members.Add(new EnumMemberSyntax(
                Field(memberFields, 0, "未命名值"),
                string.IsNullOrWhiteSpace(Field(memberFields, 1)) ? null : ParseExpression(memberFields[1], line),
                Span(line)));
            position++;
        }

        return new EnumDeclarationSyntax(name, visibility, members, Span(header));
    }

    private MethodDeclarationSyntax ParseMethod()
    {
        SourceLine header = Current;
        string code = Clean(header.Text);
        bool constructor = StartsWithDirective(code, ".构造子程序");
        string directive = constructor ? ".构造子程序" : ".子程序";
        IReadOnlyList<string> fields = FieldsAfter(code, directive);
        string name = Field(fields, 0, constructor ? "_初始化" : "未命名子程序");
        TypeSyntax returnType = ParseType(Field(fields, 1, constructor ? "" : "无返回值"), header);
        Visibility visibility = ParseVisibility(Field(fields, 2), Visibility.Private);
        DeclarationModifiers modifiers = ParseDeclarationModifiers(string.Join(' ', fields.Skip(2)));
        if (constructor)
            modifiers |= DeclarationModifiers.Constructor;
        position++;

        List<ParameterSyntax> parameters = new();
        List<LocalVariableSyntax> locals = new();
        while (!AtEnd)
        {
            SourceLine line = Current;
            string declaration = Clean(line.Text);
            if (StartsWithDirective(declaration, ".参数"))
            {
                parameters.Add(ParseParameter(declaration, line));
                position++;
            }
            else if (StartsWithDirective(declaration, ".局部变量"))
            {
                locals.Add(ParseLocal(declaration, line));
                position++;
            }
            else if (declaration.Length == 0)
            {
                position++;
            }
            else
            {
                break;
            }
        }

        IReadOnlyList<StatementSyntax> statements = ParseStatementBlock(IsMethodBoundary, null);
        return new MethodDeclarationSyntax(name, returnType, visibility, modifiers, parameters, locals, statements, Span(header));
    }

    private DllDeclarationSyntax ParseDllDeclaration()
    {
        SourceLine header = Current;
        IReadOnlyList<string> fields = FieldsAfter(Clean(header.Text), ".DLL命令");
        position++;
        List<ParameterSyntax> parameters = new();
        while (!AtEnd)
        {
            string code = Clean(Current.Text);
            if (code.Length == 0)
            {
                position++;
                continue;
            }
            if (!StartsWithDirective(code, ".参数"))
                break;
            parameters.Add(ParseParameter(code, Current, dllParameter: true));
            position++;
        }

        return new DllDeclarationSyntax(
            Field(fields, 0, "未命名DLL命令"),
            ParseType(Field(fields, 1, "无返回值"), header),
            SourceTextUtilities.Unquote(Field(fields, 2)),
            SourceTextUtilities.Unquote(Field(fields, 3, Field(fields, 0))),
            ParseVisibility(Field(fields, 4), Visibility.Internal),
            parameters,
            Span(header));
    }

    private FieldDeclarationSyntax ParseField(
        string code,
        SourceLine line,
        string directive,
        Visibility defaultVisibility,
        bool forceStatic = false)
    {
        IReadOnlyList<string> fields = FieldsAfter(code, directive);
        string modifierText = Field(fields, 2);
        DeclarationModifiers modifiers = ParseDeclarationModifiers(modifierText);
        if (forceStatic)
            modifiers |= DeclarationModifiers.Static;
        return new FieldDeclarationSyntax(
            Field(fields, 0, "未命名变量"),
            ParseType(Field(fields, 1, "整数型"), line),
            ParseVisibility(modifierText, defaultVisibility),
            modifiers,
            ParseArrayShape(Field(fields, 3), line),
            string.IsNullOrWhiteSpace(Field(fields, 4)) ? null : ParseExpression(fields[4], line),
            Span(line));
    }

    private ConstantDeclarationSyntax ParseConstant(string code, SourceLine line)
    {
        IReadOnlyList<string> fields = FieldsAfter(code, ".常量");
        return new ConstantDeclarationSyntax(
            Field(fields, 0, "未命名常量"),
            ParseExpression(Field(fields, 1, "0"), line),
            ParseVisibility(Field(fields, 2), Visibility.Internal),
            Span(line));
    }

    private ParameterSyntax ParseParameter(string code, SourceLine line, bool dllParameter = false)
    {
        IReadOnlyList<string> fields = FieldsAfter(code, ".参数");
        string modifierText = Field(fields, 2);
        ParameterModifiers modifiers = ParseParameterModifiers(modifierText, dllParameter);
        TypeSyntax type = ParseType(Field(fields, 1, "通用型"), line, modifiers.HasFlag(ParameterModifiers.Array));
        return new ParameterSyntax(
            Field(fields, 0, "未命名参数"),
            type,
            modifiers,
            string.IsNullOrWhiteSpace(Field(fields, 3)) ? null : ParseExpression(fields[3], line),
            Span(line));
    }

    private LocalVariableSyntax ParseLocal(string code, SourceLine line)
    {
        IReadOnlyList<string> fields = FieldsAfter(code, ".局部变量");
        return new LocalVariableSyntax(
            Field(fields, 0, "未命名局部变量"),
            ParseType(Field(fields, 1, "整数型"), line),
            ParseDeclarationModifiers(Field(fields, 2)),
            ParseArrayShape(Field(fields, 3), line),
            string.IsNullOrWhiteSpace(Field(fields, 4)) ? null : ParseExpression(fields[4], line),
            Span(line));
    }

    private IReadOnlyList<StatementSyntax> ParseStatementBlock(
        Func<string, bool> isEnd,
        ISet<string>? explicitEnds)
    {
        List<StatementSyntax> statements = new();
        while (!AtEnd)
        {
            SourceLine line = Current;
            string code = Clean(line.Text);
            if (isEnd(code) || explicitEnds is not null && explicitEnds.Any(end => StartsWithDirective(code, end)))
                break;
            if (code.Length == 0)
            {
                position++;
                continue;
            }

            statements.Add(ParseStatement(code, line));
        }

        return statements;
    }

    private StatementSyntax ParseStatement(string code, SourceLine line)
    {
        if (StartsWithDirective(code, ".如果真"))
        {
            ExpressionSyntax condition = ParseDirectiveExpression(code, ".如果真", line);
            position++;
            IReadOnlyList<StatementSyntax> body = ParseStatementBlock(_ => false, new HashSet<string> { ".如果真结束" });
            RequireEnd(".如果真结束", line);
            return new IfStatementSyntax(condition, body, Array.Empty<StatementSyntax>(), Span(line));
        }

        if (StartsWithDirective(code, ".如果"))
        {
            ExpressionSyntax condition = ParseDirectiveExpression(code, ".如果", line);
            position++;
            IReadOnlyList<StatementSyntax> thenBody = ParseStatementBlock(_ => false, new HashSet<string> { ".否则", ".如果结束" });
            IReadOnlyList<StatementSyntax> elseBody = Array.Empty<StatementSyntax>();
            if (!AtEnd && StartsWithDirective(Clean(Current.Text), ".否则"))
            {
                position++;
                elseBody = ParseStatementBlock(_ => false, new HashSet<string> { ".如果结束" });
            }

            RequireEnd(".如果结束", line);
            return new IfStatementSyntax(condition, thenBody, elseBody, Span(line));
        }

        if (StartsWithDirective(code, ".判断循环首"))
        {
            ExpressionSyntax condition = ParseDirectiveExpression(code, ".判断循环首", line);
            position++;
            IReadOnlyList<StatementSyntax> body = ParseStatementBlock(_ => false, new HashSet<string> { ".判断循环尾" });
            RequireEnd(".判断循环尾", line);
            return new WhileStatementSyntax(condition, body, false, Span(line));
        }

        if (StartsWithDirective(code, ".循环判断首"))
        {
            position++;
            IReadOnlyList<StatementSyntax> body = ParseStatementBlock(_ => false, new HashSet<string> { ".循环判断尾" });
            ExpressionSyntax condition = AtEnd
                ? new LiteralExpressionSyntax(false, "假", Span(line))
                : ParseDirectiveExpression(Clean(Current.Text), ".循环判断尾", Current);
            RequireEnd(".循环判断尾", line);
            return new WhileStatementSyntax(condition, body, true, Span(line));
        }

        if (StartsWithDirective(code, ".计次循环首"))
        {
            IReadOnlyList<ExpressionSyntax> args = ParseDirectiveArguments(code, ".计次循环首", line);
            position++;
            IReadOnlyList<StatementSyntax> body = ParseStatementBlock(_ => false, new HashSet<string> { ".计次循环尾" });
            RequireEnd(".计次循环尾", line);
            return new CountLoopStatementSyntax(
                Argument(args, 0, line, "1"),
                args.Count > 1 && args[1] is not MissingExpressionSyntax ? args[1] : null,
                body,
                Span(line));
        }

        if (StartsWithDirective(code, ".变量循环首"))
        {
            IReadOnlyList<ExpressionSyntax> args = ParseDirectiveArguments(code, ".变量循环首", line);
            position++;
            IReadOnlyList<StatementSyntax> body = ParseStatementBlock(_ => false, new HashSet<string> { ".变量循环尾" });
            RequireEnd(".变量循环尾", line);
            return new RangeLoopStatementSyntax(
                Argument(args, 0, line, "0"),
                Argument(args, 1, line, "0"),
                args.Count > 2 && args[2] is not MissingExpressionSyntax ? args[2] : null,
                args.Count > 3 && args[3] is not MissingExpressionSyntax ? args[3] : null,
                body,
                Span(line));
        }

        if (StartsWithDirective(code, ".枚举循环首"))
        {
            IReadOnlyList<string> fields = FieldsInsideDirective(code, ".枚举循环首");
            string variable = Field(fields, 0, "成员");
            TypeSyntax? variableType = fields.Count >= 3 ? ParseType(fields[1], line) : null;
            string collectionText = fields.Count >= 3 ? fields[2] : Field(fields, 1, "空");
            position++;
            IReadOnlyList<StatementSyntax> body = ParseStatementBlock(_ => false, new HashSet<string> { ".枚举循环尾" });
            RequireEnd(".枚举循环尾", line);
            return new ForEachStatementSyntax(variable, variableType, ParseExpression(collectionText, line), body, Span(line));
        }

        if (StartsWithDirective(code, ".判断开始"))
            return ParseChoose(line);

        if (StartsWithDirective(code, ".尝试"))
            return ParseTry(line);

        if (StartsWithDirective(code, ".临时设置"))
        {
            IReadOnlyList<ExpressionSyntax> args = ParseDirectiveArguments(code, ".临时设置", line);
            position++;
            IReadOnlyList<StatementSyntax> body = ParseStatementBlock(_ => false, new HashSet<string> { ".临时设置结束" });
            RequireEnd(".临时设置结束", line);
            return new TemporarySetStatementSyntax(Argument(args, 0, line, "空"), Argument(args, 1, line, "空"), body, Span(line));
        }

        if (StartsWithDirective(code, ".锁定"))
        {
            ExpressionSyntax target = ParseDirectiveExpression(code, ".锁定", line);
            position++;
            IReadOnlyList<StatementSyntax> body = ParseStatementBlock(_ => false, new HashSet<string> { ".锁定结束" });
            RequireEnd(".锁定结束", line);
            return new LockStatementSyntax(target, body, Span(line));
        }

        if (StartsWithDirective(code, ".使用资源"))
        {
            IReadOnlyList<string> fields = FieldsInsideDirective(code, ".使用资源");
            position++;
            IReadOnlyList<StatementSyntax> body = ParseStatementBlock(_ => false, new HashSet<string> { ".使用资源结束" });
            RequireEnd(".使用资源结束", line);
            return new UsingStatementSyntax(
                Field(fields, 0, "资源"),
                ParseExpression(Field(fields, 1, "空"), line),
                body,
                Span(line));
        }

        int assignment = FindTopLevelAssignment(code);
        position++;
        if (assignment >= 0)
        {
            string left = code[..assignment].Trim();
            string right = code[(assignment + 1)..].Trim();
            return new AssignmentStatementSyntax(ParseExpression(left, line), ParseExpression(right, line), Span(line));
        }

        return new ExpressionStatementSyntax(ParseExpression(code, line), Span(line));
    }

    private ChooseStatementSyntax ParseChoose(SourceLine header)
    {
        position++;
        List<ConditionalBranchSyntax> branches = new();
        IReadOnlyList<StatementSyntax> defaultBody = Array.Empty<StatementSyntax>();
        while (!AtEnd && !StartsWithDirective(Clean(Current.Text), ".判断结束"))
        {
            SourceLine line = Current;
            string code = Clean(line.Text);
            if (StartsWithDirective(code, ".判断"))
            {
                ExpressionSyntax condition = ParseDirectiveExpression(code, ".判断", line);
                position++;
                IReadOnlyList<StatementSyntax> body = ParseStatementBlock(
                    _ => false,
                    new HashSet<string> { ".判断", ".默认", ".判断结束" });
                branches.Add(new ConditionalBranchSyntax(condition, body, Span(line)));
            }
            else if (StartsWithDirective(code, ".默认"))
            {
                position++;
                defaultBody = ParseStatementBlock(_ => false, new HashSet<string> { ".判断结束" });
            }
            else
            {
                Report("EPL1210", line, "Expected '.判断' or '.默认'.", "这里需要“.判断”或“.默认”。");
                position++;
            }
        }

        RequireEnd(".判断结束", header);
        return new ChooseStatementSyntax(branches, defaultBody, Span(header));
    }

    private TryStatementSyntax ParseTry(SourceLine header)
    {
        position++;
        IReadOnlyList<StatementSyntax> tryBody = ParseStatementBlock(_ => false, new HashSet<string> { ".捕获", ".最终", ".尝试结束" });
        List<CatchClauseSyntax> catches = new();
        IReadOnlyList<StatementSyntax> finallyBody = Array.Empty<StatementSyntax>();
        while (!AtEnd && StartsWithDirective(Clean(Current.Text), ".捕获"))
        {
            SourceLine line = Current;
            IReadOnlyList<string> fields = FieldsInsideDirective(Clean(line.Text), ".捕获");
            position++;
            IReadOnlyList<StatementSyntax> body = ParseStatementBlock(_ => false, new HashSet<string> { ".捕获", ".最终", ".尝试结束" });
            catches.Add(new CatchClauseSyntax(
                Field(fields, 0, "异常"),
                ParseType(Field(fields, 1, "Exception"), line),
                body,
                Span(line)));
        }

        if (!AtEnd && StartsWithDirective(Clean(Current.Text), ".最终"))
        {
            position++;
            finallyBody = ParseStatementBlock(_ => false, new HashSet<string> { ".尝试结束" });
        }

        RequireEnd(".尝试结束", header);
        return new TryStatementSyntax(tryBody, catches, finallyBody, Span(header));
    }

    private void RequireEnd(string directive, SourceLine start)
    {
        if (!AtEnd && StartsWithDirective(Clean(Current.Text), directive))
        {
            position++;
            return;
        }

        Report(
            "EPL1205",
            start,
            $"'{directive}' is missing for the block that starts here.",
            $"从这里开始的代码块缺少“{directive}”。");
    }

    private ExpressionSyntax ParseDirectiveExpression(string code, string directive, SourceLine line)
    {
        string value = AfterDirective(code, directive).Trim();
        value = TrimOuterParentheses(value);
        return ParseExpression(value, line);
    }

    private IReadOnlyList<ExpressionSyntax> ParseDirectiveArguments(string code, string directive, SourceLine line)
        => FieldsInsideDirective(code, directive).Select(value => ParseExpressionOrMissing(value, line)).ToArray();

    private IReadOnlyList<string> FieldsInsideDirective(string code, string directive)
    {
        string value = AfterDirective(code, directive).Trim();
        value = TrimOuterParentheses(value);
        return SourceTextUtilities.SplitFields(value);
    }

    private ExpressionSyntax Argument(IReadOnlyList<ExpressionSyntax> arguments, int index, SourceLine line, string fallback)
        => index < arguments.Count && arguments[index] is not MissingExpressionSyntax
            ? arguments[index]
            : ParseExpression(fallback, line);

    private ExpressionSyntax ParseExpression(string value, SourceLine line)
        => new ExpressionParser(value, filePath, line.Number, diagnostics).Parse();

    private ExpressionSyntax ParseExpressionOrMissing(string value, SourceLine line)
        => ExpressionParser.ParseOrMissing(value, filePath, line.Number, diagnostics);

    private TypeSyntax ParseType(string value, SourceLine line, bool forceArray = false)
    {
        value = string.IsNullOrWhiteSpace(value) ? "无返回值" : value.Trim();
        value = value.Replace('《', '<').Replace('》', '>').Replace('？', '?');
        bool isNullable = value.EndsWith("?", StringComparison.Ordinal);
        if (isNullable)
            value = value[..^1].TrimEnd();
        bool isArray = forceArray || value.EndsWith("[]", StringComparison.Ordinal);
        if (value.EndsWith("[]", StringComparison.Ordinal))
            value = value[..^2].TrimEnd();
        return new TypeSyntax(value, isArray, isNullable, Span(line));
    }

    private ArrayShapeSyntax? ParseArrayShape(string value, SourceLine line)
    {
        value = SourceTextUtilities.Unquote(value);
        if (string.IsNullOrWhiteSpace(value))
            return null;
        IReadOnlyList<string> fields = SourceTextUtilities.SplitFields(value);
        bool dynamic = fields.Count == 1 && fields[0].Trim() == "0";
        return new ArrayShapeSyntax(fields.Select(field => ParseExpression(field, line)).ToArray(), dynamic, Span(line));
    }

    private void FlushClass()
    {
        if (currentClass is null)
            return;
        declarations.Add(currentClass.Build());
        currentClass = null;
    }

    private bool IsMethodBoundary(string code)
        => TopLevelDirectives.Any(directive => StartsWithDirective(code, directive));

    private static int FindTopLevelAssignment(string code)
    {
        bool inString = false;
        char opening = '\0';
        int nesting = 0;
        for (int i = 0; i < code.Length; i++)
        {
            char character = code[i];
            if (inString)
            {
                if (character == '\\')
                {
                    i++;
                    continue;
                }
                if (opening == '"' ? character == '"' : character is '“' or '”')
                    inString = false;
                continue;
            }
            if (character is '"' or '“' or '”')
            {
                inString = true;
                opening = character;
            }
            else if (character is '(' or '（' or '[' or '【' or '{' or '｛')
                nesting++;
            else if (character is ')' or '）' or ']' or '】' or '}' or '｝')
                nesting = Math.Max(0, nesting - 1);
            else if (nesting == 0 && character is '=' or '＝')
            {
                char previous = i > 0 ? code[i - 1] : '\0';
                char next = i + 1 < code.Length ? code[i + 1] : '\0';
                if (previous is '!' or '<' or '>' or '=' || next == '=')
                    continue;
                return i;
            }
        }
        return -1;
    }

    private void Report(string id, SourceLine line, string english, string chinese)
        => diagnostics.Add(new Diagnostic(id, DiagnosticSeverity.Error, Span(line), english, chinese));

    private SourceSpan Span(SourceLine line) => new(filePath, line.Number, 1, Math.Max(1, line.Text.Length));

    private static string Clean(string value) => SourceTextUtilities.StripComment(value).Trim();

    private static bool StartsWithDirective(string code, string directive)
        => code.Equals(directive, StringComparison.OrdinalIgnoreCase)
            || code.StartsWith(directive + " ", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith(directive + "\t", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith(directive + "(", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith(directive + "（", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith(directive + ",", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith(directive + "，", StringComparison.OrdinalIgnoreCase);

    private static string AfterDirective(string code, string directive)
        => code.Length <= directive.Length ? string.Empty : code[directive.Length..].TrimStart();

    private static IReadOnlyList<string> FieldsAfter(string code, string directive)
        => SourceTextUtilities.SplitFields(AfterDirective(code, directive));

    private static string Field(IReadOnlyList<string> fields, int index, string fallback = "")
        => index < fields.Count && !string.IsNullOrWhiteSpace(fields[index]) ? fields[index].Trim() : fallback;

    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string TrimOuterParentheses(string value)
    {
        value = value.Trim();
        if (value.Length < 2 || value[0] is not ('(' or '（'))
            return value;

        Stack<char> delimiters = new();
        bool inString = false;
        char openingQuote = '\0';
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (inString)
            {
                if (character == '\\')
                {
                    i++;
                    continue;
                }

                if (openingQuote == '"' ? character == '"' : character is '“' or '”')
                    inString = false;
                continue;
            }

            if (character is '"' or '“' or '”')
            {
                inString = true;
                openingQuote = character;
                continue;
            }

            char delimiter = character switch
            {
                '(' or '（' => '(',
                '[' or '【' => '[',
                '{' or '｛' => '{',
                _ => '\0',
            };
            if (delimiter != '\0')
            {
                delimiters.Push(delimiter);
                continue;
            }

            char closing = character switch
            {
                ')' or '）' => '(',
                ']' or '】' => '[',
                '}' or '｝' => '{',
                _ => '\0',
            };
            if (closing == '\0')
                continue;
            if (delimiters.Count == 0 || delimiters.Pop() != closing)
                return value;

            if (delimiters.Count == 0)
                return i == value.Length - 1 ? value[1..^1].Trim() : value;
        }

        return value;
    }

    private static IReadOnlyList<string> SplitWords(string value)
        => value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static Visibility ParseVisibility(string value, Visibility fallback)
    {
        if (value.Contains("公开", StringComparison.OrdinalIgnoreCase) || value.Contains("public", StringComparison.OrdinalIgnoreCase))
            return Visibility.Public;
        if (value.Contains("保护", StringComparison.OrdinalIgnoreCase) || value.Contains("protected", StringComparison.OrdinalIgnoreCase))
            return Visibility.Protected;
        if (value.Contains("内部", StringComparison.OrdinalIgnoreCase) || value.Contains("internal", StringComparison.OrdinalIgnoreCase))
            return Visibility.Internal;
        if (value.Contains("私有", StringComparison.OrdinalIgnoreCase) || value.Contains("private", StringComparison.OrdinalIgnoreCase))
            return Visibility.Private;
        return fallback;
    }

    private static DeclarationModifiers ParseDeclarationModifiers(string value)
    {
        DeclarationModifiers result = DeclarationModifiers.None;
        Add("静态", "static", DeclarationModifiers.Static);
        Add("只读", "readonly", DeclarationModifiers.ReadOnly);
        Add("虚", "virtual", DeclarationModifiers.Virtual);
        Add("重写", "override", DeclarationModifiers.Override);
        Add("抽象", "abstract", DeclarationModifiers.Abstract);
        Add("密封", "sealed", DeclarationModifiers.Sealed);
        Add("异步", "async", DeclarationModifiers.Async);
        Add("分部", "partial", DeclarationModifiers.Partial);
        return result;

        void Add(string chinese, string english, DeclarationModifiers modifier)
        {
            if (value.Contains(chinese, StringComparison.OrdinalIgnoreCase)
                || value.Contains(english, StringComparison.OrdinalIgnoreCase))
                result |= modifier;
        }
    }

    private static ParameterModifiers ParseParameterModifiers(string value, bool dllParameter)
    {
        ParameterModifiers result = ParameterModifiers.None;
        if (value.Contains("可空", StringComparison.OrdinalIgnoreCase) || value.Contains("optional", StringComparison.OrdinalIgnoreCase))
            result |= ParameterModifiers.Optional;
        if (value.Contains("参考", StringComparison.OrdinalIgnoreCase) || value.Contains("ref", StringComparison.OrdinalIgnoreCase))
            result |= ParameterModifiers.ByReference;
        if (value.Contains("传址", StringComparison.OrdinalIgnoreCase))
            result |= dllParameter ? ParameterModifiers.ByReference : ParameterModifiers.ByReference;
        if (value.Contains("输出", StringComparison.OrdinalIgnoreCase) || value.Contains("out", StringComparison.OrdinalIgnoreCase))
            result |= ParameterModifiers.Out;
        if (value.Contains("数组", StringComparison.OrdinalIgnoreCase) || value.Contains("array", StringComparison.OrdinalIgnoreCase))
            result |= ParameterModifiers.Array;
        if (value.Contains("参数数组", StringComparison.OrdinalIgnoreCase) || value.Contains("params", StringComparison.OrdinalIgnoreCase))
            result |= ParameterModifiers.Params | ParameterModifiers.Array;
        return result;
    }

    private bool AtEnd => position >= lines.Length;

    private SourceLine Current => lines[position];

    private readonly record struct SourceLine(string Text, int Number);

    private sealed class ClassBuilder
    {
        public ClassBuilder(string name, TypeSyntax? baseType, Visibility visibility, DeclarationModifiers modifiers, SourceSpan span)
        {
            Name = name;
            BaseType = baseType;
            Visibility = visibility;
            Modifiers = modifiers;
            Span = span;
        }

        public string Name { get; }
        public TypeSyntax? BaseType { get; }
        public Visibility Visibility { get; }
        public DeclarationModifiers Modifiers { get; }
        public SourceSpan Span { get; }
        public List<FieldDeclarationSyntax> Fields { get; } = new();
        public List<MethodDeclarationSyntax> Methods { get; } = new();

        public ClassDeclarationSyntax Build() => new(Name, BaseType, Visibility, Modifiers, Fields, Methods, Span);
    }

    private sealed class PluginBuilder
    {
        public string? ClassName { get; set; }
        public string? ConfigClassName { get; set; }
        public string Name { get; set; } = "EPLab Plugin";
        public string Description { get; set; } = "Built with EPLab.";
        public string Author { get; set; } = "Unknown";
        public string Version { get; set; } = "1.0.0";
        public string? RequiredApiVersion { get; set; }
        public SourceSpan? Span { get; set; }

        public PluginSyntax? Build(string filePath)
            => ClassName is null
                ? null
                : new PluginSyntax(
                    ClassName,
                    ConfigClassName,
                    Name,
                    Description,
                    Author,
                    Version,
                    RequiredApiVersion,
                    Span ?? new SourceSpan(filePath, 1));
    }

    private static readonly string[] TopLevelDirectives =
    {
        ".版本", ".扩展", ".引用命名空间", ".引用程序集", ".LabAPI插件", ".插件",
        ".插件名称", ".插件说明", ".插件作者", ".插件版本", ".所需API版本", ".配置项",
        ".订阅事件", ".事件", ".RA命令", ".玩家命令", ".游戏控制台命令", ".程序集",
        ".程序集变量", ".全局变量", ".子程序", ".构造子程序", ".数据类型", ".枚举",
        ".常量", ".DLL命令",
    };
}
