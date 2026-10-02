# ScriptedWarhead for EPLab

### What is this?

This is a real Easy-style validation project that compiles into a LabAPI plugin DLL. Think of it as a three-layer machine:

1. [脚本核弹.易](src/脚本核弹.易) is the brain. It owns the timeline, configuration decisions, localization, fallback behavior, and RA command replies.
2. EPLab translates that brain into inspectable C#.
3. [Bridge](Bridge) is a small toolbox for unavoidable SCP:SL internals: native door, light, warhead, and player actions; MEC timers; ffmpeg; and SpeakerToy audio.

The bridge is not a second plugin and contains no competing timeline policy.

### Easiest build

You need:

- Windows;
- the .NET 8 SDK;
- SCP:SL Dedicated Server installed;
- `ffmpeg` only if you want custom MP3 playback.

Double-click `build.cmd`. When it finishes, open `bin\Release\package`. Alternatively, run:

```powershell
.\build.ps1
```

For a non-default dedicated-server installation:

```powershell
.\build.ps1 -ManagedDirectory "D:\YourServer\SCPSL_Data\Managed"
```

Deploy the two outputs separately:

- copy `plugins\ScriptedWarheadEPLab.dll` to `%APPDATA%\SCP Secret Laboratory\LabAPI\plugins\<port>\`;
- copy `dependencies\ScriptedWarhead.EPLab.Bridge.dll` to `%APPDATA%\SCP Secret Laboratory\LabAPI\dependencies\<port>\`.

The build script first stages and validates the exact four DLL/PDB files in a unique temporary directory, then replaces `package` as a whole. Old names and extra DLLs cannot silently remain in a successful package; a failed replacement restores the prior package.

### Behavior

- At 19 minutes after round start, it calls the game's native Deadman Switch.
- It does not start Omega at 19:00. It waits until the native DMS actually detonates.
- Omega opens every mapped non-elevator door and leaves elevator doors completely untouched.
- Map lights become RGB `(0, 200, 255)`.
- The 182-second default Omega countdown is restarted when custom audio actually begins playing.
- Unavailable audio falls back to a native CASSIE message.
- At expiry it invokes native warhead detonation when still available, then kills every surviving player.
- Round end, round restart, and waiting-for-players cancel timers, stop audio, and restore recorded doors and lights.

### RA commands

Both commands require `WarheadEvents` permission:

- `sw status` reports the timeline phase or remaining time;
- `sw omega` manually starts a fresh playback-relative Omega sequence.

### Configuration

LabAPI creates the configuration after the first load. The commonly used settings are:

```yaml
is_enabled: true
language: ""
omega_audio_enabled: true
omega_audio_path: "D:\\Audio\\OmegaWarhead.mp3"
omega_audio_ffmpeg_path: "ffmpeg"
omega_audio_volume: 1
omega_audio_max_seconds: 240
omega_subtitle_delay_seconds: 3.19
omega_subtitle_duration_seconds: 6
omega_detonation_delay_seconds: 182
```

`language: "en"` forces English and `"cn"` forces Chinese. Empty normally means “match the client,” but Omega uses one server-wide CASSIE message and cannot show a different language to each player, so empty safely falls back to Chinese.

`omega_audio_path` must be an absolute path on the server. An empty or missing file, ffmpeg failure, or audio that has not finished preparing does not stop the timeline; it uses the CASSIE fallback.

### Known conflicts

- plugins that also change doors, map lights, or warhead state;
- audio plugins that occupy all 256 SpeakerToy controller IDs;
- plugins that also register `sw` or `scriptedwarhead`.

Native elevator doors are never modified. A round reset restores only the non-elevator doors and lights recorded by this example, but another plugin changing the same object during Omega can still be overwritten by whichever plugin restores last.

### Inspect the translation

After a successful build, open:

`obj\eplab\Release\EPLab.All.Generated.cs.txt`

This is the readable C# produced by EPLab. Errors use `#line` to point back to the `.易` file instead of blaming only a generated file.

### Verification boundary

The `.易 -> generated C# -> net48 DLL` build checks translation and types against the installed game and LabAPI assemblies. Multiplayer audio delivery, facility behavior, deaths, and round restoration require a visible local server and real clients. See the [verification guide](VALIDATION.md) for the available checks and walkthrough.
