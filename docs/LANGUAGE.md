# EPLab language reference

This describes the compiler in this folder today. It does not describe every feature of official 易语言. Read the [compatibility contract](COMPATIBILITY.md) for intentional differences.

### 1. Project file

An EPLab project is a JSON file normally named `project.eplabproj`. Project files use strict JSON: trailing commas are accepted, comments are rejected, and unknown future properties survive GUI settings saves.

```json
{
  "format": 1,
  "name": "HelloEPLab",
  "target": "labapi-net48",
  "rootNamespace": "HelloEPLab",
  "sourceDirectories": ["src"],
  "references": [
    "game:LabApi",
    "global:ServerKeybinds",
    "lib/MyHelper.dll"
  ],
  "managedDirectory": null,
  "globalDependenciesDirectory": null,
  "outputDirectory": "bin"
}
```

| Property | Meaning |
| --- | --- |
| `format` | Must currently be `1`. |
| `name` | Output assembly name. Use a valid file/assembly name. |
| `target` | Must be `labapi-net48`, `library-net48`, `library-net8`, or `syntax-only`; any other value is an error. A `.LabAPI插件` declaration specifically requires `labapi-net48`. |
| `rootNamespace` | CLR namespace containing generated program types. |
| `sourceDirectories` | Recursively scanned for `.易` and `.eplab` files. Paths are relative to the project file. |
| `references` | CLR DLL references. See the prefixes below. |
| `managedDirectory` | Optional SCP:SL `SCPSL_Data/Managed` path. Environment variables are expanded, and a relative path is based at the project file. |
| `globalDependenciesDirectory` | Optional LabAPI `dependencies/global` path. A relative path is based at the project file. |
| `outputDirectory` | Output base folder; configuration such as `Release` is added beneath it. |

Reference forms:

```text
game:LabApi              → <Managed>/LabApi.dll
global:ServerKeybinds    → <dependencies/global>/ServerKeybinds.dll
lib/MyHelper.dll         → relative to project.eplabproj
D:\shared\MyHelper.dll   → absolute path
%MY_LIBS%\MyHelper.dll   → environment variable is expanded
```

The Managed folder is chosen in this order: command-line `--managed`, project property, `SCP_SL_MANAGED` environment variable, then the default Steam dedicated-server path. The global-dependencies folder uses command-line `--dependencies`, project property, then the normal `%APPDATA%` LabAPI path.

`syntax-only` is intended for `eplab check`: it stops after EPLab parsing, binding, and C# generation instead of running the C# compiler. The other targets make `check` perform the full backend type-build.

### 2. Source-file shape

Every source file is plain UTF-8 and needs:

```e
.版本 2
```

Useful project-level directives can appear after it:

```e
.扩展 CLR 1
.扩展 LabAPI 1
.引用命名空间 System.Collections.Generic
.引用程序集 “game:Mirror”
```

- `CLR` and `LabAPI` are the only extension names today; their supported version is `1`.
- Namespace imports become C# `using` directives.
- Assembly imports use the same reference forms as the project JSON.
- Imports and extensions from all source files are combined.
- A single quote starts a comment: `' this is a comment`.
- Blank lines are for people; they do not end a method or block.

There is no `.程序集结束`. A method ends when the next top-level declaration begins. Control-flow blocks have explicit endings.

### 3. Characters, names, and punctuation

Names start with a Chinese/Unicode letter or `_`; later characters may also be digits. `@` and `$` are rejected outside their documented uses, such as the `$“...”` interpolation prefix. Keywords are contextual: a name is usually only special where its syntax expects it.

Both styles work:

```e
日志.信息($"hello")
日志。信息（$“你好”）
```

Supported common full-width alternatives include `（）【】｛｝，。：？！＋－＊／＼％＝＜＞` and curly Chinese quotes. Use ordinary ASCII for CLR names when the referenced API spells them that way.

Strings accept straight or curly quotes. `\n`, `\r`, `\t`, `\\`, and `\"` escapes are understood. Doubling the matching quote also places a quote in the value. Prefix a string with `$` for interpolation:

```e
$“玩家 {玩家.Nickname} 已加入；分数 {分数:0.00}”
```

Interpolation holes are parsed as EPLab expressions, so friendly names and normal diagnostics still work. Text after a top-level colon is passed as the CLR format string.

### 4. Types

