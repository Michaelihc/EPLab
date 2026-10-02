# EPLab compatibility contract

### The honest one-sentence description

EPLab is an **unofficial, independently implemented, 易语言-inspired textual dialect** with documented source compatibility and useful extensions for CLR and LabAPI; it is not a replacement for every part of the official 易语言 IDE, compiler, runtime, libraries, or binary project format.

There is no complete public normative specification that EPLab can truthfully claim to implement. “Compatible” in this project means only the behavior listed in this file. If this file and an assumption about official behavior disagree, this file wins for EPLab.

### Status words

| Word | Meaning |
| --- | --- |
| Supported | EPLab intentionally implements this behavior and tests or examples exercise it. |
| Simplified | The common case works, but uncommon metadata or edge behavior is omitted. |
| Changed | EPLab deliberately uses a safer or clearer rule. |
| Extension | Friendly syntax invented for EPLab; do not expect the official compiler to accept it. |
| Unsupported | The compiler rejects it or cannot represent it. |
| Defined here | Public information was incomplete, so EPLab chose and documented a deterministic rule. |

### File and tool compatibility

| Area | Status | EPLab rule |
| --- | --- | --- |
| `.易` and `.eplab` | Supported | Plain UTF-8 source text, one declaration or statement line at a time. Both suffixes mean the same thing. |
| Official `.e` / `.ec` projects | Unsupported | Those are structured/binary project containers. Renaming one to `.易` will not work. |
| Official IDE forms and visual designer | Unsupported | EPLab builds server-side LabAPI DLLs and has no Windows form/resource importer. |
| Official compiler/runtime | Not used | EPLab has its own lexer, parser, model, checks, runtime helpers, and C# emitter. |
| Generated C# | Supported | Always emitted as readable source with `#line` mappings; the combined view ends in `.cs.txt` so it is not compiled twice. |
| Native machine-code output | Unsupported | LabAPI output is a managed .NET Framework 4.8 DLL. |
| Project format | Supported | Strict JSON `project.eplabproj`, currently `"format": 1`. Trailing commas and unknown properties are accepted; comments are rejected. |
| Dialect version | Supported | Source currently requires `.版本 2`; extension versions currently require `1`. |

EPLab does not import passwords, bookmarks, folded editor state, resource tables, icons, visual components, compiled modules, or support-library metadata from official project files.

### Language behavior

