# EPLab

> **Unofficial and independent.** EPLab is an 易语言-inspired, source-compatible dialect for writing server-side LabAPI plugins. It is not the official 易语言 product, compiler, runtime, or file format, and it is not affiliated with its publisher.

EPLab lets you write friendly Chinese source code, inspect what it means, and build a normal SCP:SL LabAPI plugin DLL.

```text
UTF-8 .易 / .eplab source
          ↓
lexer → parser → checked program model
          ↓
readable, source-mapped C#
          ↓
dotnet build → .NET Framework 4.8 LabAPI DLL
```

The C# step is intentional. It makes the result understandable and lets the ordinary C# compiler check CLR and LabAPI calls. The GUI has a **View generated C#** button, and compiler errors point back to the original `.易` line.

### What you need

- The [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
- Windows for the small WinForms GUI. The compiler and command-line projects themselves target .NET 8.
- For a LabAPI DLL, an installed SCP:SL Dedicated Server so EPLab can reference its `SCPSL_Data/Managed` assemblies. See the [official LabAPI project guide](https://github.com/northwood-studios/LabAPI/wiki/Creating-The-Project).

The official 易语言 compiler is **not** needed. EPLab does not load its runtime or call it behind the scenes.

### Build and open the GUI

The easiest route is to double-click `run-gui.cmd` in File Explorer. It builds what it needs and opens the GUI.

If you prefer PowerShell, run these commands inside the `EPLab` folder:

```powershell
dotnet build EPLab.sln -c Release
dotnet run --project src/EPLab.Gui -c Release
```

The GUI deliberately stays small:

1. **New** makes a tiny hello project in an empty folder.
2. **Open** opens `project.eplabproj`.
3. Edit a `.易` or `.eplab` file and press **Save**.
4. **Check** parses the source and asks the C# compiler to type-check it.
5. **Build DLL** creates the plugin.
6. **View generated C#** shows exactly what EPLab produced.
7. **Project settings** lets you browse for the game Managed and LabAPI global-dependencies folders when the defaults are wrong.

Double-click a problem to jump to its source line. The language selector changes the GUI and diagnostics between Chinese and English.

### Command line

```powershell
# Make a starter project
dotnet run --project src/EPLab.Cli -c Release -- new MyFirstPlugin

# Check it; --cn selects Chinese messages
dotnet run --project src/EPLab.Cli -c Release -- check MyFirstPlugin/project.eplabproj --cn

# Build its DLL
dotnet run --project src/EPLab.Cli -c Release -- build MyFirstPlugin/project.eplabproj --cn
```

If SCP:SL is installed somewhere unusual, use **Project settings** in the GUI or add:

```text
--managed "D:\path\to\SCPSL_Data\Managed"
--dependencies "D:\path\to\LabAPI\dependencies\global"
```

`check` is a real type-check, not just a spelling check, so a LabAPI project still needs its referenced game assemblies.

### A tiny source file

```e
.版本 2
.扩展 CLR 1
.扩展 LabAPI 1

.引用命名空间 LabApi.Events.Arguments.PlayerEvents
.引用命名空间 LabApi.Events.Handlers

.LabAPI插件 主程序集
.插件名称 “你好，EPLab！”
.订阅事件 PlayerEvents.Joined, 玩家加入

.程序集 主程序集, , 公开

.子程序 玩家加入, , 公开
.参数 事件, PlayerJoinedEventArgs

日志.信息（$“你好，{事件.Player.Nickname}！”）
```

Source files are plain UTF-8 text. `.易` here is a text extension; it is **not** the official binary `.e` project format. Straight and full-width punctuation are both welcome.

### Where files go

- Generated C#: `obj/eplab/Release/EPLab.All.Generated.cs.txt`
- Reference hashes and build facts: `obj/eplab/Release/build-info.json`
- Built DLL: `bin/Release/<project-name>.dll`

EPLab does not silently deploy or restart a server. Copy a finished DLL to the LabAPI plugin folder for the correct server port only when you are ready, then restart that server normally.

### Examples and build boundaries

- [`examples/HelloLabApi`](examples/HelloLabApi) is the smallest real LabAPI plugin.
- [`examples/ScriptedWarhead`](examples/ScriptedWarhead/README.md) keeps the DMS/Omega timeline, config choices, localization/fallback, and RA command flow in `.易`. Its narrow bridge handles only MEC/native facility, warhead, player, ffmpeg, and SpeakerToy operations.
- [`examples/SpinBot`](examples/SpinBot/README.md) keeps configuration, lifecycle, rotation/state math, persistent access, ServerKeybinds API 3 UI, and RA commands in `.易`. Cement's engine-facing C# source is intentionally private and is not included in this public repository.

ScriptedWarhead is buildable from this repository with the required server assemblies. SpinBot preserves the complete `.易` side, but its native bridge also requires Cement's seven private source files. See the [ScriptedWarhead verification guide](examples/ScriptedWarhead/VALIDATION.md) and [SpinBot verification guide](examples/SpinBot/VALIDATION.md) for the checks and game-client coverage each example needs.

### Read next

- [A guide for a 10-year-old](docs/KIDS-GUIDE.md)
- [Language reference](docs/LANGUAGE.md)
- [Exact compatibility and intentional differences](docs/COMPATIBILITY.md)
- [Third-party references and notices](THIRD-PARTY-NOTICES.md)
- [MIT license](LICENSE)

Double-click `build.cmd` to build the complete tool and run its compiler conformance checks. The equivalent PowerShell test command is:

```powershell
dotnet run --project tests/EPLab.Compiler.Tests -c Release
```
