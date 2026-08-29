# 你的第一个 EPLab 插件

[English](KIDS-GUIDE.en.md)

### 我们要做什么？

把游戏服务器想成一个小俱乐部。**插件**就是住在俱乐部里、照你的规则做事的小机器人。我们的第一个机器人会发现玩家进入，然后在服务器日志中写一句“你好”。

你用亲切的中文写规则，EPLab 当翻译员：

```text
你的 .易 故事 → EPLab 理解 → 看得见的 C# 指令 → 插件 DLL
```

不需要官方易语言编译器。EPLab 不会把它藏在后面偷偷调用。

### 开始前

把 DLL 复制进服务器以前，请先问服务器主人或大人。本地练习插件写错时，一般只是插件加载失败，但仍然应该在本地测试服务器上练习，不要先放到很忙的公开服务器。

你需要：

1. Windows 电脑，用来打开小图形界面。
2. [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。
3. 安装 SCP:SL 专用服务器，取得 LabAPI 和游戏引用文件。
4. 这个 `EPLab` 文件夹。

### 第 1 步：启动 EPLab

最简单的方法是在资源管理器里打开 `EPLab` 文件夹，然后双击 `run-gui.cmd`。等一会儿，小窗口就会打开。

如果你想学习 PowerShell，也可以在 `EPLab` 文件夹里打开它，再一次输入一条：

```powershell
dotnet build EPLab.sln -c Release
dotnet run --project src/EPLab.Gui -c Release
```

第一条制作工具，第二条打开工具。

想一次检查整个工具时，可以双击 `build.cmd`；它会编译所有项目，再运行 44 个编译器测试。窗口最后会停住，让你看清结果。

如果 PowerShell 说不认识 `dotnet`，请安装 **SDK**（不能只装 runtime），关掉 PowerShell，再重新打开。

### 第 2 步：新建项目

1. 按 **新建**。
2. 选择一个新的空文件夹，例如 `文档\MyHelloPlugin`。
3. EPLab 会打开 `src\你好.易`。

文件夹必须是空的，这样 EPLab 就不会不小心盖掉作业或别的项目。

### 第 3 步：读懂小程序

入门程序是这样的：

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

重要的行是这个意思：

- `.版本 2`：这份文件使用第 2 版 EPLab 语言。
- `.扩展 CLR 1`：允许程序使用普通 .NET 类型。
- `.扩展 LabAPI 1`：打开插件辅助功能。
- `.LabAPI插件 主程序集`：告诉编译器，`主程序集` 是机器人的主类。
- `.订阅事件 PlayerEvents.Joined, 玩家加入`：“有人加入时，运行 `玩家加入`。”
- `.参数 事件, PlayerJoinedEventArgs`：给子程序一个装着加入信息的盒子。
- 最后一行让 LabAPI 日志打印玩家昵称。

文本里的 `{...}` 是一个插槽。玩家昵称是 `小米` 时，服务器就写 `你好，小米！`。

### 第 4 步：改一个地方

只改最后一行：

```e
日志.信息（$“欢迎来到服务器，{事件.Player.Nickname}！”）
```

按 **保存**。

### 第 5 步：编译前先检查

按 **检查**。

可以把“检查”想成错别字检查器加积木拼合检查器。它先检查易风格语法，然后生成临时 C#，再请真正的 C# 编译器检查每块 LabAPI 积木能不能拼上。

- 状态显示成功，就继续。
- 出现问题时，双击它，EPLab 会跳到对应行。
- 修好一个，保存，再按一次 **检查**。

想看翻译结果，可以按 **查看生成的 C#**。现在看不懂全部也没关系，重要的是 EPLab 没有把它藏起来。

### 第 6 步：制作 DLL

按 **编译 DLL**。完成的文件一般在：

```text
MyHelloPlugin\bin\Release\HelloEPLab.dll
```

项目名称决定 DLL 名称。状态栏和输出区也会显示准确路径。

如果 EPLab 说找不到 `LabApi.dll` 或其他游戏文件，专用服务器可能装在别处。按 **项目设置**，选择它的 `SCPSL_Data\Managed` 文件夹和 LabAPI `dependencies\global` 文件夹，再保存。也可以照主 README 使用命令行 `--managed` 和 `--dependencies`。

### 第 7 步：安全试一试

先停下本地测试服务器：

1. 找到正确服务器端口的 LabAPI 插件文件夹：

   ```text
   %APPDATA%\SCP Secret Laboratory\LabAPI\plugins\<端口>\
   ```

2. 只把完成的 DLL 复制进去。
3. 用正常方式启动或重启那台本地服务器。
4. 加入服务器，查看服务器日志。
5. 应该能看到欢迎语。

不要猜生产服务器路径，不要复制网上来历不明的 DLL，也不要没得到允许就重启别人的服务器。

### 出问题怎么办

按这个小清单检查：

1. **EPLab 里有红色问题？** 双击它，读对应源代码行。
2. **构建时缺 DLL？** 检查 Managed 和 dependencies 路径。
3. **LabAPI 列表里没有插件？** 确认 DLL 放进了你真正启动的那个端口文件夹。
4. **插件抛出错误？** 保存完整错误和前面几行日志；它们是线索，不是垃圾。
5. **什么也没打印？** 确认插件启用后真的有玩家重新加入。

每次只改一个小地方，每次修改后按“检查”。有经验的程序员也是这样做大项目，才不会迷路。

### 三个有趣的小实验

1. 换一句欢迎语。
2. 在昵称后加玩家 User ID：`{事件.Player.UserId}`。
3. 打开 `examples/ScriptedWarhead` 或 `examples/SpinBot`，各找出一个循环、一个“如果”和一个自定义类型。

完整语言“字典”在 [LANGUAGE.md](LANGUAGE.md)。和官方易语言的诚实差异在 [COMPATIBILITY.md](COMPATIBILITY.md)。