| Feature | Status | Exact EPLab behavior |
| --- | --- | --- |
| Chinese/Unicode identifiers | Supported | Identifiers preserve Unicode spelling. Direct CLR type and member names must still match the referenced API. |
| Full-width punctuation | Supported | Common full-width brackets, parentheses, comma, dot, colon, quotes, comparison and arithmetic symbols are normalized by the lexer. |
| Comments | Supported | A single quote `'` starts a comment when it is outside a string and top-level bracket nesting. |
| Primitive types | Supported | `字节型`, `短整数型`, `整数型`, `无符号整数型`, `长整数型`, `小数型`, `双精度小数型`, `逻辑型`, `文本型`, `字节集`, `日期时间型`, `通用型`, and `子程序指针` map to documented CLR types. |
| Omitted types | Supported/simplified | An omitted method result is `无返回值`; omitted fields and local variables default to `整数型`; an omitted parameter type defaults to `通用型`. Writing the type is still recommended at CLR boundaries. |
| Text | Changed | `文本型` is a .NET Unicode `string`. EPLab does not reproduce legacy byte-string/code-page accidents. |
| Text interpolation | Extension | `$“你好，{名字}”` becomes an inspectable C# interpolated string. Its expressions must also be valid in the generated CLR context. |
| Equality | Supported | `=` inside an expression means equality; a top-level `=` in a statement means assignment. `==` is also accepted for equality. |
| `≈` / `~=` | Supported | Converts both operands to invariant text and asks whether the **right** text is a prefix of the **left** text. It is not fuzzy numeric equality. |
| Boolean `且` / `或` | Changed | They short-circuit, like C# `&&` and `||`. This avoids unnecessary calls and surprises in server plugins. |
| Arithmetic | Simplified | CLR numeric operators and overflow behavior are used. `\` becomes division; it is integer division only when CLR operand types make it integer division. |
| Conversions | Changed | `转换类型` is a build-time-checked but runtime-unchecked CLR cast; `安全转换类型` uses a checked numeric cast; `到文本` uses invariant-culture `System.Convert`. EPLab does not imitate every implicit coercion of the official runtime. |
| Date/time literal | Defined here | Supported bracket formats are parsed with the `zh-CN` culture. The default date is `1899-12-30 00:00:00`, unspecified kind. |
| Null | Changed | `空`, `空对象`, and `null` become CLR `null`. A type suffix `?` uses CLR nullable/reference annotations where possible. |
| Optional parameters | Simplified | `可空` uses an `EOptional<T>` missing-value sentinel. `是否为空` distinguishes a missing optional value; for ordinary objects it tests `null`. |
| Named/ref/out arguments | Supported | `名字: 值`, `参考 值`/`传址 值`, and `输出 值` map to C# named, `ref`, and `out` arguments. `ref` and `out` are mutually exclusive and cannot be optional/defaulted. |
| Omitted arguments | Simplified | An empty argument slot emits `default`; it does not consult an official support-library command table. |
| Arrays | Supported | EPLab arrays are `EArray<T>`, start at index **1**, and throw for index 0 or an out-of-range index. |
| Multidimensional arrays | Supported | Last dimension varies fastest. A single index may address the flattened storage. `重定义数组` preserves the flattened prefix that still fits. |
| Array literals | Supported | `{1, 2, 3}` creates a one-dimensional, one-based `EArray<T>`. |
| CLR arrays | Extension | `CLR数组<T>` is a normal zero-based CLR `T[]`. `到CLR数组` and `从CLR数组` make explicit copies at the boundary, so one-based and zero-based indexing are never silently mixed. |
| Count loop | Supported | `.计次循环首(count, counter)` runs the counter from 1 through `count`, inclusive. |
| Range loop | Simplified | `.变量循环首(start, end, step, variable)` preserves a declared counter's numeric type; an omitted counter uses `double`. It includes the end, supports positive or negative steps, and rejects a zero step at runtime. |
| Pre/post-test loops | Supported | `.判断循环首/.判断循环尾` and `.循环判断首/.循环判断尾`. |
| Choice block | Simplified | `.判断开始` becomes an `if / else if / else` chain, not a jump table. |
| Classes/assemblies | Simplified | `.程序集` creates one CLR class with at most one base class. Fields, concrete methods, constructors, visibility, and common modifiers are supported; static classes, abstract methods, constructor modifiers, and partial classes are explicitly rejected. |
| Data types/enums | Supported | `.数据类型/.成员` emits a CLR struct; `.枚举/.枚举值` emits a CLR enum. |
| Constants | Simplified | `.常量` infers a CLR type for simple literals and emits a static readonly field, not necessarily a CLR compile-time constant. Other expressions use `dynamic`. `#类型.成员` addresses enum/static members. |
| Static local | Supported | A static local is hoisted to a private static field unique to its method. |
| Generic CLR type use | Supported with CLR extension | Types such as `列表<文本型>` can be used. Declaring a new generic type or generic method is unsupported. |
| Direct CLR calls | Supported with CLR extension | External resolution, overload choice, accessibility, and many type errors are deliberately delegated to the C# compiler. |
| DLL commands | Simplified | `.DLL命令` emits ordinary `DllImport` with a library and entry point. Custom calling conventions, encodings, layouts, and marshaling attributes are not exposed yet. |
| Delegates | Simplified | `&子程序名` emits a method group. Lambda syntax and custom delegate declarations are unsupported. |
| Source mapping | Supported | Generated declarations/statements use `#line`; C# diagnostics are reported against the `.易` source where possible. |

### EPLab extensions

These constructs favor clear server-plugin code over strict historical syntax:

- `.扩展 CLR 1` allows referenced CLR types, namespaces, assemblies, generics, direct calls, `创建对象`, `转换类型`, `是否类型`, and `默认值`.
- `.扩展 LabAPI 1` allows plugin metadata, generated configuration, event subscriptions, and command adapters.
- `.枚举循环首/.枚举循环尾` is a `foreach` loop.
- `.尝试/.捕获/.最终/.尝试结束` maps to CLR exception handling.
- `.临时设置/.临时设置结束` restores a value in `finally`, even when the body fails.
- `.锁定/.锁定结束` maps to `lock`.
- `.使用资源/.使用资源结束` maps to `using` and disposes the resource.
- `$“...”` interpolation, `?.`, and `??` use the predictable CLR meanings.

These extensions are source features, not raw C# escape blocks. EPLab does not provide a “paste arbitrary C# here” directive.

### LabAPI compatibility

`"target": "labapi-net48"` generates a class derived from `Plugin<TConfig>` and targets `net48`, which matches the LabAPI project guidance. It automatically references `LabApi.dll`, `Assembly-CSharp.dll`, `CommandSystem.Core.dll`, and `YamlDotNet.dll` from the selected game Managed folder.

