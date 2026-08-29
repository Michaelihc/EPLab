# ScriptedWarhead 验证

[English](VALIDATION.en.md)

### 这个例子验证了什么？

它不是一个只会打印“你好”的玩具。它让 EPLab 处理一个真实插件需要的 Unicode 标识符、配置、五个 LabAPI 事件、RA 命令、权限、委托回调、`out` 参数、分支、循环之外的异步时间线，以及外部 net48 程序集调用。

| 原版行为合同 | 易源码中的位置 | 桥接层只做什么 |
| --- | --- | --- |
| 19:00 启动原生 DMS | `到达DMS里程碑` | 调用 `DeadmanSwitch.InitiateProtocol()` |
| 等待真正的 DMS 引爆 | `原生核弹已引爆` | 读取原生 `Warhead.ScenarioType` |
| 门、灯和电梯边界 | `启动欧米伽` 决定何时应用 | 枚举包装器、保存并恢复原生状态 |
| 播放开始后重新锚定 182 秒 | `音频开始`、`安排欧米伽引爆` | MEC 计时器和实时钟 |
| 音频失败改用 CASSIE | `启动欧米伽`、`音频失败` | ffmpeg、线程切回主线程、SpeakerToy 包 |
| 到期杀死所有幸存者 | `引爆欧米伽` | 原生 Warhead/Player 调用和 dummy 防护 |
| `sw omega/status` 与回答 | `处理命令`、`取得状态` | 大小写无关文本比较 |

### 自动验证记录

2026-08-29 在本仓库当前安装的 SCP:SL Dedicated Server 程序集上完成：

1. `dotnet build Bridge\ScriptedWarhead.EPLab.Bridge.csproj -c Release`：成功，0 警告，0 错误。
2. `eplab build project.eplabproj --cn`：成功，0 警告，0 错误。
3. 生成 `ScriptedWarheadEPLab.dll`，目标为 `net48`；生成 C# 保留指向 [脚本核弹.易](src/脚本核弹.易) 的 `#line` 映射。
4. `build.ps1`：成功创建分开的 `plugins` 与 `dependencies` 部署包；临时包的文件清单、SHA-256 和程序集名称均通过检查。
5. `validate.ps1` 会先放入一个旧名字 DLL 哨兵；成功构建后哨兵消失，证明脚本替换了整个精确包而不是覆盖几个同名文件。

运行 `validate.ps1` 可以重复上述检查，并确认易源码里仍有关键合同标记，防止以后“不小心简化没了”。

### 还没有自动证明什么？

- 没有在本次验证里启动可见本地服务器，所以没有声称门、灯、DMS、死亡和回合恢复已做游戏内测试。
- 没有真实客户端，因此没有声称 MP3 网络包、0.5 秒 SpeakerToy 传播等待和 3.19 秒字幕同步已做多人测试。
- EPLab 目前把生成配置对象通过一个可空宿主槽交给易代码；桥接层在启动时读取一次配置值，避免每个时间线方法都依赖动态对象。这是配置管道适配，不是隐藏游戏规则。

手动测试时应使用可见的本地测试服务器，记录端口、配置、预期/实际结果和完整日志；不要把这份编译验证当成生产部署授权。

### 为了更好用而有意做的差异

- 插件名是 `ScriptedWarheadEPLab`，这样验证时不会和原版 `ScriptedWarhead` 撞名；不要同时让两者控制同一回合。
- `omega_audio_path` 默认留空，不会把某一台生产 Linux 服务器的私人路径送给所有新手。路径无效时仍按原合同安全回退到 CASSIE。
- `language` 默认 `""`，遵守仓库的本地化约定；全服 CASSIE 不能按玩家分别显示，因此空值回退中文。RA 回答一次只显示选中的一种语言，不再把中英两句挤在一起。
- 原版仅在 `DEBUG` 构建出现的 `sw test ...` 开发命令没有作为用户命令移植。发布行为 `sw omega`、`sw status` 和 `WarheadEvents` 权限保持不变。
