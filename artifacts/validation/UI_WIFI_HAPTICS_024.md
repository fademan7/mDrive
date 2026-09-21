# 0.2.4 validation — 2026-09-14

## Changes

- Main screen: only square Center/Options actions. QR is inside Options. One-line steering readout/bar; larger square-aspect central gamepad buttons. User pedal widths (~20.25% each) preserved; old button layout backed up in `phonewheel_controls/layout_before_compact_v4`. Pedal 10% endpoint margins and steering halfRange=116/curve=1 unchanged.
- Network/host release transitions enter neutral preparation, not permanent stopped state. All touches must be lifted, steering neutral, sensor fresh, connection fresh for 700 ms before a new ARM edge; host still requires its independent 300 ms neutral dwell. Sensor/focus faults release as before. No re-centering during healthy active driving.
- Status receipt now uses a single atomic latest-message mailbox with network-thread receipt time. UI refresh cannot make old queued status look newly received, and does not enqueue a UI runnable for every status.
- Receiver status/ACK tick 20 ms (previously 50 ms); 100 ms freshness and 150 ms watchdog unchanged. Haptic scheduling remains capped separately (~60 ms). Transient UDP send socket errors do not terminate all workers.
- Android re-sends authenticated Hello after 1 s with no Wi-Fi reply, retaining sequence. PC allows endpoint replacement only after previous input is stale, authenticated newer Hello, with release and old-challenge invalidation. Active Wi-Fi endpoint cannot be replaced.

## Tests and device checks

- Final Android offline build, unit tests, lint PASS: 30 tests (AutoDrive 8, Core 14, Pairing 3, Transport 5), zero failures. Existing ZXing deprecation / analytics environment warnings remain.
- C# tests 12/12 PASS.
- Real loopback USB TCP/CSV integration PASS, partial-frame watchdog 152.637 ms, source-port reconnect, held throttle blocked, neutral rearm.
- `tools/test_wifi_recovery.py` real loopback UDP integration PASS: active endpoint protected, stale endpoint reauthenticated, held throttle blocked, neutral rearm. Initial test harness hit Windows ICMP reset while receiver was still starting; bounded startup retry fixed, subsequent run passed. This is not a physical Wi-Fi signal-loss test.
- 0.2.4 APK installed successfully, receiver published and launched via USB. Phone reported `USB · 운전 활성` without Resume or Center presses. New screenshot visually checked: `phonewheel-024.png`; no pedal/central control overlap. Main UI tree has only Center/Options buttons.
- Observed saved gameRumble=false before update; enabled through Options. Readback gameRumble=true, halfRange=116, curve=1, smoothing=0, deadzone=0.
- All four XInput slots absent before receiver, only slot 0 present afterwards. A bounded 0.3 s XInput rumble pulse on slot 0 was accepted and stopped. Android vibrator_manager recorded dev.phonewheel waveform at 19:22:22.992, ~309 ms duration, ending cancelled_by_user (explicit stop). This confirms PC->USB->phone vibration service, not subjective feel or actual F1 rumble generation.

## Limits / user comparison

- User reports stationary F1 cockpit wheel now matches phone rotation, high speed does not. PhoneWheel code does not receive vehicle speed or vary gain by speed. Game pad steering processing is the leading inference; no game settings changed and no exact high-speed 1:1 claim.
- EA developer search result for https://forums.ea.com/blog/f1-games-game-info-hub-en/race-your-way---ea-sports%E2%84%A2-f1%C2%AE-25-deep-dive/12111491 mentions steering-rate tuning across car speeds. Full page retrieval returned 429; does not establish a user-adjustable speed-sensitive steering toggle or exact 2026-pack behavior.
- Physical Wi-Fi radio stability, long gameplay, F1-sourced rumble, and continuous vibration feel still require user test. No firewall, Wi-Fi, USB debugging permissions, game assists, or security settings changed. Automatic neutral recovery mitigates stuck-stop behavior, not underlying radio interference.
