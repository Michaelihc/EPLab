# ScriptedWarhead 的 EPLab 版

[English](README.en.md)

### 这是什么？

这是一个真的能编译成 LabAPI 插件 DLL 的易风格例子。把它想成一台三层小机器：

1. [脚本核弹.易](src/脚本核弹.易) 是“大脑”，决定什么时候启动 DMS、什么时候启动 Omega、命令怎么回答。
2. EPLab 把这个“大脑”翻译成你能查看的 C#。
3. [Bridge](Bridge) 是“小工具箱”，只负责 SCP:SL 很底层的门、灯、核弹、MEC 计时器、ffmpeg 和 SpeakerToy 操作。

时间线、配置、中文/英文选择、音频失败后改用 CASSIE，以及 `sw` 命令都没有藏在桥接层里。桥接层不是第二份插件。

### 最简单的编译方法

你需要：

- Windows；
- .NET 8 SDK；
- 已安装 SCP:SL Dedicated Server；
- 如果要播放 MP3，还要安装 `ffmpeg`。

双击 `build.cmd`。看到“编译完成”以后，打开 `bin\Release\package`：

- 把 `plugins\ScriptedWarheadEPLab.dll` 放进 `%APPDATA%\SCP Secret Laboratory\LabAPI\plugins\<端口>\`；
- 把 `dependencies\ScriptedWarhead.EPLab.Bridge.dll` 放进 `%APPDATA%\SCP Secret Laboratory\LabAPI\dependencies\<端口>\`。

构建脚本会先在唯一的临时文件夹里放入并检查这四个 DLL/PDB 文件，再完整替换 `package`。旧名字或多余 DLL 不会偷偷留在新包里；替换失败时会恢复上一份包。

也可以在 PowerShell 里运行：

```powershell
.\build.ps1
```

如果游戏服务器的 `Managed` 文件夹不在默认 Steam 位置：

```powershell
.\build.ps1 -ManagedDirectory "D:\你的服务器\SCPSL_Data\Managed"
```

### 它会做什么？

- 回合开始后第 19 分钟，调用游戏自己的 Deadman Switch。
- 它不会在第 19 分钟立刻启动 Omega；它会等原生 DMS 真的引爆。
- Omega 打开所有能找到的非电梯门，但完全不碰电梯门。
- 地图灯变成 RGB `(0, 200, 255)`。
- 自定义音频真正开始播放后，Omega 重新开始 182 秒倒计时。
- 音频不可用时会播放原生 CASSIE 备用消息。
- 到期时会调用原生核弹引爆（如果仍可用），然后杀死所有仍存活的玩家。
- 回合结束、重启或回到等待玩家时，会取消计时器、停止音频并恢复记录过的门和灯。

### RA 命令

命令需要 `WarheadEvents` 权限：

- `sw status`：查看时间线或剩余时间；
- `sw omega`：手动开始一条新的、以实际播放开始为准的 Omega 倒计时。

### 配置

LabAPI 第一次加载后会创建配置。最常用的项目是：

```yaml
is_enabled: true
language: ""
omega_audio_enabled: true
omega_audio_path: "D:\\Audio\\OmegaWarhead.mp3"
omega_audio_ffmpeg_path: "ffmpeg"
omega_audio_volume: 1
omega_audio_max_seconds: 240
omega_subtitle_delay_seconds: 3.19
omega_subtitle_duration_seconds: 6
omega_detonation_delay_seconds: 182
```

`language: "en"` 强制英文，`"cn"` 强制中文。空字符串通常表示“跟随客户端”；但 Omega 的 CASSIE 是全服一条消息，无法同时给不同玩家显示不同语言，所以这里安全地回退到中文。

`omega_audio_path` 必须是服务器上的绝对路径。留空、找不到文件、ffmpeg 失败或音频还没准备好都不会卡住时间线，而会使用 CASSIE 备用消息。

### 可能冲突的插件

- 也会改门、地图灯或核弹状态的插件；
- 占用全部 256 个 SpeakerToy controller ID 的音频插件；
- 也注册 `sw` 或 `scriptedwarhead` 的插件。

原生电梯门永远不修改。回合重置只恢复本例亲自记录过的非电梯门和灯，但另一个插件在 Omega 期间改同一对象时，最后恢复者仍可能覆盖对方。

### 去哪里看“翻译结果”？

成功编译后，打开：

`obj\eplab\Release\EPLab.All.Generated.cs.txt`

它就是 EPLab 生成的可阅读 C#。错误也会通过 `#line` 指回 `.易` 文件，而不是只告诉你生成文件坏了。

### 证据与限制

本移植对照了另行维护的原版 ScriptedWarhead 实现，重点核对了回合时间线、原生 Omega 音频服务和 RA 命令组件；原版源码不属于这个独立 EPLab 包。官方事件签名来自 LabAPI 上游公开的 [ServerEvents 事件源码](https://github.com/northwood-studios/LabAPI/blob/84b0da472e3ce42bf86cb49bde8379d00605a8f6/LabApi/Events/Handlers/ServerEvents.EventHandlers.cs) 与 [WarheadEvents 事件源码](https://github.com/northwood-studios/LabAPI/blob/84b0da472e3ce42bf86cb49bde8379d00605a8f6/LabApi/Events/Handlers/WarheadEvents.EventHandlers.cs)。

编译验证证明了完整的 `.易 -> 生成的 C# -> net48 DLL` 路径，也会用已安装的游戏与 LabAPI 程序集做类型检查。它没有证明多人音频传输或回合内设施行为；这些仍需要可见的本地服务器和真实客户端。详情见 [VALIDATION.md](VALIDATION.md)。
