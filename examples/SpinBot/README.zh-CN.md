# 用 EPLab 编写的 SpinBot

[英文版](README.md) · [验证记录](VALIDATION.zh-CN.md) · [私有原生源码边界](bridge/ReferenceNative/PROVENANCE.zh-CN.md)

这是一个真实的 LabAPI 移植。平时要读、要改的逻辑使用亲切的易风格源码编写。EPLab 会把 `.易` 翻译成可以阅读的 C#，再由 .NET 编译器制作 `net48` DLL。Cement 选择让七个接触游戏引擎的 C# 实现文件保持私有，所以公开克隆包含完整的易侧源码和验证记录，但不能独自编译原生桥接层。

可以把它想成两个盒子组成的玩具：

1. **大号易源码盒子**决定 SpinBot 应该做什么。
2. **小号 C# 桥接盒子**接触那些不稳定、也不是公开 LabAPI 功能的 SCP:SL 引擎内部。

桥接层故意做得很窄。配置、启动和停止、旋转与射速数学、玩家状态、持久权限、ServerKeybinds API 3 设置、本地化以及 RA 命令行为都在 `.易` 中。只有 Harmony 观察者同步补丁、原生武器/命中判定请求、原生 RA 假人机制保留在 C# 中。

## 编译方法

你需要 Windows、.NET SDK、已经安装的 SCP:SL 专用服务器、`ServerKeybinds.dll` 和 `0Harmony.dll`。

你还需要把 Cement 的七个私有原生边缘源码文件放在 `bridge/ReferenceNative` 下。这些文件有意由 Git 忽略，不在公开仓库中。缺少它们时，`build.ps1` 会用清楚的“缺少源码”错误停止；编译器本身和其他示例仍然可以完整编译。

编译脚本兼容系统自带的 Windows PowerShell 5.1，也兼容更新版本的 PowerShell。
它把本示例上方两级目录当作 EPLab 根目录，因此从独立克隆的 EPLab 也能运行，不要求外面还有特定的父仓库。

在这个文件夹打开 PowerShell，然后运行：

```powershell
.\build.ps1
```

如果 Harmony 放在别处：

```powershell
.\build.ps1 -HarmonyPath 'C:\你的路径\0Harmony.dll'
```

省略 `-HarmonyPath` 时，脚本依次检查 `EPLAB_HARMONY_DLL` 和 LabAPI 全局依赖目录中的 `0Harmony.dll`，不会搜索个人下载文件夹。

脚本会完成五件事：

1. 用固定哈希检查本地提供的七个私有原生边缘源码文件。
2. 用真实游戏程序集编译窄桥接层。
3. 运行 EPLab：`.易` → 生成的 C# → `net48` DLL。
4. 创建一个临时的结构化安装包。
5. 先测试临时包；17 项数学检查和生成类型反射冒烟检查全部通过后，才发布为 `bin\Release\package`。

发布包中恰好有两个文件：

- `bin\Release\package\plugins\EPLabSpinBot.dll`：生成的 LabAPI 插件。
- `bin\Release\package\dependencies\global\SpinBot.EPLab.Bridge.dll`：窄小的原生桥接层。

用于本地服务器时，把 `package\plugins` 中的文件复制进 `LabAPI\plugins\<端口>`，把 `package\dependencies\global` 中的文件复制进 `LabAPI\dependencies\global`。你仍须另外把 `ServerKeybinds.dll` 和 `0Harmony.dll` 安装到 `LabAPI\dependencies\global`；发布包有意不再分发它们。这个示例不会自行部署或重启服务器。

## 我应该改哪个文件？

从下面六个文件开始：

| 易源码 | 它的简单任务 |
| --- | --- |
| `src/插件与配置.易` | 插件名称、配置、启动、停止和 LabAPI 事件 |
| `src/模型与旋转数学.易` | 四种模式以及全部可重复的旋转/射速公式 |
| `src/旋转状态.易` | 哪些玩家已开启，以及别人应该看到什么姿态 |
| `src/玩家权限与设置.易` | 持久权限和“服务器专属设置”菜单 |
| `src/管理命令.易` | `spinbot` RA 命令家族 |
| `src/旋转数学验证.易` | 从参考插件数学测试逐项移植的 17 项可执行检查 |

生成的 C# 位于 `obj\eplab\Release\EPLab.All.Generated.cs.txt`。可以放心阅读，但不要修改，因为下次编译时 EPLab 会重新写入。

## 玩家怎样使用？

SpinBot 默认上锁。管理员先授予权限：

```text
spinbot access <玩家ID或名称> on
```

该玩家随后会在游戏自带的“服务器专属设置”中看到 **SpinBot / HVH 视觉** 区块。API 3 会把它放在“玩法”分类。建议的 `V` 键只是建议，玩家需要选择“采用建议”，也可以绑定自己的按键。

菜单包含平滑、反向、抖动、随机模式，旋转速度，观察者俯仰，最低预测伤害，以及“强制开火”与“开火时修正”。玩家自己的镜头不会旋转。

可选的 1.8 秒切换提示是有意保留的短时兼容反馈。它不是持久或频繁刷新的文字，因此本示例不会仅为它新增 HSM 显示依赖。

## RA 命令

所有命令都需要 `FacilityManagement`：

```text
spinbot demo
spinbot spawn <ntf|ci> [数量]
spinbot access <玩家ID|名称> <on|off>
spinbot player <玩家ID|名称> <on|off|toggle>
spinbot clear
spinbot speed <每秒角度>
spinbot mode <smooth|reverse|jitter|random>
spinbot pitch <-88..88>
spinbot jump <on|off>
spinbot status
```

创建假人的命令必须由存活的 RA 用户在游戏内执行。

## 诚实的兼容性说明

这不是官方易语言工程导入器，也不会假装 SCP:SL 私有内部全部由纯易源码实现。它验证的是 EPLab 的“源码 → C# → net48”流程、Unicode 标识符、CLR 调用、LabAPI 生命周期/事件/配置、命令生成、API 3 设置集成、源码行映射，以及明确划分的原生桥接边界。

七个接触引擎的 C# 文件是放在 `bridge/ReferenceNative` 下的固定私有验证输入。它们对应的组件名称和 SHA-256 记录在[来源页面](bridge/ReferenceNative/PROVENANCE.zh-CN.md)中，但源码本身不会公开。Cement 让它们保持私有，不属于本仓库或其 MIT 许可证材料。

因此，公开示例在易侧/编译器边界上是完整的，但在原生引擎边界上有意不自给自足。拥有私有文件的维护者可以在不启动游戏时运行自动冒烟检查；真正的引擎行为仍需进入游戏做 QA。
