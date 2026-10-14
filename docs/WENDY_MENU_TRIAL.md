# Withdrawn in 0.5.11

The user chose F1's native voice commands on 2026-10-07. OCR and menu-trial UI /
launcher have been removed. The production Wendy service no longer constructs
the experimental menu output. The following is a historical experiment record,
not installation instructions for the current release.

# Wendy 0.5.9 — experimental menu input, NOT race-ready

The user authorized a limited menu-sequence experiment on 2026-10-05, relaxing
the previous blanket ban on blind menu macros for this trial only. No OCR,
screen capture, game memory access, new model or game-file modification is used.
The menu trial needs no Android protocol change. Android 0.5.9 separately changes
the default steering half-range to 120 degrees at the user's request.

## What is implemented

`Box box`, `OK box box`, `boxbox`, `Box this lap`, and `Prepare soft/medium/hard/
inters/wets` now have an optional native keyboard execution path. Polite wrappers
and `prepare medium tyres for the next stop` work. Mixed compounds, conditional
or negative requests, and confidence below .75 cannot send input. The classifier
cannot invent executable steps: the original utterance must pass the closed parser.

There are NO default command recipes: radio pages/rows and current user bindings
have not been verified in F1. Naming a recognised intent does not mean the game
performed it. BB/Diff shortcuts are unchanged and not required by this trial.
Understeer/oversteer handling adjustments are not implemented through menu macros.

## First actual F1 trial

1. Connect Android as usual, enable Wendy and receive fresh 2025-format telemetry.
2. Use a **solo practice** session (FP1/2/3 or short practice), not a race, online
   lobby or garage. Stop safely **off the racing line**, with throttle released.
3. Manually check the current keyboard profile. Installed default
   `actionmaps/keyboard.xml` maps **T = Talk**, **F2 = MFD Open Tab Two**, arrows =
   MFD navigation, Enter = Menu Accept. This is NOT proof of active user bindings.
   `PitStop = Space` is also present but must NOT be assumed to mean a pit request;
   Space is also clutch/gear-up. The executor does not use it.
4. Determine exact radio/MFD steps from a closed-menu starting state. Check Box
   and each requested compound independently, including repeated invocations.
   Do not guess a row or copy an assumed universal sequence.
5. Receiver **Menu trial**: enter only observed comma-separated tokens. Tokens:
   `Radio`, `Pits`, `Up`, `Down`, `Left`, `Right`, `Enter`. First token must be Radio
   or Pits; 2–16 steps. Leave untested commands blank. The confirmation checkbox
   acknowledges the keys/menu order and the limitations below.
6. Close **all** game menus, enable the trial, return focus to F1, and speak one
   command. Never open pause/radio/other menus during execution. Observe the game
   manually. This version does not inspect the screen.
7. Record whether the game actually requested a pit stop and which tyre was
   selected. Close all menus again before another command. Do not use in races.

No game input has been sent during development of this version. The exact menu
sequence and live native-key/game response are **not yet confirmed**.

## Boundaries / limitations

- Trial OFF on startup; enable expires after ten minutes. Profiles are RAM-only.
  Controller reconnect, Wendy disconnect/OFF and telemetry generation change
  disable the trial; session/flashback/reconnect invalidate pending commands.
- Only stationary (<=1 km/h, <1% throttle), on-track, active solo-practice state
  with lap/telemetry <500ms old. Normal telemetry/session freshness also applies.
- Separate optional Task-based keyboard path; no virtual-pad reports, ARM,
  calibration, transport, SafetyGate or GamepadWorker changes. Every pulse checks
  foreground process F1_25, cancellation, profile revision/expiry, and a short
  telemetry lease. Key-up runs in finally. No focus stealing.
- One 45ms pulse at a time, >=250ms between completed pulses; eight-second total
  cancellation, no queue or retries, five-second command admission cooldown.
- `Pits` waits for a NEW UDP MFD index 1 before further keys, with 1.5s timeout;
  subsequent MFD page changes stop execution. Radio page/selected row is NOT
  available in UDP and cannot be checked. MFD indices can vary by game mode.
- UDP does not reliably expose an offline pause-menu state. Closed MFD alone
  does not prove all menus are closed. Operator setup/observation remains required.
  Focus changes between key-down/up may still expose a short key to another app;
  key-up is sent to avoid leaving a key held. This is experimental keyboard input.
- Requested -> Executing -> **Unconfirmed** after the sequence, never Confirmed.
  A key delivery success is not a game-action acknowledgement. Current tyres or
  subsequently entering pit lane do not prove next-tyre selection/pit request.
- Cancel stops remaining steps; previously sent steps may already have applied.
  No automatic menu-closing key or rollback is sent (Escape could pause the game).

## Validation

Synthetic tests cover literal/polite/ambiguous/negative utterances, profile bounds,
no native call while disabled, no command queue/retry, cooldown, stopped-car gate,
low confidence, menu page feedback/timeouts, stale telemetry, output exceptions,
session/reset/revision/disable, network/race rejection, and honest cancellation.
Existing controller loss/recovery/native-failure regressions are included in the
same Windows test run. These do not establish live F1 compatibility or latency.

Windows 44/44 groups and self-contained publication passed. See
[validation record](../artifacts/validation/WENDY_MENU_TRIAL_059.md).
The initial missing T/F2 menus were observed in Time Trial. Subsequently the
user reported this visible radio page: Box this lap, Full race update, Driver in
front, Driver behind, Tyre status, Vehicle condition, Weather report, Pit stop
information, Set hard tyres. The user confirmed **direction keys followed by
Enter or Right** for selection. The initial highlighted row, reopening behavior
and soft/medium pages remain unverified. No executable recipe is inferred from
this list, nor is row nine treated as a compound selector.

The seven information labels map to read-only telemetry answers and do not
press game buttons. Full race update includes position/lap/flag, available gaps,
fresh highest wear/fuel and time penalties. Vehicle condition uses the component
damage report. Pit stop information reports actual completed stops, valid game
pit target/latest lap and predicted rejoin; it explicitly cannot confirm next
tyres or crew readiness. Adjacent-driver answers currently give same-lap gaps,
not opponent names/setups/strategy or every detail of the native engineer.