The generated base-class/lifecycle shape was checked against LabAPI's public upstream [`Plugin` source](https://github.com/northwood-studios/LabAPI/blob/84b0da472e3ce42bf86cb49bde8379d00605a8f6/LabApi/Loader/Features/Plugins/Plugin.cs), [`Plugin<TConfig>` source](https://github.com/northwood-studios/LabAPI/blob/84b0da472e3ce42bf86cb49bde8379d00605a8f6/LabApi/Loader/Features/Plugins/Plugin%7BTConfig%7D.cs), and the project guide linked below.

Supported LabAPI conveniences:

- one project-wide `.LabAPI插件` declaration;
- generated name, description, author, plugin version, and required API version;
- generated configuration properties from `.配置项`;
- event subscribe/unsubscribe pairs from `.订阅事件`;
- optional lifecycle methods named `插件启动`, `_插件启动`, or `Enable`, and `插件停止`, `_插件停止`, or `Disable`;
- RA, player-console, and game-console `ICommand` adapters;
- bilingual `CommandContext` properties (`Arguments/参数`, `Sender/发送者`, `Response/回复`, `Success/成功`);
- an optional `PlayerPermissions` check for a command.

Current simplifications:

- event paths and handler signatures are type-checked by the C# backend rather than a bundled snapshot of the LabAPI metadata;
- failed enable rolls back events, commands, the entry instance, and hosted configuration; disable also clears them through nested `finally` blocks;
- command handlers receive one `CommandContext`; a `逻辑型` result is used directly, while a no-result handler sets `context.成功` and `context.回复`;
- `.配置项` can emit a YamlDotNet serialized-name alias and a `Description` attribute (Chinese description is preferred when both are present); optional numeric minimum/maximum fields are clamped when the plugin enables, and an invalid/NaN/infinite value falls back to the default;
- EPLab does not register Server-Specific Settings or HintServiceMeow automatically; source must call the corresponding dependency APIs explicitly and respect their ownership rules;
- no deployment, server restart, or Harmony patching happens automatically. Ordinary local helper DLLs are copied beside output; `game:` and `global:` dependencies remain server-provided and are not duplicated.

SCP:SL and LabAPI change over time. A project is checked against the exact local DLLs resolved at build time. EPLab records their paths and SHA-256 hashes in `build-info.json`; rebuilding against a different server version can reveal API changes.

### Not currently supported

- opening, saving, packing, or compiling official `.e` / `.ec` containers;
- official support libraries (`.fne`), compiled 易 modules, COM/OCX forms, database components, or IDE resource designers;
- interfaces, properties, indexer declarations, events, operators, extension methods, attributes, partial classes, or multiple inheritance;
- generic declarations, lambdas, local procedures, iterators/yield, `await`, query syntax, unsafe code, or arbitrary C# blocks;
- object/collection initializer syntax and pattern matching beyond the provided helpers;
- conditional compilation, macros, a package manager, an interactive debugger, or incremental compilation;
- exact reproduction of undocumented memory layout, reference counting, string encoding, floating-point corner cases, error recovery, or support-library side effects;
- client-side custom SCP:SL UI. LabAPI plugins remain server-side.

### How unknown or proprietary behavior is handled

When behavior is not publicly specified, EPLab follows this order:

1. Prefer reproducible public behavior and community-readable formats.
2. Prefer the rule that is simplest to explain and safest for a long-running game server.
3. Use deterministic .NET behavior rather than pretending to know proprietary internals.
4. Surface a compiler error when a safe interpretation is not possible.
5. Add the chosen rule to this document before calling it compatible.

This is interpolation, not reverse-engineered certainty. The goal is a comfortable and teachable language, with every important difference visible.

### Public references

- [易语言 official site and manuals](https://www.eyuyan.com/)
- [OpenEpl/TextECode](https://github.com/OpenEpl/TextECode) — public textual-code conventions
- [OpenEpl/EProjectFile](https://github.com/OpenEpl/EProjectFile) — public documentation of structured `.e/.ec` containers
- [OpenEpl/EplOnCppCore](https://github.com/OpenEpl/EplOnCppCore) — an independent translation precedent
- [aiqinxuancai/e-packager](https://github.com/aiqinxuancai/e-packager) — modern readable unpacked project representation
- [Northwood Studios LabAPI](https://github.com/northwood-studios/LabAPI) and its [project guide](https://github.com/northwood-studios/LabAPI/wiki/Creating-The-Project)

These are references, not a claim of endorsement. See [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).
