# SpinBot EPLab 验证

[英文版](VALIDATION.md) · [返回示例说明](README.zh-CN.md) · [私有原生源码边界](bridge/ReferenceNative/PROVENANCE.zh-CN.md)

验证日期为 2026-08-29。验证在 Cement 的维护者工作区完成，使用本机安装的 SCP:SL 专用服务器托管程序集、另行提供的 `ServerKeybinds.dll` API 3 版本，以及七个不属于公开仓库的私有原生边缘源码文件。

## 自动验证结果

运行：

```powershell
.\build.ps1 -HarmonyPath 'C:\你的路径\0Harmony.dll'
```

实际结果：

```text
Windows PowerShell 5.1：退出代码 0
SpinBot.EPLab.Bridge：编译成功，0 个警告，0 个错误
EPLabSpinBot.generated：编译成功，0 个警告，0 个错误
SpinBot EPLab 验证通过：数学检查 17 项，反射类型 2 个
```

最终验证使用 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -HarmonyPath C:\你的路径\0Harmony.dll`。安装包报告使用经过目录包含检查、兼容 Windows PowerShell 5.1 的辅助函数，不再调用较新的 `Path.GetRelativePath` API。

输出文件：

```text
bin\Release\EPLabSpinBot.dll
bridge\bin\Release\SpinBot.EPLab.Bridge.dll
bin\Release\package\plugins\EPLabSpinBot.dll
bin\Release\package\dependencies\global\SpinBot.EPLab.Bridge.dll
obj\eplab\Release\EPLab.All.Generated.cs.txt
```

本次验证编译发布的准确产物：

| 包内路径 | 字节数 | SHA-256 |
| --- | ---: | --- |
| `plugins/EPLabSpinBot.dll` | 50,176 | `509BF25BC74E09EC6C5B9113607DDF57200A34658D509028EC4048C41ED104F4` |
| `dependencies/global/SpinBot.EPLab.Bridge.dll` | 58,368 | `57C867353AFF5C3BE8A4BA188DE8C2F0EDA670CD4A17F5227331D7C460DA09E8` |

测试程序会从临时安装包、真实游戏托管目录和 LabAPI 全局依赖目录解析依赖。它加载临时包里的 `EPLabSpinBot.dll`，调用 `EPLabSpinBot.旋转数学验证.运行`，并反射 `EPLabSpinBot.__EplabPlugin` 与 `EPLabSpinBot.__EplabCommand0`。它会检查生成的 LabAPI 插件基类、公开构造器、`CommandSystem.ICommand` 契约和公开命令名称。因此 17 项断言执行的是从 `.易` 生成的方法；测试程序没有再复制一份公式。

维护者编译脚本先检查 `bridge/ReferenceNative` 下七个私有文件的固定哈希，再直接在 `bin\Release` 下创建唯一的临时目录并对它运行冒烟检查。只有通过检查的临时包才会替换准确的 `bin\Release\package` 目录。本仓库不会发布这个本地验证包或私有源码。

## 独立路径与私有源码边界

`build.ps1` 从 `../..` 推导 EPLab 根目录，并把 CLI 解析为 `src/EPLab.Cli/EPLab.Cli.csproj`。脚本中没有指向 EPLab 外部的路径，也没有个人下载目录回退。编译器路径可以在独立克隆中使用；`ServerKeybinds.dll`、游戏程序集和 Cement 的私有原生边缘源码只是本地编译输入。

依照 Cement 的要求，Git 会忽略七个原生实现文件，公开仓库也不包含它们或编译后的桥接包。公开克隆可以阅读完整的易侧移植和本验证证据，但没有这些私有文件就不能复现完整原生编译。详见[来源页面](bridge/ReferenceNative/PROVENANCE.zh-CN.md)。

## 移植边界

| 行为 | 所在位置 | 验证方式 |
| --- | --- | --- |
| 插件元数据、配置、启停和 LabAPI 事件 | `src/插件与配置.易` | 生成后进行 net48 编译 |
| 平滑、反向、抖动、随机姿态和射速数学 | `src/模型与旋转数学.易` | 对产出的 DLL 执行 17 项检查 |
| 玩家/假人状态、连续性、角色重绑和确定性种子 | `src/旋转状态.易` | 生成后编译，并检查桥接契约 |
| 持久授权以及 API 3 分类、区块、可见性和设置 | `src/玩家权限与设置.易` | 使用真实 `ServerKeybinds.dll` 编译 |
| 完整 RA 命令和本地化回复 | `src/管理命令.易` | 生成命令适配器并编译 |
| 仅观察者 FPC 同步替换 | 私有维护者源码（不公开） | 固定哈希及桥接层 net48 编译 |
| 原生命中判定、伤害预测、强制开火和开火修正 | 私有维护者源码（不公开） | 固定哈希及桥接层 net48 编译 |
| RA 假人生成、演示、移动和武器 | 私有维护者源码（不公开） | 固定哈希及桥接层 net48 编译 |

## 行为参考与作者归属

私有验证输入对应的组件名称和哈希记录在[来源页面](bridge/ReferenceNative/PROVENANCE.zh-CN.md)中。移植时对照了以下参考组件：

- `SpinBot.Services.SpinStateService`
- `SpinBot.Services.ServerSettingsService`
- `SpinBot.Services.SpinPoseMath`
- `SpinBot.Services.AutoFireRateMath`
- `SpinBot.Commands.SpinBotCommand`
- `SpinBot.Patches.VisualRotationPatch`
- `SpinBot.Patches.CorrectOnFirePatch`
- `SpinBot.Patches.DummyRevolverPatches`
- `SpinBot.Services.AutoAimFireService`
- `SpinBot.Services.DummySpinService`
- `SpinBot.Tests.Program`
- `ServerKeybinds.KeybindRegistry`（`ApiVersion => 3`）
- `ServerKeybinds.KeybindBlock`
- LabAPI 上游公开的 [`Plugin<TConfig>` 源码](https://github.com/northwood-studios/LabAPI/blob/84b0da472e3ce42bf86cb49bde8379d00605a8f6/LabApi/Loader/Features/Plugins/Plugin%7BTConfig%7D.cs)

## 没有声称完成的项目

本记录只覆盖编译、对产出 DLL 的数学验证和依赖解析后的类型加载；它**没有**实例化插件或调用 Unity/游戏服务，也不声称已经完成游戏内手动运行、实时视觉确认、目标命中判定确认或服务器部署。这些项目需要可见的本地测试服务器、游戏客户端、RA 权限、获授权的真实玩家和敌对假人。没有进行生产环境部署。

手动 QA 应检查：授权持久化、未授权玩家看不到设置、四种观察姿态、换角色重绑、两种开火模式、障碍物拒绝、假人演示清理、回合重启、插件停用，以及 Harmony 冲突时安全停用。