| EPLab name | CLR output |
| --- | --- |
| `无返回值` | `void` |
| `字节型` | `byte` |
| `短整数型` | `short` |
| `整数型` | `int` |
| `无符号整数型` | `uint` |
| `长整数型` | `long` |
| `小数型` | `float` |
| `双精度小数型` | `double` |
| `逻辑型` | `bool` |
| `文本型` | `string` |
| `字节集` | `byte[]` |
| `日期时间型` | `System.DateTime` |
| `通用型` | `object` |
| `子程序指针` | `System.Delegate` |
| `命令上下文` | `EPLab.Runtime.CommandContext` |
| `列表<T>` | `System.Collections.Generic.List<T>` |
| `字典<K,V>` | `System.Collections.Generic.Dictionary<K,V>` |
| `集合<T>` | `System.Collections.Generic.HashSet<T>` |
| `只读列表<T>` | `System.Collections.Generic.IReadOnlyList<T>` |
| `玩家` | `LabApi.Features.Wrappers.Player` |
| `CLR数组<T>` | zero-based CLR `T[]` |

English CLR aliases such as `int`, `Int32`, `string`, `Boolean`, and `DateTime` also work. With `.扩展 CLR 1`, any referenced qualified type may be used. Chinese angle brackets `《》` may replace `< >` in a type. A trailing `?` asks for a nullable type.

### 5. Declarations

Directive fields are separated by ASCII or Chinese commas. Empty fields keep their position.

#### Class/assembly

```text
.程序集 name, base-type, visibility and modifiers
```

```e
.程序集 计分器, , 公开 密封
.程序集变量 总分, 整数型, 私有, , 0
```

Visibility words: `公开/public`, `保护/protected`, `内部/internal`, `私有/private`.

Declaration modifiers include `静态/static`, `只读/readonly`, `虚/virtual`, `重写/override`, `密封/sealed`, and `异步/async`, but each is accepted only where it has a defined meaning. EPLab reports unsupported combinations before generating C#. EPLab 1 explicitly rejects static classes, abstract methods, constructor modifiers, and `分部/partial` classes; an abstract class itself may still be declared.

#### Fields and global variables

```text
.程序集变量 name, type, visibility/modifiers, array-shape, initializer
.全局变量   name, type, visibility/modifiers, array-shape, initializer
```

Global variables are static. Quote a multidimensional shape so its commas stay in one directive field:

```e
.程序集变量 名字, 文本型, 公开, , “小明”
.程序集变量 棋盘, 整数型, 私有, “8, 8”
.全局变量 在线人数, 整数型, 内部, , 0
```

#### Methods, parameters, locals, and constructors

```text
.子程序 name, return-type, visibility, modifiers
.参数 name, type, modifiers, default-value
.局部变量 name, type, modifiers, array-shape, initializer
```

```e
.子程序 相加, 整数型, 公开, 静态
.参数 左, 整数型
.参数 右, 整数型, 可空
.局部变量 答案, 整数型

答案 ＝ 左 ＋ 右
返回（答案）
```

Parameter modifiers are `可空/optional`, `参考/ref` or `传址`, `输出/out`, `数组/array`, and `参数数组/params`. A `静态` local keeps its value between calls.

Parameter rules are checked before C# generation:

- `参考/ref` and `输出/out` cannot appear together, and neither may be optional or have a default;
- `参数数组/params` must be last and cannot be optional or have a default;
- a required parameter cannot follow an optional/defaulted parameter;
- EPLab's `可空` missing-value sentinel cannot also have an explicit default.

If a type is omitted, a field or local defaults to `整数型`, a parameter defaults to `通用型`, and a method result defaults to `无返回值`.

A constructor uses `.构造子程序`; its written name is descriptive because generated C# uses the containing class name:

```e
.构造子程序 创建, , 公开
.参数 初始值, 整数型

总分 ＝ 初始值
```

Constructors currently accept visibility only, not static, async, or other declaration modifiers.

#### Struct-like data type

```e
.数据类型 坐标, 公开
.成员 X, 小数型
.成员 Y, 小数型
```

#### Enum

```e
.枚举 方向, 公开
.枚举值 北, 0
.枚举值 东, 1
.枚举值 南, 2
.枚举值 西, 3
```

Use `#方向.北` to name an enum member.

#### Constant

```e
.常量 最大人数, 50, 公开
```

