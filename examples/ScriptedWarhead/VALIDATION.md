# ScriptedWarhead verification

Run checks from this example's directory after installing the prerequisites in the [example guide](README.md).

## Compiler and package checks

```powershell
.\validate.ps1
```

The script builds the native bridge and generated plugin, checks critical Easy-source behavior markers, and verifies the staged package's file list, SHA-256 hashes, and assembly identities. It pre-seeds an old-name DLL sentinel to check that a successful package replacement removes stale files. Generated C# is available at `obj/eplab/Release/EPLab.All.Generated.cs.txt` with source-line mappings.

## Ownership contract

| Original behavior contract | Location in Easy source | Bridge responsibility only |
| --- | --- | --- |
| Start native DMS at 19:00 | `到达DMS里程碑` | Call `DeadmanSwitch.InitiateProtocol()` |
| Wait for a real DMS detonation | `原生核弹已引爆` | Read native `Warhead.ScenarioType` |
| Door, light, and elevator boundary | `启动欧米伽` decides when to apply it | Enumerate wrappers and save/restore native state |
| Re-anchor 182 seconds after playback starts | `音频开始`, `安排欧米伽引爆` | MEC timer and realtime clock |
| Fall back to CASSIE after audio failure | `启动欧米伽`, `音频失败` | ffmpeg, main-thread handoff, and SpeakerToy packets |
| Kill every survivor at expiry | `引爆欧米伽` | Native Warhead/Player calls and dummy protection |
| `sw omega/status` and replies | `处理命令`, `取得状态` | Case-insensitive text comparison |

## Local game walkthrough

Use an isolated, visible local test server with real clients. Check native DMS timing, the transition after actual detonation, non-elevator doors, map lights, both RA commands and permission rejection, playback-relative Omega timing, audio-failure CASSIE fallback, survivor deaths, and cancellation/restoration on round end or restart. Check multiplayer audio and subtitle timing with the configured audio file.

Report the selected port, configuration, results, and reviewed captures in the response. Keep raw logs and captures in ignored runtime storage. A successful build does not establish game behavior or authorize production deployment.

## Configuration boundary

EPLab exposes its generated config through a nullable host slot. The bridge reads those values once during startup so timeline methods do not depend on a dynamic object; timeline policy remains in Easy source.

### Intentional usability deviations

- The plugin name is `ScriptedWarheadEPLab` so validation does not collide with the original `ScriptedWarhead`; do not let both control the same round.
- `omega_audio_path` defaults empty instead of distributing one production Linux server's private path. An invalid path still safely falls back to CASSIE.
- `language` defaults to `""` under repository localization convention. One global CASSIE message cannot vary per player, so empty falls back to Chinese. Each RA reply shows only the selected language.
- The original `sw test ...` development commands exist only in `DEBUG` builds and are not exposed as user commands here. The release contracts for `sw omega`, `sw status`, and `WarheadEvents` permission remain unchanged.
