# SpinBot written with EPLab

[Chinese version](README.zh-CN.md) · [Validation record](VALIDATION.md) · [Private native-source boundary](bridge/ReferenceNative/PROVENANCE.md)

This is a real LabAPI port whose everyday logic is written in friendly 易-style source. EPLab translates those `.易` files into readable C#, then the .NET compiler makes a `net48` DLL. Cement has chosen to keep the seven engine-facing C# implementation files private, so a public clone contains the full Easy side and validation record but cannot build the native bridge by itself.

Think of it like a toy made from two boxes:

1. The **big Easy box** decides what SpinBot should do.
2. The **small C# bridge box** touches difficult SCP:SL engine parts that are not stable public LabAPI features.

The bridge is intentionally narrow. Configuration, startup/shutdown, rotation and rate math, player state, persistent access, ServerKeybinds API 3 settings, localization, and RA command behavior are all in `.易`. C# is retained only for the Harmony observer-sync patch, native firearm/hitreg requests, and native RA-dummy mechanics.

## Build it

You need Windows, the .NET SDK, an installed SCP:SL dedicated server, `ServerKeybinds.dll`, and `0Harmony.dll`.

You also need Cement's seven private native-edge source files placed under `bridge/ReferenceNative`. They are intentionally ignored by Git and are not in the public repository. Without them, `build.ps1` stops with a clear missing-source error; the compiler itself and the other examples remain fully buildable.

The build script works in the built-in Windows PowerShell 5.1 as well as newer PowerShell versions.
It treats the directory two levels above this example as the EPLab root, so it also works from a standalone EPLab clone and does not assume any particular parent repository.

Open PowerShell in this folder and run:

```powershell
.\build.ps1
```

If Harmony is somewhere unusual:

```powershell
.\build.ps1 -HarmonyPath 'C:\path\to\0Harmony.dll'
```

When `-HarmonyPath` is omitted, the script checks `EPLAB_HARMONY_DLL` and then `0Harmony.dll` in LabAPI's global dependency directory. It does not search personal download folders.

The script does five things:

1. Checks the seven locally supplied private native-edge files against their pinned hashes.
2. Builds the narrow bridge against the real game assemblies.
3. Runs EPLab: `.易` → generated C# → `net48` DLL.
4. Creates a temporary structured package.
5. Tests that temporary package, then publishes it as `bin\Release\package` only after all 17 math checks and the generated-type reflection smoke pass.

The published package contains exactly:

- `bin\Release\package\plugins\EPLabSpinBot.dll` — the generated LabAPI plugin.
- `bin\Release\package\dependencies\global\SpinBot.EPLab.Bridge.dll` — the narrow native bridge.

For a local server, copy the file under `package\plugins` into `LabAPI\plugins\<port>`, and copy the file under `package\dependencies\global` into `LabAPI\dependencies\global`. You must install `ServerKeybinds.dll` and `0Harmony.dll` in `LabAPI\dependencies\global` separately; the package deliberately does not redistribute them. This example does not deploy or restart a server by itself.

## Where should I edit?

Start with these six files:

| Easy file | Plain-English job |
| --- | --- |
| `src/插件与配置.易` | Plugin name, configuration, startup, shutdown, and LabAPI events |
| `src/模型与旋转数学.易` | The four modes and all deterministic rotation/rate formulas |
| `src/旋转状态.易` | Which players are active and what observers should see |
| `src/玩家权限与设置.易` | Persistent access and the Server-Specific Settings menu |
| `src/管理命令.易` | The `spinbot` RA command family |
| `src/旋转数学验证.易` | 17 executable checks copied from the reference plugin's math tests |

The generated C# is in `obj\eplab\Release\EPLab.All.Generated.cs.txt`. It is safe to read, but do not edit it because EPLab writes it again on the next build.

## Player experience

SpinBot is locked by default. An admin first grants access:

```text
spinbot access <player-id|name> on
```

That player then receives a **SpinBot / HVH Visuals** block in the built-in Server-Specific Settings panel. API 3 places it in the Gameplay category. The suggested `V` key is only a suggestion; the player chooses whether to apply it or bind another key.

The menu includes smooth, reverse, jitter, and random modes; rotation speed; observer pitch; minimum predicted damage; and force-fire versus correct-on-fire control. The owner's camera does not spin.

The optional 1.8-second toggle hint is deliberate, short compatibility feedback. It is not persistent or frequently refreshed text, so this example does not add an HSM display dependency for it.

## RA commands

All commands require `FacilityManagement`:

```text
spinbot demo
spinbot spawn <ntf|ci> [count]
spinbot access <player-id|name> <on|off>
spinbot player <player-id|name> <on|off|toggle>
spinbot clear
spinbot speed <degrees-per-second>
spinbot mode <smooth|reverse|jitter|random>
spinbot pitch <-88..88>
spinbot jump <on|off>
spinbot status
```

Commands that create dummies must be run in-game by an alive RA user.

## Honest compatibility note

This is not an official 易语言 project importer and it does not claim that SCP:SL's private internals are pure Easy code. It validates EPLab's source-to-C#-to-net48 pipeline, Unicode identifiers, CLR calls, LabAPI lifecycle/events/config, command generation, API 3 settings integration, source mapping, and a deliberate native-bridge boundary.

The seven engine-facing C# files are a pinned private validation input under `bridge/ReferenceNative`. Their component names and SHA-256 values are recorded in the [provenance page](bridge/ReferenceNative/PROVENANCE.md), but the source itself is not published. Cement retains it privately, outside this repository and its MIT license.

The public example is therefore intentionally complete at the Easy/compiler boundary but not self-contained at the native engine boundary. A maintainer with the private files can run the automated smoke test without starting the game; actual engine behavior still needs in-game QA.
