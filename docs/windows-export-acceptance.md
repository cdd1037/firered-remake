# Windows x64 trimmed export

`python dev export windows` creates a standalone Chinese test folder at
`exports/windows` and a normal DEFLATE ZIP at
`exports/FireRed-Windows-x64-zh-Hans.zip`. Existing outputs are never overwritten.
Install official Godot 4.7.2 .NET Windows x64 export templates before exporting.
Keep the executable, PCK, .NET runtime folder, `content/generated`, and font license
 together. Fully extract and double-click `Play-Chinese.cmd`; direct EXE launch
retains the English default. The launcher only changes process-local variables.
No SDK, source inputs, checkpoints, or player saves belong in this package.

## Trimming policy

Only Windows `ExportRelease` / `win-x64` automatically enables
`FireRedTrimmedExport`. This sets `PublishTrimmed=true`, `TrimMode=full`, and
self-contained publishing. Default Linux behavior is unchanged. An explicit
`FireRedTrimmedExport=true` environment/property can exercise the shared policy
for a separate Linux compatibility test; this cannot prove Windows execution.

Roots retain all five Game assemblies, GodotSharp, System.Collections and
System.Collections.Immutable. The bounded CoreLib descriptor retains Dictionary,
List and HashSet generic members used by reflection JSON. It does not retain all
CoreLib. JSON reflection stays enabled. `EnableDynamicLoading` stays enabled.
Godot's native host requires
`_EnableConsumingManagedCodeFromNativeHosting=true`; after `PrepareForILLink`,
the intermediate assembly's root is changed from EntryPoint to All, preserving
the native-generated entry point without inventing Main or disabling trimming.

Detailed linker warnings remain visible and are not a zero-warning trim-safety
certification. Only the explicit trimming configuration permits the known
reflection warning codes; ordinary compilation warnings remain errors. Runtime
coverage remains necessary for dynamically selected content and save models.

The exporter checks Windows x64 PE headers, bundled CoreCLR major 10, both native
host and JSON switches, required assemblies, actual CoreLib shrinkage against
the matching runtime pack, exact SHA256 match with ILLink output, and ZIP CRC.
Evidence is developer-side and not a runtime requirement.

## Optional cloud IPC workaround

Official Microsoft.NET.ILLink.Tasks 10.0.12 targets declare ILLink and
ComputeManagedAssemblies with TaskHostFactory. This cloud sandbox previously
rejected task-host Unix-socket IPC (MSB4216). The opt-in import executes the same
official tasks using AssemblyTaskFactory and Override=true under Core MSBuild:

```sh
source scripts/env.sh
export CustomAfterMicrosoftCommonTargets="$PWD/scripts/dev/il-link-in-process.targets"
export UseSharedCompilation=false
python dev export windows
```

Do not set these optional variables on machines with working task-host IPC.
This does not modify SDK binaries, linker arguments or security settings.
Packaging contract tests do not create an export:

```sh
python scripts/checks/test-windows-export.py
```

## Recovery state, 2026-10-20

After workspace loss, the Windows exporter was reconstructed from retained source
text; configuration, descriptor, optional task override, preset and contract tests
were restored or reconstructed. This is not a byte-for-byte restoration of the
lost commit. The override task names/factory declarations were rechecked against
the official NuGet 10.0.12 package. Eight local contract tests pass. No export was
run during recovery, honoring the instruction to defer packaging.

Official Godot 4.7.2 .NET and SDK 10.0.401 were downloaded outside the project and
matched the repository lock's SHA256/SHA512. Installed runtimes are 10.0.12.
`python dev doctor` passes. Export templates were not restored by this recovery;
installation is required before a later explicitly requested export.

Earlier cross-export and Linux smoke outcomes are historical, not new evidence
for this restored workspace. Windows execution, real audio, and full-campaign
acceptance remain unverified. A prior isolated Linux post-save shutdown stalled
once; subsequent exact and ordinary exit repetitions passed. Do not silently
claim that intermittent issue fixed or convert absent evidence into a pass.
