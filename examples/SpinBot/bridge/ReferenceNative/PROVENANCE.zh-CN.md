# 私有原生边缘验证来源

[英文版](PROVENANCE.md) · [返回示例说明](../../README.zh-CN.md)

完整验证使用了 Cement 私有 SpinBot 实现中七个接触游戏引擎的文件，固定于 2026-08-29。Cement 目前选择不公开这些源码文件。

公开仓库保留这份来源记录，但不包含七个 `.cs` 文件：`.gitignore` 会明确忽略它们。配置、生命周期、旋转/状态策略、持久权限、ServerKeybinds API 3 集成、RA 命令、本地化和验证数学仍然公开位于 `.易` 文件中。`BridgeContracts.cs` 记录 EPLab 与原生层的边界。

| 私有文件 | 参考组件 | 验证时的 SHA-256 |
| --- | --- | --- |
| `AutoAimFireService.cs` | `SpinBot.Services.AutoAimFireService` | `1AD20A0840787B8205ABEA7D37E16F0E79C361C4CC259273BFA7581CB1EA9576` |
| `AutoFireRateMath.cs` | `SpinBot.Services.AutoFireRateMath` | `419C881065A75960C96BA14EF15B9D195AD0B617253FB291904407D6F1CF8E49` |
| `CorrectOnFirePatch.cs` | `SpinBot.Patches.CorrectOnFirePatch` | `E68F38FF9B9FCC47D5350931938D1F9A08714EA38655D48E994D06BF6E4843D7` |
| `DummyRevolverPatches.cs` | `SpinBot.Patches.DummyRevolverPatches` | `2F9EE91CD9FC2B55C84AB032CEC0178494D0AF744A395A889EE96D55EA9261BB` |
| `DummySpinService.cs` | `SpinBot.Services.DummySpinService` | `893FC38EBA810C4E5D1E7ACC001C4407991372E3CEC71A8DB3097D7F6EDDABC3` |
| `SpinRateMath.cs` | `SpinBot.Services.SpinRateMath` | `E3A4CBE288B2531660E53C3B5460A1BD896E1F1BC78DD27C38C112109C0490C6` |
| `VisualRotationPatch.cs` | `SpinBot.Patches.VisualRotationPatch` | `FD1F7D20C3FFD609D9A5304A0B564AEEF5580BA0B777C3B25AB0EFD371ECAEFC` |

这些哈希可以标识记录验证时使用的私有输入，而无需公开其内容。维护者若有意更新其中某个文件，请检查对应行为，重新运行完整编译和冒烟测试，并更新本表。

## 公开边界

七个私有文件的著作权仍属于 Cement。它们不属于公开 Git 文件树、公开发布提交或 EPLab 的 MIT 授权材料。

因此，公开克隆不能复现完整的 SpinBot 原生桥接编译。这是有意的：公开材料负责验证并讲解 EPLab 一侧，本页面负责记录仅供维护者使用的原生验证边界。仓库也不会提交编译后的桥接 DLL。
