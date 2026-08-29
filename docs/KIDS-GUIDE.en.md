# Your first EPLab plugin

[中文](KIDS-GUIDE.md)

### What are we making?

Imagine that a game server is a clubhouse. A **plugin** is a small robot that lives in the clubhouse and follows your rules. Our first robot will notice when a player enters and write “hello” in the server log.

You write the rules in friendly Chinese. EPLab is the translator:

```text
your .易 story → EPLab understands it → visible C# instructions → plugin DLL
```

You do not need the official 易语言 compiler. EPLab does not hide it or call it.

### Before you start

Ask the server owner or an adult before copying a DLL into a server. A broken practice plugin usually only makes the plugin fail to load, but you should still test on a local server, not a busy public one.

You need:

1. A Windows computer for the little GUI.
2. The [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
3. SCP:SL Dedicated Server installed for the LabAPI and game reference files.
4. This `EPLab` folder.

### Step 1: start EPLab

The easiest way is to open the `EPLab` folder in File Explorer and double-click `run-gui.cmd`. Wait a moment and the small window will open.

If you want to learn PowerShell, open it in the `EPLab` folder and type these two commands, one at a time:

```powershell
dotnet build EPLab.sln -c Release
dotnet run --project src/EPLab.Gui -c Release
```

The first command builds the tool. The second opens it.

To check the whole tool at once, double-click `build.cmd`. It builds every project, runs 44 compiler tests, and leaves the window open so you can read the result.

If PowerShell says that `dotnet` is unknown, install the **SDK** (not only the runtime), close PowerShell, and open it again.

### Step 2: make a project

1. Press **New**.
2. Pick a new empty folder, perhaps `Documents\MyHelloPlugin`.
3. EPLab opens a file named `src\你好.易`.

The folder must be empty so EPLab cannot overwrite homework or another project by accident.

### Step 3: read the tiny program

The starter looks like this:

```e
.版本 2
.扩展 CLR 1
.扩展 LabAPI 1

.引用命名空间 LabApi.Events.Arguments.PlayerEvents
.引用命名空间 LabApi.Events.Handlers

.LabAPI插件 主程序集
.插件名称 “你好，EPLab！”
.插件说明 “玩家加入时，在服务器日志里说你好。”
.插件作者 “Your Name”
.插件版本 “1.0.0”
.订阅事件 PlayerEvents.Joined, 玩家加入

.程序集 主程序集, , 公开

.子程序 玩家加入, , 公开
.参数 事件, PlayerJoinedEventArgs

日志.信息（$“你好，{事件.Player.Nickname}！”）
```

Here is what the important lines say:

- `.版本 2` says which EPLab language version this file uses.
- `.扩展 CLR 1` lets the program use normal .NET types.
- `.扩展 LabAPI 1` turns on the plugin helpers.
- `.LabAPI插件 主程序集` says that `主程序集` is the robot's main class.
- `.订阅事件 PlayerEvents.Joined, 玩家加入` says: “When somebody joins, run `玩家加入`.”
- `.参数 事件, PlayerJoinedEventArgs` gives that method a box containing information about the join.
- The last line asks the LabAPI logger to print the player's nickname.

The `{...}` part is a slot in the text. If the nickname is `Mia`, the server writes `你好，Mia！`.

### Step 4: make one change

Change only the last line:

```e
日志.信息（$“欢迎来到服务器，{事件.Player.Nickname}！”）
```

Press **Save**.

### Step 5: check before building

Press **Check**.

Think of Check as a spelling checker plus a LEGO-fit checker. It checks the 易-style grammar, then makes temporary C# and asks the real C# compiler whether every LabAPI piece fits.

- If the status says success, continue.
- If a problem appears, double-click it. EPLab jumps to the line.
- Fix one problem, save, and press **Check** again.

Want to see the translation? Press **View generated C#**. It is okay if you do not understand all of it yet. The important part is that EPLab does not hide it.

### Step 6: build the DLL

Press **Build DLL**. The finished file normally appears here:

```text
MyHelloPlugin\bin\Release\HelloEPLab.dll
```

Your project name controls the DLL name. The exact path is also shown in the status/output area.

If EPLab says it cannot find `LabApi.dll` or another game file, the dedicated server is probably installed in a different place. Press **Project settings**, browse to its `SCPSL_Data\Managed` folder and the LabAPI `dependencies\global` folder, then save. You can also use command-line `--managed` and `--dependencies` as shown in the main README.

### Step 7: try it safely

With the local test server stopped:

1. Find the LabAPI plugins folder for the correct server port:

   ```text
   %APPDATA%\SCP Secret Laboratory\LabAPI\plugins\<port>\
   ```

2. Copy only your finished DLL there.
3. Start or restart that local server normally.
4. Join it and look at the server log.
5. You should see your welcome line.

Do not guess a production-server folder, copy random DLLs, or restart somebody else's server without permission.

### When something goes wrong

Try this short checklist:

1. **Red problem in EPLab?** Double-click it and read the source line.
2. **Missing DLL while building?** Check the Managed/dependencies paths.
3. **Plugin not listed by LabAPI?** Make sure the built DLL is in the folder for the same port you started.
4. **Plugin throws an error?** Save the whole error and the few log lines before it; they are clues, not noise.
5. **Nothing is printed?** Confirm a player really joined after the plugin was enabled.

Change one small thing at a time. Press Check after each change. That is how experienced programmers make big things without getting lost.

### Three fun next experiments

1. Change the welcome sentence.
2. Add the player's user ID after the nickname: `{事件.Player.UserId}`.
3. Open `examples/ScriptedWarhead` or `examples/SpinBot` and find a loop, an `if`, and a custom type.

For the complete “dictionary” of the language, read [LANGUAGE.en.md](LANGUAGE.en.md). For honest differences from official 易语言, read [COMPATIBILITY.en.md](COMPATIBILITY.en.md).