Special unqualified constants are `#换行符` and `#制表符`.

Simple literal constants infer a CLR type; other constant expressions use `dynamic`. A `.常量` is a static readonly field, not necessarily a CLR compile-time constant.

#### DLL import

```e
.DLL命令 取滴答数, 无符号整数型, “kernel32.dll”, “GetTickCount”, 公开
```

Following `.参数` lines become native parameters. This is deliberately a small `DllImport` surface; advanced marshaling is not yet part of the dialect.

### 6. Expressions

Literals:

```e
123
12.5
12.5F
123U
123L
0xFF
真
假
空
“普通文本”
$“分数：{分数}”
[2026年8月29日]
{1, 2, 3}
```

Postfix expressions:

```e
对象.成员
对象?.成员
子程序（第一个参数，名字: 第二个参数）
数组[1]
矩阵[2, 3]
```

Call arguments can be `参考 变量`, `传址 变量`, or `输出 变量`. An empty argument position emits `default`.

Operators, from stronger to weaker:

| Group | Spellings |
| --- | --- |
| Unary | `+`, `-`, `非`, `NOT`, `!`, `~` |
| Multiply | `*`, `×`, `/`, `÷` |
| Integer-style divide | `\` |
| Remainder | `模`, `MOD`, `%` |
| Add | `+`, `-` |
| Shift | `左移`, `右移`, `<<`, `>>` |
| Compare | `=`, `==`, `!=`, `<>`, `≠`, `<`, `<=`, `≤`, `>`, `>=`, `≥`, `≈`, `~=` |
| Bitwise | `位与`/`&`, then `位异或`/`^`, then `位或`/`|` |
| Boolean | `且`/`AND`/`&&`, then `或`/`OR`/`||` |
| Null coalesce | `??` |

Assignment is recognized only when `=` is at the top level of a statement:

```e
总分 ＝ 总分 ＋ 1
.如果（总分 ＝ 10）
```

In the first line `=` assigns; inside the parentheses it compares.

### 7. Statements and blocks

#### If

```e
.如果（分数 ≥ 10）
日志.信息（“赢了”）
.否则
日志.信息（“再试一次”）
.如果结束
```

`.如果真/.如果真结束` is the same without an `else` branch.

#### Count loop

```e
.计次循环首（3，次数）
日志.信息（到文本（次数））
.计次循环尾（）
```

`次数` receives 1, 2, then 3. The counter can be omitted.

#### Numeric range loop

```e
.变量循环首（0，10，2，数值）
日志.信息（到文本（数值））
.变量循环尾（）
```

The range is inclusive. If `数值` was declared, its numeric type is preserved for the start, end, step, and increment; if the counter is omitted, EPLab uses `double`. Positive and negative steps work, while a zero step throws an error instead of looping forever.

#### While and do/while

```e
.判断循环首（还要继续）
' body
.判断循环尾（）

.循环判断首（）
' body runs once before the test
.循环判断尾（还要继续）
```

#### Foreach extension

```e
.枚举循环首（玩家，玩家，Player.List）
日志.信息（玩家.Nickname）
.枚举循环尾（）
```

Fields are variable name, optional type, then collection. With two fields they are variable and collection.

#### Multi-branch choice

```e
.判断开始
.判断（分数 ≥ 100）
日志.信息（“金牌”）
.判断（分数 ≥ 50）
日志.信息（“银牌”）
.默认
日志.信息（“继续加油”）
.判断结束
```

#### Exceptions

```e
.尝试
可能失败（）
.捕获（错误，Exception）
日志.错误（错误.Message）
.最终
清理（）
.尝试结束
```

There may be several `.捕获` blocks. `.最终` is optional.

#### Safe helper blocks

```e
.临时设置（服务器.锁定，真）
' old value is restored in finally
.临时设置结束

.锁定（同步对象）
' one thread at a time
.锁定结束

