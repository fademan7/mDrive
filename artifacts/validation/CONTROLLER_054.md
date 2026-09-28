# mDrive 0.5.4 controller dropout validation

Date: 2026-09-26/27 (KST). Status: **not yet confirmed / not fixed** for the
reported F1 driving problem. Changes are restricted to controller reliability,
diagnostics and tests; no Wendy feature work.

## 1. Confirmed observations and root-cause limits

The 600-second Fold5 → actual Windows Receiver direct Wi-Fi test reproduced
**two un-injected SafetyGate timeouts**, at approximately 28 and 332 seconds into
the held-throttle test. This was a null-output test with Wendy disabled, not F1.
The app remained focused and sensor-valid. Neither case was an output exception.

Paired phone/PC incident records establish:

| Observation | First incident | Second incident |
|---|---:|---:|
| PC control receive gap | 152.3901 ms | 154.6326 ms |
| Phone maximum send gap in captured window | 10 ms | 10 ms |
| Phone maximum sensor age in captured window | 15 ms | 17 ms |
| Phone maximum STATUS gap in captured window | 152 ms | 164 ms |
| PC release reason | Timeout | Timeout |
| PC neutral-to-rearm elapsed after release (test releases pointer) | 830.6959 ms | 818.1617 ms |

Phone sequence 3125 was emitted 9 ms after 3124, and sequence 36038 was emitted
9 ms after 36037. PC received each after the corresponding 152/155 ms gap,
followed by a burst of queued frames with stale challenges. Output and STATUS
events continued during the gaps. This narrows these two incidents to transport
delivery/receive scheduling below or at UDP reception, not the Android periodic
sender, sensor freshness, focus loss, or a whole-PC output stall. It does not
separate phone Wi-Fi, AP, PC adapter/driver or receive-thread scheduling.

The two gaps were about 304 seconds apart; periodic radio/driver behavior is a
hypothesis, not a confirmed diagnosis. The phone's existing low-latency Wi-Fi lock
was held. The PC adapter reported Power Saving=Auto. No adapter setting was
changed during this baseline, and no WLAN disconnect event was returned in the
queried test interval. Absence of an event does not exclude radio-level delay.

The reproduced timeout correctly enters neutral at the existing deadline.
Holding a pedal instead of releasing it would lengthen the re-arm wait. This
explains how a short transport pause can become a longer user-visible loss, but
does not establish that the user's original F1 incidents had the same cause.

## 2. Code changes

- Android: `ControllerFlightRecorder.kt`, `MainActivity.kt`,
  `UdpControllerClient.kt`, version metadata in `app/build.gradle.kts`.
- PC Core: `ControllerFlightRecorder.cs`, `SafetyGate.cs`, `CriticalLoop.cs`,
  `GamepadOutput.cs`; Host: `Program.cs`, `ReceiverTransport.cs`.
- Tests: Android `ControllerDropoutTest.kt`, `ControllerSoak.kt`, existing
  instrumentation routing; Windows `DropoutTests.cs`, test project/runner;
  `tools/test_controller_soak.py`, scoped firewall test wrapper.
- Documentation: README and `docs/CONTROLLER_DROPOUT.md`.

Sensor timestamp validation now discards isolated duplicate/out-of-order/future/
already-stale events before they can reset the estimator. Snapshot publication
is serialized. PC UDP receive and send no longer await shared-pool continuations.
The bounded metadata flight recorders capture pre/post incidents off the critical
path. They do not contain voice text, audio, IPs, device IDs or pairing secrets.

## 3. State transitions

Previously a duplicate sensor timestamp could clear calibration and require
calibration plus neutral re-arm. Now it is ignored without refreshing the last
valid sensor timestamp. Continued invalid samples still expire normally.

Packet loss/rejection within the valid-input deadline already left SafetyGate
armed before this change. This was retained, not newly introduced. Missing valid
input for 150 ms, lost READY/focus, reconnect and output errors still require
neutral/re-arm. No automatic replay of held throttle was introduced.

## 4. Safety

Challenge age remains 100 ms, valid-control timeout 150 ms, sensor freshness
100 ms, Android neutral dwell 700 ms and PC neutral dwell 300 ms. History retained
for diagnostics is not an expanded authentication/acceptance window. True loss
neutralizes output. A hung native ViGEm call cannot be forcibly cancelled; software
watchdogs do not guarantee a driver can physically complete a neutral write.

## 5. Before/after reproduction and builds

- Old sensor estimator behavior reproduced in unit test: duplicate timestamp
  clears calibration; guarded path preserves calibration. Real-phone occurrence
  of that timestamp fault has **not** been observed in this run.
- Forced shared-pool saturation blocks a pooled continuation beyond 160 ms;
  dedicated loopback UDP receive still completes. Actual F1 thread-pool starvation
  is **not** established.
