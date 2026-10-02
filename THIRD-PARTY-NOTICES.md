# Third-party notices

### Independence and names

EPLab is an independent, unofficial implementation. It is not produced, approved, or supported by the publisher of 易语言, Northwood Studios, or the maintainers of the community projects below.

“易语言” and related product names belong to their respective owners. “SCP: Secret Laboratory”, “SCP:SL”, and LabAPI names belong to their respective owners. They are used here only to explain source compatibility and interoperability.

EPLab does not contain the official 易语言 compiler, runtime, support libraries, IDE files, or proprietary `.e/.ec` implementation. It also does not redistribute SCP:SL game assemblies.

### Public projects consulted

The compiler/parser implementation in this folder was written for EPLab. The following public projects were consulted as behavioral, terminology, architecture, or file-format references; their source and binaries are not bundled into EPLab.

| Project | Why it was consulted | Upstream license |
| --- | --- | --- |
| [OpenEpl/TextECode](https://github.com/OpenEpl/TextECode) | Public, readable textual conventions for 易-style code | [MIT License, Copyright © 2022 OpenEpl](https://github.com/OpenEpl/TextECode/blob/main/LICENSE.txt) |
| [OpenEpl/EProjectFile](https://github.com/OpenEpl/EProjectFile) | Public description and tooling for structured `.e/.ec` project containers | [The Unlicense / public-domain dedication](https://github.com/OpenEpl/EProjectFile/blob/main/LICENSE) |
| [OpenEpl/EplOnCppCore](https://github.com/OpenEpl/EplOnCppCore) | Prior independent translator architecture and terminology | [MIT License, Copyright © 2019 QIQI](https://github.com/OpenEpl/EplOnCppCore/blob/master/LICENSE) |
| [aiqinxuancai/e-packager](https://github.com/aiqinxuancai/e-packager) | Modern unpacked, diffable representation of 易 projects and an independent compilation precedent | [MIT License, Copyright © 2026 aiqinxuancai](https://github.com/aiqinxuancai/e-packager/blob/master/LICENSE) |
| [易语言 official site/manual](https://www.eyuyan.com/) | Public user-facing terminology and learning material | Copyright and terms remain with the publisher |

These links acknowledge research sources; they do not change EPLab's compatibility limits. See [COMPATIBILITY.md](docs/COMPATIBILITY.md).

### Private SpinBot native edge

The complete SpinBot validation used seven engine-facing C# files owned by Cement. At Cement's request, those source files remain private, are ignored by Git, and are not included in this public repository or its MIT-licensed material. The public example contains the `.易` implementation, bridge contract, build recipe, and an honest [validation provenance record](examples/SpinBot/bridge/ReferenceNative/PROVENANCE.md), but not the private implementation.

### Build-time and target APIs

#### Microsoft .NET Framework reference assemblies

For a `net48` output, the generated temporary C# project contains this private build-time package reference:

```xml
<PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies.net48"
                  Version="1.0.3"
                  PrivateAssets="all" />
```

Package information and its upstream license link are on the [NuGet package page](https://www.nuget.org/packages/Microsoft.NETFramework.ReferenceAssemblies.net48/1.0.3). The package supplies compile-time reference assemblies; EPLab does not copy it into the finished plugin DLL.

#### LabAPI

Generated LabAPI plugins compile against [Northwood Studios LabAPI](https://github.com/northwood-studios/LabAPI), which is published upstream under the [GNU Lesser General Public License version 3](https://github.com/northwood-studios/LabAPI/blob/master/LICENSE).

EPLab resolves `LabApi.dll` from the user's local SCP:SL Dedicated Server Managed folder and marks the reference `Private=false`. It is not copied into EPLab's output. Users distributing a plugin remain responsible for following LabAPI's license and any applicable SCP:SL server/plugin terms.

#### SCP:SL, Unity, command, and plugin dependency assemblies

`Assembly-CSharp.dll`, `CommandSystem.Core.dll`, `YamlDotNet.dll`, Unity assemblies, Harmony, ServerKeybinds, and other example references are resolved from the user's game installation or LabAPI dependency folder. A reference name in `project.eplabproj` does not bundle or redistribute that DLL. Each assembly remains under its own upstream terms.

### No endorsement and no warranty

Upstream project names are used for accurate attribution and interoperability only. No upstream author has endorsed EPLab. Follow each linked project's current license when copying its code or redistributing its binaries.

This notice is attribution information, not legal advice and not a replacement for any project license that may apply to EPLab itself.
