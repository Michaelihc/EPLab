# 第三方声明

[English](THIRD-PARTY-NOTICES.en.md)

### 独立性与名称

EPLab 是独立、非官方的实现。它不是由易语言发布方、Northwood Studios 或下面社区项目的维护者制作、批准或支持的。

“易语言”及相关产品名称属于各自权利人；“SCP: Secret Laboratory”、“SCP:SL”和 LabAPI 名称也属于各自权利人。本文只为说明源码兼容和互操作而使用这些名称。

EPLab 不包含官方易语言编译器、运行库、支持库、IDE 文件或专有 `.e/.ec` 实现，也不会重新分发 SCP:SL 游戏程序集。

### 研究时查阅的公开项目

这个文件夹中的编译器/解析器是为 EPLab 编写的。下面项目只作为公开的行为、术语、架构或文件格式参考；它们的源代码和二进制没有打包进 EPLab。

| 项目 | 查阅原因 | 上游许可证 |
| --- | --- | --- |
| [OpenEpl/TextECode](https://github.com/OpenEpl/TextECode) | 易风格代码的公开可读文本习惯 | [MIT License，Copyright © 2022 OpenEpl](https://github.com/OpenEpl/TextECode/blob/main/LICENSE.txt) |
| [OpenEpl/EProjectFile](https://github.com/OpenEpl/EProjectFile) | 结构化 `.e/.ec` 项目容器的公开说明与工具 | [The Unlicense / 公有领域奉献](https://github.com/OpenEpl/EProjectFile/blob/main/LICENSE) |
| [OpenEpl/EplOnCppCore](https://github.com/OpenEpl/EplOnCppCore) | 早期独立翻译器的架构和术语 | [MIT License，Copyright © 2019 QIQI](https://github.com/OpenEpl/EplOnCppCore/blob/master/LICENSE) |
| [aiqinxuancai/e-packager](https://github.com/aiqinxuancai/e-packager) | 现代可解包、可比较的易项目表示，以及独立编译先例 | [MIT License，Copyright © 2026 aiqinxuancai](https://github.com/aiqinxuancai/e-packager/blob/master/LICENSE) |
| [易语言官方网站/手册](https://www.eyuyan.com/) | 公开的用户术语和学习资料 | 版权与条款仍归发布方所有 |

这些链接用于感谢研究资料，不会扩大 EPLab 的兼容范围。请看 [COMPATIBILITY.md](docs/COMPATIBILITY.md)。

### 私有的 SpinBot 原生边缘源码

完整 SpinBot 验证使用了七个归 Cement 所有、接触游戏引擎的 C# 文件。依照 Cement 的要求，这些源码保持私有，由 Git 忽略，不包含在这个公开仓库或其 MIT 授权材料中。公开示例保留 `.易` 实现、桥接契约、编译方法和诚实的[验证来源记录](examples/SpinBot/bridge/ReferenceNative/PROVENANCE.zh-CN.md)，但不包含私有实现。

### 构建时与目标 API

#### Microsoft .NET Framework 引用程序集

生成 `net48` 输出时，临时 C# 项目会含这个私有构建期包引用：

```xml
<PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies.net48"
                  Version="1.0.3"
                  PrivateAssets="all" />
```

包信息和上游许可证链接在 [NuGet 包页面](https://www.nuget.org/packages/Microsoft.NETFramework.ReferenceAssemblies.net48/1.0.3)。它提供编译期引用程序集；EPLab 不会把它复制进最终插件 DLL。

#### LabAPI

生成的 LabAPI 插件会引用 [Northwood Studios LabAPI](https://github.com/northwood-studios/LabAPI)。上游以 [GNU Lesser General Public License version 3](https://github.com/northwood-studios/LabAPI/blob/master/LICENSE) 发布。

EPLab 从用户本地 SCP:SL 专用服务器 Managed 文件夹找到 `LabApi.dll`，并把引用标成 `Private=false`，不会复制到 EPLab 输出中。用户发布插件时，仍要自行遵守 LabAPI 许可证及适用的 SCP:SL 服务器/插件条款。

#### SCP:SL、Unity、命令和插件依赖程序集

`Assembly-CSharp.dll`、`CommandSystem.Core.dll`、`YamlDotNet.dll`、Unity 程序集、Harmony、ServerKeybinds 和其他示例引用，均从用户自己的游戏安装或 LabAPI 依赖文件夹解析。`project.eplabproj` 中出现引用名称，不代表打包或重新分发该 DLL；每个程序集继续适用自己的上游条款。

### 不代表背书，也不提供保证

上游项目名称只用于准确署名和互操作，不代表任何上游作者为 EPLab 背书。复制上游代码或重新分发二进制时，请遵守对应链接中的最新许可证。

本文是署名信息，不是法律意见，也不能代替 EPLab 自身可能适用的项目许可证。
