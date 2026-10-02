# Private native-edge validation provenance

[Back to the example](../../README.md)

The native bridge requires seven engine-facing files from Cement's private SpinBot implementation. Their pinned hashes are build inputs; the source files are not published.

The public repository keeps this provenance record but not the seven `.cs` files: `.gitignore` explicitly excludes them. Configuration, lifecycle, rotation/state policy, persistent access, ServerKeybinds API 3 integration, RA commands, localization, and validation math remain public in the `.易` files. `BridgeContracts.cs` documents the EPLab/native boundary.

| Private file | Reference component | Required SHA-256 |
| --- | --- | --- |
| `AutoAimFireService.cs` | `SpinBot.Services.AutoAimFireService` | `1AD20A0840787B8205ABEA7D37E16F0E79C361C4CC259273BFA7581CB1EA9576` |
| `AutoFireRateMath.cs` | `SpinBot.Services.AutoFireRateMath` | `419C881065A75960C96BA14EF15B9D195AD0B617253FB291904407D6F1CF8E49` |
| `CorrectOnFirePatch.cs` | `SpinBot.Patches.CorrectOnFirePatch` | `E68F38FF9B9FCC47D5350931938D1F9A08714EA38655D48E994D06BF6E4843D7` |
| `DummyRevolverPatches.cs` | `SpinBot.Patches.DummyRevolverPatches` | `2F9EE91CD9FC2B55C84AB032CEC0178494D0AF744A395A889EE96D55EA9261BB` |
| `DummySpinService.cs` | `SpinBot.Services.DummySpinService` | `893FC38EBA810C4E5D1E7ACC001C4407991372E3CEC71A8DB3097D7F6EDDABC3` |
| `SpinRateMath.cs` | `SpinBot.Services.SpinRateMath` | `E3A4CBE288B2531660E53C3B5460A1BD896E1F1BC78DD27C38C112109C0490C6` |
| `VisualRotationPatch.cs` | `SpinBot.Patches.VisualRotationPatch` | `FD1F7D20C3FFD609D9A5304A0B564AEEF5580BA0B777C3B25AB0EFD371ECAEFC` |

The hashes identify the required private build inputs without publishing their contents. If a maintainer intentionally updates one, review the corresponding behavior, rerun the complete build and smoke tests, and update this table.

## Publication boundary

Copyright in all seven private files remains with Cement. They are not part of the public Git tree, the public release commit, or EPLab's MIT-licensed material.

A public clone therefore cannot reproduce the complete SpinBot native bridge build. That is intentional: the public material validates and teaches the EPLab side while this page defines the maintainer-only native build boundary. No compiled bridge binary is committed either.
