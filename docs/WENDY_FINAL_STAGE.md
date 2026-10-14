# Final stage: verified game actions and strategy

## 0.5.11 decision (supersedes menu/OCR experiments below)

The user cancelled OCR/menu automation on 2026-10-07. Game radio commands are
handled by F1's own speech recognition. LB keeps its gamepad bit and pauses Wendy
recognition/TTS while held and for 8 seconds after release. No T-key injection,
screen capture, audio forwarding or controller-session restart is used. Earlier
trial notes below are historical, not currently supported functionality.

## 0.5.9 limited experiment

The user authorized an operator-calibrated radio/MFD sequence experiment.
See [practice-only menu trial](WENDY_MENU_TRIAL.md). This is NOT verified pit/tyre
automation or race-ready support. No actual F1 trial has passed yet.

## 0.5.7 status (supersedes the historical deferral below)

The user authorized this stage. Android 0.5.8 later removed Race/Menu switching
and standalone PTT; the original gamepad remains, with LB for manual PTT.
Tyre ranges and repeated-feedback
advice are implemented. BB/Diff use opt-in direct shortcuts with telemetry
verification; actual F1 testing is still pending. Pit request/next-stop tyres/wing
remain unsupported, rather than using blind macros. See [current setup and
limitations](WENDY_057.md). The remainder records the original 0.5.6 deferral.

Deferred explicitly by the user on 2026-09-28. Do not turn voice feedback or a
pit reminder into actual game input until this stage is implemented and tested.

## Requested behavior to retain

- Explicit numeric brake bias/differential/wing requests: exact requested value,
  no guessed number, range/step validation, verify resulting telemetry before
  saying success. Ambiguous or low-confidence values must not execute.
- Understeer/oversteer correction: determine corner phase and evidence; only one
  smallest supported change at a time, bounded and reversible. No arbitrary
  universal correction, repeated drift, or setting that is unavailable in-race.
- "OK, box box": actually request pit entry, with confirmation if observable.
- Explicit next-stop tyre requests: select the requested compound/set and verify
  what the game selected. Do not confuse currently fitted tyres with next tyres.
- Full strategy optimization: pit loss, traffic/rejoin, tyre degradation, weather,
  available sets, race rules, uncertainty, and explicit user override.

## Prerequisites

Controller reliability comes first. Action executor must remain separate from
controller transport/output and must not steal D-pad/buttons while driving.
Identify supported game bindings and reliable menu/context/selection feedback.
The F1 25 UDP output protocol is telemetry, not an arbitrary game-setting RPC.
Existing code does not establish the selected MFD row or next-stop tyre setting;
do not guess them with blind menu macros or report an unconfirmed change as done.
No screen capture/OCR/vision. Test in a paused/non-race scenario before enabling
real actions. Distinguish Requested, Executing, Confirmed, Unconfirmed, Rejected.

Current 0.5.6 pit phrases create reminders only. No numeric, tyre, setup or pit
game action is sent. Component-specific damage and conservative pit guidance
are spoken directly; no damage-panel referral is required.
