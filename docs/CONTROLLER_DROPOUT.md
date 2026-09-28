# Controller dropout investigation — 0.5.4 candidate

**0.5.6 update:** actual incident evidence and the bounded post-expiry proof
recovery change are documented in
[CONTROLLER_WENDY_056.md](../artifacts/validation/CONTROLLER_WENDY_056.md).
The hard-timeout transitions below remain applicable to legacy senders and real
loss/hard faults. New capable clients can briefly enter neutral RecoveryHold and
resume only on a fresh post-expiry challenge. See `PROTOCOL.md` for its exact bounds.

Historical 0.5.4 investigation. From 0.5.5 onward USB controller mode and its
transport test script are removed; Wi-Fi and the flight recorder remain. The USB
results/commands below describe the old version, not a supported connection mode.

Status: **not yet confirmed** for the reported F1 incident. Synthetic failures
establish mechanisms, not the cause of a particular race interruption. Wendy,
telemetry, voice commands and controller mapping are outside this change.

## Control path and state transitions

Android sensor/input → synchronized immutable snapshot publication → dedicated
120 Hz sender → authenticated UDP (or existing framed USB transport).
PC dedicated UDP receive thread → peer/session/authentication/sequence/challenge
validation → SafetyGate → independent GamepadWorker → ViGEm.
STATUS/challenges remain on the separate 20 ms critical loop. UI and Wendy do not
run on these loops. USB framing still uses its existing asynchronous internals.

| Condition | Result / safety behavior |
|---|---|
| Isolated loss, reorder or stale challenge | Reject that packet; do not refresh accepted-input freshness. Stay armed if another valid packet arrives before 150 ms. This was already supported before 0.5.4. |
| No valid control for 150 ms | Neutral, disarm, require ordinary neutral re-arm; held throttle/steering cannot re-arm. |
| Challenge older than 100 ms, unknown or future | Reject; repeated rejection can eventually cause the same 150 ms timeout. |
| One duplicate/out-of-order/impossible sensor timestamp | New timestamp gate discards it without clearing calibration or refreshing freshness. Previously duplicate timestamps could clear calibration and start multi-stage re-arm. |
| No valid sensor for 100 ms | READY lost; neutral/disarm and normal calibration/neutral recovery remain required. |
| Foreground/focus loss | Immediate release; no background driving or stale-input hold. |
| Wi-Fi STATUS silence / reconnect | Existing liveness checks and authenticated HELLO retry remain. Reconnect does not replay held controls. |
| Output exception / output watchdog | Neutral/recovery latch and re-arm requirement remain; error/latency recorded. A blocked native driver call cannot be forcibly cancelled safely. |

Android neutral qualification (700 ms), PC neutral qualification (300 ms),
sensor freshness (100 ms), challenge validation (100 ms), and accepted-input
timeout (150 ms) were **not increased**. Re-arm intervals can overlap. Loss while
a pedal remains held intentionally requires releasing it before re-arm.

The PC formerly awaited UDP on the shared thread pool. The new dedicated receive
thread and synchronous UDP STATUS send remove that shared-pool scheduling
dependency. A test saturates the pool and demonstrates the old scheduling risk;
it does not prove that Wendy caused the reported incident. There is no packet
buffering, extra smoothing or waiting added to valid controller input.

## Flight recorder

Both platforms keep approximately the latest 10 seconds in bounded RAM. A disarm,
reconnect or output/transport error triggers a file with up to 10 seconds before
and 3 seconds after it. Incidents within the pending 3-second capture coalesce.
Normal driving does not continuously write files. A low-priority writer serializes
off the input path; producers use try-lock and drop diagnostic entries instead of
waiting. The first trigger cause is latched independently of ring contention.

- PC: `%LOCALAPPDATA%\mDrive\diagnostics\controller\controller-*.json`;
  override using `--diagnostics-dir <directory>`.
- Android: app external files `controller-diagnostics/controller-*.csv`, normally
  `/sdcard/Android/data/dev.phonewheel/files/controller-diagnostics`.
- Retention: latest 8 incident files per platform. Ring capacity: Android 8192,
  PC 32768 records. Abnormally high event rates may shorten the history; inspect
  `Dropped` / `dropped` and save-failure counters.
- App diagnostics and Receiver monitor show recorder status, dropped records,
  saved file and/or save failures. Force-killing a process before the post-window
  finishes can lose the pending capture.

Data includes monotonic timestamps, sequence/ACK, send/receive/STATUS gaps,
accepted/rejected classification, challenge/ACK age, sensor age, READY/ARM flags,
SafetyGate state/release reason, output write latency/error code and reconnects.
Android uses -1 for unavailable fields; PC uses null. READY and ARM are encoded
in protocol flags (see `PROTOCOL.md`). Android reason codes are event-specific:
sensor rejection 1=non-new, 2=future, 3=stale; lifecycle release 3=focus/pause.
Consult the event's call site for other numeric codes.

No packet payloads, exact control values, network addresses, session keys,
device IDs, voice transcripts, audio or exception messages are recorded.
Timestamps on the two devices have different epochs: correlate sequences/ACKs,
not raw timestamp subtraction. ACK age on Android is not one-way network latency.
Receive-gap maximum includes rejected arrivals; accepted-input age and rejection
history must also be examined.

To retrieve Android incidents after a race (USB debugging authorized):

```powershell
adb pull /sdcard/Android/data/dev.phonewheel/files/controller-diagnostics ./phone-controller-diagnostics
```

Copy the PC directory too. Note roughly when control stopped and whether pedals
were held, without sharing QR/session keys. Preserve these files before repeated
tests rotate them out. A shutdown/focus-loss incident is not an in-race dropout.

## Reproducible verification

```powershell
dotnet run --project windows/tests/PhoneWheel.Tests -c Release
cd android
.\gradlew.bat testDebugUnitTest assembleDebug assembleDebugAndroidTest lintDebug
cd ..
python tools/test_usb_transport.py --receiver release/receiver/PhoneWheel.Receiver.exe
python tools/test_wifi_recovery.py --receiver release/receiver/PhoneWheel.Receiver.exe
python tools/test_controller_soak.py --host <PC-LAN-IP> --serial <ADB-serial> --seconds 600 --direct --receiver release/receiver/PhoneWheel.Receiver.exe
```

The phone soak requires the instrumentation APK and a paused game; its Receiver
always uses `--backend null`. It holds a real test touch at 50% throttle, uses the
phone's real sensors, and releases/re-arms normally after a fault. It does not test
actual ViGEm/F1 rendering or physical steering motion. `--faults` replaces direct
mode with a metadata-only UDP proxy: 60 ms isolated packet delays, two-packet
loss bursts, 80 ms isolated STATUS delays, duplicates/reorder, and intentional
1.5 second full interruptions. Real interruptions are expected to disarm safely.

If Python is blocked by a firewall, do not disable the firewall or change network
profiles. `run-controller-fault-soak.ps1` requires explicit authorization and
administrator rights; it temporarily permits only the named phone/UDP port for
the selected Python executable and restores its previous UDP block in `finally`.
Verify restoration after use, especially if the shell was forcibly terminated.

Synthetic tests also cover sustained stale ACKs, sensor timestamp anomalies,
output write exceptions, temporary transport interruption/reconnect, and held
throttle/brake/steering. Hardware observations and limitations belong in the
validation report; passing these tests alone is not a "fixed" claim.
