# ScriptedWarhead validation

[中文](VALIDATION.md)

### What does this example validate?

This is substantially more than a hello-world compiler path. It makes EPLab handle Unicode identifiers, generated LabAPI configuration, five official events, an RA command and permission, delegates, `out` arguments, branches, an asynchronous timeline, and calls into an external net48 assembly.

| Original behavior contract | Location in Easy source | Bridge responsibility only |
| --- | --- | --- |
| Start native DMS at 19:00 | `到达DMS里程碑` | Call `DeadmanSwitch.InitiateProtocol()` |
| Wait for a real DMS detonation | `原生核弹已引爆` | Read native `Warhead.ScenarioType` |
| Door, light, and elevator boundary | `启动欧米伽` decides when to apply it | Enumerate wrappers and save/restore native state |
| Re-anchor 182 seconds after playback starts | `音频开始`, `安排欧米伽引爆` | MEC timer and realtime clock |
| Fall back to CASSIE after audio failure | `启动欧米伽`, `音频失败` | ffmpeg, main-thread handoff, and SpeakerToy packets |
| Kill every survivor at expiry | `引爆欧米伽` | Native Warhead/Player calls and dummy protection |
| `sw omega/status` and replies | `处理命令`, `取得状态` | Case-insensitive text comparison |

### Automated validation record

Completed on 2026-08-29 against the repository's currently installed SCP:SL Dedicated Server assemblies:

1. `dotnet build Bridge\ScriptedWarhead.EPLab.Bridge.csproj -c Release`: passed with zero warnings and zero errors.
2. `eplab build project.eplabproj --cn`: passed with zero warnings and zero errors.
3. It produced the net48 `ScriptedWarheadEPLab.dll`; generated C# retained `#line` mappings to [脚本核弹.易](src/脚本核弹.易).
4. `build.ps1` successfully produced separate `plugins` and `dependencies` package folders after validating the staged file list, SHA-256 hashes, and assembly identities.
5. `validate.ps1` pre-seeds an old-name DLL sentinel; its absence after a successful build proves that the entire exact package was replaced instead of merely overwriting known filenames.

Run `validate.ps1` to repeat these checks and verify that the Easy source still contains its critical behavior markers.

### What is not automatically proven?

- No visible local server was launched for this validation, so it does not claim in-game testing of doors, lights, DMS, deaths, or round restoration.
- There were no real clients, so it does not claim multiplayer testing of MP3 packets, the 0.5-second SpeakerToy propagation wait, or the 3.19-second subtitle synchronization.
- EPLab currently hands its generated config object to Easy code through a nullable host slot. The bridge reads those values once during startup so every timeline method does not depend on a dynamic object. This adapts the config pipeline; it does not hide game policy.

Manual testing should use a visible local test server and record the port, configuration, expected and observed results, and full logs. Compile validation is not production deployment authorization.

### Intentional usability deviations

- The plugin name is `ScriptedWarheadEPLab` so validation does not collide with the original `ScriptedWarhead`; do not let both control the same round.
- `omega_audio_path` defaults empty instead of distributing one production Linux server's private path. An invalid path still safely falls back to CASSIE.
- `language` defaults to `""` under repository localization convention. One global CASSIE message cannot vary per player, so empty falls back to Chinese. Each RA reply shows only the selected language.
- The original `sw test ...` development commands exist only in `DEBUG` builds and are not exposed as user commands here. The release contracts for `sw omega`, `sw status`, and `WarheadEvents` permission remain unchanged.
