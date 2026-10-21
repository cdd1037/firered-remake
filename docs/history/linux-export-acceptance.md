# Linux package acceptance (2026-10-19)

Historical package validation. These results apply to the 2026-10-19 export and do not validate later source revisions. See [current build and platform status](../validation.md).

The maintained `python dev export linux` path exported the current game using the
existing official Godot4.7.2 mono Linux templates and installed .NET10 SDK. The
missing template archive alone was restored from the reviewed official release
URL and matched the existing lock-file SHA256; only Linux templates were extracted.
The editor and SDK were not reinstalled. All downloads and package outputs remain
local/ignored, with no asset upload or Drive bundle.

The package at `exports/linux` contains `firered.x86_64`, `firered.pck`,
`data_Game.Godot_linuxbsd_x86_64`, `content/generated` and the font license.
Its files total216,976,043 bytes; it contains neither the SDK/editor nor raw source
assets. Keep the whole directory together. The existing developer window launcher
now calls this current executable, forwards arguments, and clears development
runtime/project-root overrides instead of attempting the retired probe executable.
Shell syntax and an isolated executable stub verified its path, arguments,
environment cleanup and evidence destination.

## Actual checks

- Export's existing clean-environment headless readiness passed with isolated
  HOME/XDG and package-owned resources/runtime. This smoke deliberately terminates
  after readiness and is not a graceful-close check.
- The actual exported window ran on own-cloud X11/Mesa software OpenGL with
  `env -i`, fresh HOME/XDG and no installed-runtime or project-root override.
  `/proc` mappings and runtime evidence located libcoreclr/System.Private.CoreLib
  inside the exported package. Runtime was Godot4.7.2/.NET10.0.12, release MVID
  `42c6126d-9013-46b4-8eb1-b41d5433a4a2`.
- A strictly validated controlled Route1 checkpoint retained its existing earned
  state. One public Down moved (2,4)→(2,6). Start→SAVE→YES displayed the success
  text and wrote generation59. Normal Escape exited0.
- A second launch reused only that isolated user profile, with no checkpoint
  input. Public Continue loaded the same saved generation and resumed visibly at
  (2,6); public Start still worked. Normal Escape again exited0.
- Both runs had no runtime errors, exceptions or leak warnings and left no game
  window/process. Unsupported VSync is a recorded driver warning.

External evidence: `/workspace/shared/linux-export-native/acceptance.json` and
its initial/continue screenshots and process/runtime logs. No generated evidence
or assets are tracked in Git. The historical application identity remains intact
so existing user-save locations do not change.

## Remaining limits

This is a standalone package/dependency/Save/Continue acceptance, not a fresh
whole-campaign playthrough or all-platform export. Audio was Dummy. Read-only
cloud inspection found FFmpeg and capture libraries but no live Pulse/PipeWire
service/socket or sound device; ALSA null only discards/captures zero samples.
No available non-Dummy capture path or actual listening was verified, and no
backend was installed/configured. Digital waveform tests do not close that gap.
