# Wendy PC / Android audio routing — 0.5.12

Date: 2026-10-07. Device: SM-S942N. No F1 inputs sent during this validation.

## Changes

- Independent microphone and response-output routes, default PC/PC; all four combinations.
- Windows native English SAPI input and English TTS on a dedicated below-normal priority thread. No audio streaming between devices.
- Wendy volume 0–100%, saved with routes in LocalAppData/mDrive/audio.json. PC playback uses Windows default output; Android applies gain on each new utterance.
- Single WinExe pairing/settings window; QR 205px square (previously 410px). No automatic console/log window. Diagnostics remain available on request.
- Routing revisions discard old Android queries; route changes cancel audio and conversation approvals. Bounded speech mailbox, no offline speech replay. PC classification is dispatched outside telemetry processing.
- Authenticated LB input posts an atomic game-radio guard. No speech APIs or Wendy locks run in the controller receive callback. LB held + 8s release grace pauses both audio paths.
- Controller SafetyGate, input freshness deadlines, GamepadWorker, steering calibration and pedal mapping were not changed for this audio work.

## Results

- Windows: 45/45 test groups passed, including four audio routes, volume metadata, stale route rejection, single-destination delivery, LB suppression and OFF muting.
- Existing controller jitter/loss/recovery, stale ACK, held throttle/steering, output exception and worker isolation tests passed. These synthetic results do not prove long-run F1 reliability.
- Android: 51/51 unit tests passed; debug APK + instrumentation APK built; lint 0 errors / 79 warnings.
- APK installed on SM-S942N; package verified versionName 0.5.12 / versionCode 24.
- On-device detached UI regression passed: both held pedals, all 12 original buttons, LB radio mute without microphone start, right stick and cancelled/removed targets. No gamepad transport used by that test.
- Windows UI visually checked with a loopback/null-backend receiver: compact QR, two route selectors, volume and clear missing-engine error fit the window. Temporary receiver closed after inspection.
- Runtime SAPI probe: **0 English recognition engines, 1 English output voice**. PC microphone recognition is therefore not available on this particular Windows installation until a compatible English speech engine is installed. No silent Android fallback and no OS language/permission changes were made.

## Not verified

- Live English recognition through a PC headset (blocked by missing engine).
- Actual headset loudness, Windows mixer balance, Bluetooth hands-free profile behavior.
- Extended real F1 driving with mixed-device audio routes. Audio routing tests use a fake PC speech adapter plus authenticated TCP; they are not acoustic or game tests.

Until English PC recognition is installed, select Android microphone + PC output explicitly in the receiver. Input/output default remains PC/PC as requested. Automatic game-volume ducking and >100% amplification are not implemented.