.使用资源（流，创建对象（FileStream，路径，#FileMode.Open））
' stream is disposed at the end
.使用资源结束
```

#### Method-control pseudo-calls

| Source | Meaning |
| --- | --- |
| `返回（）` / `返回（值）` | `return` |
| `跳出循环（）` | `break` |
| `到循环尾（）` | `continue` |
| `抛出（异常）` | `throw` |
| `重定义数组（数组, 维数...）` | resize an `EArray<T>` |

### 8. Built-in helpers and aliases

| Source | Generated meaning |
| --- | --- |
| `创建对象（类型, 参数...）` | `new 类型(参数...)` |
| `转换类型（类型, 值）` | runtime-unchecked CLR cast |
| `安全转换类型（类型, 值）` | checked CLR numeric cast |
| `是否类型（值, 类型）` | CLR `is` test |
| `默认值（类型）` | `default(类型)` |
| `选择（条件, 真值, 假值）` | conditional expression |
| `字符（值）` | convert to one-character string |
| `到文本（值）` | invariant-culture text conversion |
| `取数组成员数（数组）` | `.Count` |
| `取文本长度（文本）` | `.Length` |
| `是否为空（值）` | missing optional or CLR null test |
| `到CLR数组（易数组）` | copy an `EArray<T>` to `T[]` |
| `从CLR数组（集合）` | copy an enumerable into `EArray<T>` |
| `&子程序名` | method-group/delegate expression |

Friendly names:

| EPLab | CLR |
| --- | --- |
| `日志` | `LabApi.Features.Console.Logger` |
| `配置` | the current generated LabAPI config object |
| `环境` | `System.Environment` |
| `.信息/.警告/.错误` | `.Info/.Warn/.Error` |
| `.数量/.长度` | `.Count/.Length` |
| `.加入/.删除/.清空` | `.Add/.Remove/.Clear` |

Other CLR names pass through unchanged.

### 9. Arrays

An array shape in a declaration creates an `EArray<T>`:

```e
.局部变量 一维, 整数型, , “5”
.局部变量 二维, 文本型, , “2, 3”
.局部变量 可变, 小数型, , “0”
```

- Valid `EArray<T>` indices start at 1.
- `二维[1, 1]` is the first cell.
- `二维[6]` is the same storage addressed as a flat array.
- `取数组成员数（二维）` returns the total cell count.
- `重定义数组（可变，10）` changes its size and preserves the prefix that fits.
- Only a one-dimensional array may grow with `.加入`.

Use `CLR数组<类型>` (or `CLR数组《类型》`) only when an external API really expects a normal zero-based CLR array. `到CLR数组（易数组）` copies to `T[]`; `从CLR数组（CLR数组）` copies back to a one-based `EArray<T>`. The explicit copy prevents an accidental off-by-one change.

### 10. LabAPI declarations

A project with LabAPI declarations needs `.扩展 LabAPI 1` and target `labapi-net48`.

```e
.LabAPI插件 主程序集
.插件名称 “My Plugin”
.插件说明 “What it does”
.插件作者 “Name”
.插件版本 “1.2.3”
.所需API版本 “1.1.0”
```

The optional second `.LabAPI插件` field names a custom config class. Leave it empty to generate config:

```e
.配置项 Enabled, 逻辑型, 真
.配置项 language, 文本型, “”, “language”, “Language”, “语言”
.配置项 Speed, 小数型, 1.0, “speed”, “Speed”, “速度”, 0.1, 10.0
```

The fields are name, type, default, serialized name, English description, Chinese description, optional minimum, and optional maximum. The serialized name becomes a YamlDotNet alias. One `Description` attribute is emitted, preferring Chinese when both descriptions are present. Numeric limits are clamped when the plugin enables; a value that cannot be converted, is NaN/infinite, or otherwise fails validation falls back to the default.

Subscribe to an event:

```e
.订阅事件 PlayerEvents.Joined, 玩家加入
```

EPLab subscribes in `Enable` and unsubscribes in `Disable`. The named method must exist and have the delegate-compatible CLR signature.

A lifecycle method is optional, but when present it must be the only matching lifecycle name in its group, be an instance method, take no parameters, and return `无返回值`.

Commands:

```text
.RA命令 name, alias1|alias2, description, handler, optional PlayerPermissions member
.玩家命令 name, aliases, description, handler, optional permission
.游戏控制台命令 name, aliases, description, handler, optional permission
```

```e
.RA命令 greet, hi|hello, Say hello, 处理问候, PlayersManagement

.子程序 处理问候, 无返回值, 公开
.参数 命令, 命令上下文

命令.回复 ＝ “你好！”
命令.成功 ＝ 真
```

A handler returning `逻辑型` supplies command success directly. Otherwise set `命令.成功` and `命令.回复`.