- .NET: **33/33 groups passed**. Android: **46 tests, 0 failures/errors**.
- Android debug APK/test APK and self-contained Windows x64 Receiver built.
Final Android lint: **0 errors, 72 warnings**.
- USB regression passed (partial-frame neutralization measured **159.023 ms**
  including OS scheduling/polling); malformed/fragments/reconnect/held input and
  neutral re-arm covered. UDP endpoint/authentication/reconnect regression passed.
- New direct 10-minute real-device candidate run: completed, **2 unexpected
  timeouts remain**. There is no comparable pre-change 10-minute hardware run,
  so no before/after hardware improvement percentage is claimed.
- Synthetic tests include jitter, temporary loss, stale/delayed ACKs, duplicate/
  reordered packets, sensor timestamp anomalies, write exceptions and held
  throttle/brake/steering during faults. Physical steering motion and actual F1
  gameplay were not exercised by the automated phone test.

## 6. Maximum gaps

Direct real-phone run: sender **18 ms**, STATUS **164 ms**, PC receive **155 ms**
(UI rounding; incident maximum 154.6326 ms), completed null write **0.5 ms**.
Maxima use separate monotonic clocks, not cross-device one-way latency estimates.
PC receive maximum includes rejected arrivals; inspect accepted age separately.

## 7. Releases

Direct run: **2 × Timeout**; app releases at 28013 ms and 332094 ms. ACK challenge
reject total **229**, not 229 releases. Instrumentation releases the held pointer
and waits for the normal safety handshake before creating a new touch.

## 8. Output errors and overhead

Direct run: **0 null-backend output errors**, sender failures **0**. This does not
verify ViGEm under game load. Fault-injection unit tests exercise exception recovery.
PC recorder: **315 dropped diagnostic entries**, **0 save errors** in the final
monitor sample; producer drops are intentional instead of waiting for the writer.

During the direct run, Receiver working set was about **57.3 MiB**, private bytes
**27.6 MiB**, accumulated CPU **6.27 seconds** after roughly 8 minutes. Phone total
PSS was about **98 MiB**. These include the app/test process and are observations,
not isolated before/after recorder overhead measurements or game-load benchmarks.

## 9. Remaining unknowns / next evidence

Actual F1+ViGEm incidents still need the paired incident files. Radio/AP/adapter
versus receive scheduling needs additional controlled comparison. Do not label
the candidate fixed, or increase deadlines to hide the reproduced gaps.

Local raw direct-run evidence (ignored by Git):
`artifacts/dropout/20260926-235743/{summary.json,pc,phone}`.
An earlier proxy attempt could not arm because Python UDP was firewall-blocked;
it is an invalid connectivity setup trial, not a dropout result.

Installed candidate SHA-256:

- APK: `FF1C5AB9AE8478562B5181023DB5C7F9FF2124C6E60795246F45F93BFEC0887E`
- Receiver EXE: `763DB7B030CC3E9362031286DCCA675DF1FF473F7E4C6945D4702BF3CD5F47D4`

## Fault-proxy hardware run (additional 600 seconds)

Actual Fold5 and Receiver, null backend, same Wi-Fi; narrow temporary Python UDP
allowance only. **5 intentional 1.5-second interruptions → 5 Timeout releases**.
All five release-count changes occurred in the interruption phase. No extra
release occurred during the other phases in this run.

- 64,395 controls forwarded/considered, 20,501 STATUS packets.
- 542 isolated controls delayed 60 ms, 904 controls dropped in two-packet bursts,
  288 isolated STATUS packets delayed 80 ms, 542 duplicate/reordered controls.
- Sender maximum **19 ms**, PC receive maximum **1547 ms** (includes deliberate
  full outages), phone STATUS maximum **1551 ms**; sender failures **0**.
- Null output errors **0**, completed write maximum **0.5 ms**; ACK rejects **153**.
- Recorded PC re-arm after the first four deliberate releases: **2.078–2.143 s**,
  including the remaining outage and ordinary neutral handshake. Instrumentation
  observed host-active re-arm on all five before creating a fresh touch.
- PC recorder final sample: **370 dropped entries**, **0 save errors**.
- Four full PC incident captures survived. The fifth happened at the end of the
  test and the test process terminated before the 3-second post-window finished;
  its release remains in the summary/counters, not a complete fifth trace. This
  is a test shutdown limitation, not evidence that the fifth interruption did not
  happen. Android pull contains the prior direct incidents plus four fault traces.

Raw evidence (ignored by Git): `artifacts/dropout/20260927-000750`.
This different run does **not** overturn the two spontaneous direct-run timeouts.

Firewall restoration independently verified: the original Python TCP and UDP
Block rules are enabled; **zero temporary test Allow rules remain**. Firewall was
never disabled; network profile and PC adapter power setting were not changed.
The development-only `dev.phonewheel.test` APK was uninstalled; the updated main
APK and diagnostic Receiver remain installed. Removing the test APK is reversible
by rebuilding/reinstalling it and does not remove the main app or its settings.

After the diagnostic pull and test-APK removal, USB debugging became unavailable;
automatic foreground launch of the phone app could not run. The normal Wi-Fi
Receiver was started for new QR pairing. Phone app reopening/scanning is manual.
