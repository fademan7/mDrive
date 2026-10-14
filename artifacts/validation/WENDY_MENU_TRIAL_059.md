# Wendy menu trial 0.5.9 — 2026-10-05

## Executed

- `.tools/dotnet/dotnet.exe run --project windows/tests/PhoneWheel.Tests/PhoneWheel.Tests.csproj --no-restore -c Release`
  **44/44 groups passed**, twice. Includes existing controller safety/recovery,
  held-input/loss/output exceptions, dedicated receive isolation, plus two new
  menu trial groups (language/profiles and execution/cancellation).
- `.tools/dotnet/dotnet.exe publish windows/src/PhoneWheel.Host/PhoneWheel.Host.csproj --no-restore -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o release/receiver-menu-trial`
  **Passed**. Self-contained EXE product version 0.5.9.
- `git -c safe.directory=C:/paper/mdrive diff --check`: exit 0; existing CRLF
  normalization warnings, no whitespace errors.

## Local evidence

Read the installed F1 25 `actionmaps/keyboard.xml` without modifying it. Both
default presets map Talk=T, Pits tab=F2, MFD arrows=arrows. These are defaults,
not evidence of the active custom profile or radio menu row order.

Initial missing T/F2 menus were reported in Time Trial. The user subsequently
provided nine radio labels including Box this lap (first) and Set hard tyres
(ninth), and confirmed direction-key navigation with Enter/Right to select.
Initial cursor, soft/medium pages and repeated navigation remain unknown.
No sequence was configured/enabled; no native input was sent.
Real Box/tyre execution and mixed keyboard/pad behavior are **not yet confirmed**.

Added exact/polite phrase tests for all nine labels through the CPU classifier's
rules bypass, seven read-only queries with zero menu writes, detailed pit window
and full race report/staleness checks. Opponent reports remain gap-only.
The first follow-up run exposed `vehicle condition and weather report` being
misclassified as weather only (43/44). Adding vehicle-condition damage semantics
preserved mixed-topic rejection; the subsequent full run passed **44/44**.

## Deployment / scope

Normal receiver and Android installation left intact; no process restart.
`START_MENU_TRIAL.cmd` starts the separate build after the operator closes the
normal receiver. New QR pairing required. Android is unchanged, no new APK.
The trial is OFF by default; empty recipes cannot execute. It is restricted to
stationary solo practice, not offered as long-race automation.

SafetyGate, controller transport and GamepadWorker have no changes in this turn.
The existing rare Wendy reconnect callback now atomically invalidates the menu
profile too. Keyboard tasks have no virtual-pad access. Synthetic regression
passes do not prove live F1 latency, dropout absence or action success.
