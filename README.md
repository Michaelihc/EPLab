# EPLab

[English](README.en.md)

> **非官方、独立实现。** EPLab 是受易语言启发、用于编写服务器端 LabAPI 插件的源码兼容方言。它不是官方易语言产品、编译器、运行库或文件格式，也不隶属于其发布方。

EPLab 让你用亲切的中文源代码写服务器端 LabAPI 插件。它先把代码变成可查看的 C#，再编译成普通的 SCP:SL 插件 DLL。

```text
UTF-8 的 .易 / .eplab 源代码
              ↓
分词 → 解析 → 检查过的程序模型
              ↓
可阅读、可映射回原行的 C#
              ↓
dotnet build → .NET Framework 4.8 LabAPI DLL
```

C# 这一步是特意保留的：你能看懂编译器做了什么，普通 C# 编译器也能检查 CLR 和 LabAPI 调用。图形界面里有 **查看生成的 C#** 按钮；出现错误时，位置会指回原来的 `.易` 行。

### 需要准备什么

- 安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。
- 小图形界面使用 Windows WinForms；编译器和命令行项目本身使用 .NET 8。
- 要制作 LabAPI DLL，需要安装 SCP:SL 专用服务器，让 EPLab 能引用 `SCPSL_Data/Managed` 里的程序集。可以阅读 [LabAPI 官方项目指南](https://github.com/northwood-studios/LabAPI/wiki/Creating-The-Project)。

**不需要**安装官方易语言编译器。EPLab 不会偷偷调用官方编译器或运行库。

### 编译并打开图形界面

最简单的方法：在资源管理器中双击 `run-gui.cmd`。它会编译需要的内容并打开图形界面。

如果你更喜欢 PowerShell，也可以在 `EPLab` 文件夹里运行：

```powershell
dotnet build EPLab.sln -c Release
dotnet run --project src/EPLab.Gui -c Release
```

图形界面故意做得很小：

1. **新建**：在空文件夹里制作一个“你好”项目。
2. **打开**：打开 `project.eplabproj`。
3. 修改 `.易` 或 `.eplab` 文件，再按 **保存**。
4. **检查**：解析代码，并请 C# 编译器检查类型和调用。
5. **编译 DLL**：制作插件。
6. **查看生成的 C#**：看看 EPLab 到底生成了什么。
7. **项目设置**：默认路径不对时，用选择框找到游戏 Managed 和 LabAPI 全局依赖文件夹。

双击“问题”中的一项，可以跳到出错的源代码行。语言选择框能在中文和 English 之间切换界面及诊断信息。

### 命令行

```powershell
# 制作入门项目
dotnet run --project src/EPLab.Cli -c Release -- new MyFirstPlugin

# 检查；--cn 表示使用中文消息
dotnet run --project src/EPLab.Cli -c Release -- check MyFirstPlugin/project.eplabproj --cn

# 编译 DLL
dotnet run --project src/EPLab.Cli -c Release -- build MyFirstPlugin/project.eplabproj --cn
```

如果 SCP:SL 安装在不常见的位置，可以用图形界面的 **项目设置**，或在命令行加上：

```text
--managed "D:\你的路径\SCPSL_Data\Managed"
--dependencies "D:\你的路径\LabAPI\dependencies\global"
```

`check` 会真的做类型检查，不只是找错别字，所以 LabAPI 项目仍然需要所引用的游戏程序集。

### 一个很小的源文件

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

源文件是普通 UTF-8 文本。这里的 `.易` 是文本扩展名，**不是**官方二进制 `.e` 项目。半角和全角标点都可以使用。

### 生成的文件在哪里

- 生成的 C#：`obj/eplab/Release/EPLab.All.Generated.cs.txt`
- 引用哈希和构建信息：`obj/eplab/Release/build-info.json`
- 编译好的 DLL：`bin/Release/<项目名称>.dll`

EPLab 不会悄悄部署插件，也不会重启服务器。准备好以后，才把 DLL 复制到正确端口的 LabAPI 插件文件夹，再用正常方式重启那台服务器。

### 示例与验证

- [`examples/HelloLabApi`](examples/HelloLabApi) 是最小的真实 LabAPI 插件。
- [`examples/ScriptedWarhead`](examples/ScriptedWarhead/README.md) 把 DMS/Omega 时间线、配置选择、语言/备用流程和 RA 命令留在 `.易`；窄桥接只处理 MEC、原生设施/核弹/玩家、ffmpeg 和 SpeakerToy 操作。
- [`examples/SpinBot`](examples/SpinBot/README.zh-CN.md) 把配置、生命周期、旋转/状态数学、持久授权、ServerKeybinds API 3 界面和 RA 命令留在 `.易`；Cement 的引擎侧 C# 源码有意保持私有，不包含在这个公开仓库中。

后两个示例都是“易源码 → C# → net48”的编译/结构验证，目前不宣称已经完成多人游戏内验证，也不假装能用纯易代码表达 SCP:SL 内部机制，更不代表所有官方易语言程序都能不修改直接编译。请阅读 [ScriptedWarhead 验证记录](examples/ScriptedWarhead/VALIDATION.md) 和 [SpinBot 验证记录](examples/SpinBot/VALIDATION.zh-CN.md) 了解准确边界。

验证记录目前都通过：ScriptedWarhead 可以只用本仓库内容复现；SpinBot 的完整记录则使用了 Cement 的私有原生边缘源码。该维护者工作区中的 SpinBot 通过了生成 DLL 的 17/17 数学检查和 2 项插件/命令反射检查。公开的 SpinBot 文件夹仍完整保留并讲解 `.易` 一侧，但缺少这些私有文件时不能生成原生桥接 DLL。

### 下一步读什么

- [写给 10 岁小朋友的指南](docs/KIDS-GUIDE.md)
- [语言参考](docs/LANGUAGE.md)
- [准确的兼容范围与有意差异](docs/COMPATIBILITY.md)
- [第三方参考资料与声明](THIRD-PARTY-NOTICES.md)
- [MIT 许可证](LICENSE)

双击 `build.cmd` 可以编译整个工具并运行 44 个编译器一致性测试。对应的 PowerShell 测试命令是：

```powershell
dotnet run --project tests/EPLab.Compiler.Tests -c Release
```
