# SpinBot verification

[Back to the example](README.md) · [Private native-source boundary](bridge/ReferenceNative/PROVENANCE.md)

## Prerequisites and checks

A full build requires locally installed SCP:SL dedicated-server assemblies, `ServerKeybinds.dll`, `0Harmony.dll`, and Cement's seven private native-edge files. The public repository contains the complete Easy side and bridge contracts, but cannot build the native bridge without those private inputs. Their required hashes are in the [provenance page](bridge/ReferenceNative/PROVENANCE.md).

Run from this example's directory:

```powershell
.\build.ps1 -HarmonyPath 'C:\path\to\0Harmony.dll'
```

The script supports Windows PowerShell 5.1 and newer PowerShell versions. It resolves EPLab from `../..`, checks private input hashes, and builds the bridge and generated plugin. The staged-DLL harness runs 17 math assertions emitted from Easy source and reflects the generated plugin and command types. It resolves dependencies from the staged package, game managed directory, and LabAPI global dependencies. Only a passing stage replaces `bin/Release/package`.

The package contains:

- `plugins/EPLabSpinBot.dll`
- `dependencies/global/SpinBot.EPLab.Bridge.dll`

Inspect generated C# at `obj/eplab/Release/EPLab.All.Generated.cs.txt`. Neither the private source nor a compiled bridge package is published by this repository.

## Ownership contract

| Behavior | Owner | Validation |
| --- | --- | --- |
| Plugin metadata, config, enable/disable, and LabAPI events | `src/插件与配置.易` | Generated net48 compile |
| Smooth, reverse, jitter, random pose, and fire-rate math | `src/模型与旋转数学.易` | Produced-DLL math checks |
| Player/dummy state, continuity, role rebind, and deterministic seed | `src/旋转状态.易` | Generated net48 compile and bridge contract |
| Persistent grants and API 3 category, block, visibility, and settings | `src/玩家权限与设置.易` | Compile against the real `ServerKeybinds.dll` |
| Complete RA command surface and localized responses | `src/管理命令.易` | Generated command-adapter compile |
| Observer-only FPC sync substitution | Private maintainer source (not published) | Pinned hash and bridge net48 compile |
| Native hitreg, damage prediction, force fire, and corrected fire | Private maintainer source (not published) | Pinned hashes and bridge net48 compile |
| RA dummy spawn, demo, movement, and weapons | Private maintainer source (not published) | Pinned hashes and bridge net48 compile |

## Local game walkthrough

The build and staged-DLL checks do not instantiate the plugin or call Unity/game services. Use an isolated, visible local test server, a real game client, RA permissions, and enemy dummies to check:

- grant persistence and hidden settings for unauthorized players;
- all four observer poses and the owner's unchanged camera;
- role-change rebind, both fire modes, and obstruction rejection;
- dummy demo cleanup, round restart, and plugin disable;
- Harmony conflict fail-closed behavior.

Report results and reviewed captures in the response; keep raw logs and captures in ignored runtime storage. Compilation does not establish client rendering, hit registration, or server deployment.
