# SpinBot EPLab validation

[Chinese version](VALIDATION.zh-CN.md) · [Back to the example](README.md) · [Private native-source boundary](bridge/ReferenceNative/PROVENANCE.md)

Validated on 2026-08-29 in Cement's maintainer workspace against locally installed SCP:SL dedicated-server managed assemblies, a separately supplied `ServerKeybinds.dll` API 3 build, and seven private native-edge source files that are not part of the public repository.

## Automated result

Run:

```powershell
.\build.ps1 -HarmonyPath 'C:\path\to\0Harmony.dll'
```

Observed result:

```text
Windows PowerShell 5.1: exit 0
SpinBot.EPLab.Bridge: build succeeded, 0 warnings, 0 errors
EPLabSpinBot.generated: build succeeded, 0 warnings, 0 errors
SpinBot EPLab validation passed: math=17, reflected-types=2
```

The definitive run used `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -HarmonyPath C:\path\to\0Harmony.dll`. Package reporting uses a containment-checked helper compatible with Windows PowerShell 5.1 rather than the newer `Path.GetRelativePath` API.

Outputs:

```text
bin\Release\EPLabSpinBot.dll
bridge\bin\Release\SpinBot.EPLab.Bridge.dll
bin\Release\package\plugins\EPLabSpinBot.dll
bin\Release\package\dependencies\global\SpinBot.EPLab.Bridge.dll
obj\eplab\Release\EPLab.All.Generated.cs.txt
```

Exact published artifacts from this validation build:

| Package path | Bytes | SHA-256 |
| --- | ---: | --- |
| `plugins/EPLabSpinBot.dll` | 50,176 | `509BF25BC74E09EC6C5B9113607DDF57200A34658D509028EC4048C41ED104F4` |
| `dependencies/global/SpinBot.EPLab.Bridge.dll` | 58,368 | `57C867353AFF5C3BE8A4BA188DE8C2F0EDA670CD4A17F5227331D7C460DA09E8` |

The test harness resolves dependencies from the staged package, the real game managed directory, and LabAPI's global dependency directory. It loads the staged `EPLabSpinBot.dll`, invokes `EPLabSpinBot.旋转数学验证.运行`, and reflects `EPLabSpinBot.__EplabPlugin` plus `EPLabSpinBot.__EplabCommand0`. It checks the generated LabAPI plugin base, public constructor, `CommandSystem.ICommand` contract, and public command name. The 17 assertions execute methods emitted from `.易`; the harness does not contain a second copy of the formulas.

The maintainer build verifies the hashes of all seven private files under `bridge/ReferenceNative`, creates a unique staging directory directly under `bin\Release`, and runs the smoke test there. Only a passing stage replaces the exact `bin\Release\package` directory. The local validation package contains only the plugin and bridge in their structured destinations; neither that package nor the private source is published by this repository.

## Standalone path and private-source boundary

`build.ps1` derives the EPLab root from `../..` and resolves the CLI as `src/EPLab.Cli/EPLab.Cli.csproj`. It contains no path outside EPLab and no personal Downloads fallback. The compiler path is standalone-safe; `ServerKeybinds.dll`, game assemblies, and Cement's private native-edge source were supplied only as local build inputs.

At Cement's request, Git ignores the seven native implementation files and the public repository does not contain them or the compiled bridge package. A public clone can inspect the complete Easy-side port and this validation evidence, but cannot reproduce the full native build without those private files. See the [provenance page](bridge/ReferenceNative/PROVENANCE.md).

## Port boundary

| Behavior | Owner | Validation |
| --- | --- | --- |
| Plugin metadata, config, enable/disable, and LabAPI events | `src/插件与配置.易` | Generated net48 compile |
| Smooth, reverse, jitter, random pose, and fire-rate math | `src/模型与旋转数学.易` | 17 produced-DLL checks |
| Player/dummy state, continuity, role rebind, and deterministic seed | `src/旋转状态.易` | Generated net48 compile and bridge contract |
| Persistent grants and API 3 category, block, visibility, and settings | `src/玩家权限与设置.易` | Compile against the real `ServerKeybinds.dll` |
| Complete RA command surface and localized responses | `src/管理命令.易` | Generated command-adapter compile |
| Observer-only FPC sync substitution | Private maintainer source (not published) | Pinned hash and bridge net48 compile |
| Native hitreg, damage prediction, force fire, and corrected fire | Private maintainer source (not published) | Pinned hashes and bridge net48 compile |
| RA dummy spawn, demo, movement, and weapons | Private maintainer source (not published) | Pinned hashes and bridge net48 compile |

## Behavioral references and attribution

The private validation inputs' component names and hashes are recorded in [the provenance page](bridge/ReferenceNative/PROVENANCE.md). The port was compared with these reference components:

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
- `ServerKeybinds.KeybindRegistry` (`ApiVersion => 3`)
- `ServerKeybinds.KeybindBlock`
- LabAPI's public upstream [`Plugin<TConfig>` source](https://github.com/northwood-studios/LabAPI/blob/84b0da472e3ce42bf86cb49bde8379d00605a8f6/LabApi/Loader/Features/Plugins/Plugin%7BTConfig%7D.cs)

## Not claimed

This record covers compilation, produced-DLL math validation, and dependency-resolved type loading. It does **not** instantiate the plugin, call Unity/game services, or claim an in-game manual run, live visual confirmation, target hitreg confirmation, or server deployment. Those require a visible local test server, an in-game client, RA permission, authorized real players, and enemy dummies. Production deployment was not performed.

For manual QA, test grant persistence, hidden settings for unauthorized players, all four observer poses, role-change rebind, both fire modes, obstruction rejection, dummy demo cleanup, round restart, plugin disable, and Harmony conflict fail-closed behavior.
