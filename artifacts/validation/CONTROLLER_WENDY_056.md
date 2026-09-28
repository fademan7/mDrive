# 0.5.6 — bounded controller recovery and spoken race assistance

Date: 2026-09-28. **Actual F1 fix status: not yet confirmed.**
Code/build verification and the authorized Fold5 null-output tests are complete;
in-game validation remains separate. Existing user changes were preserved.
No commit/push or game-setting automation. A scoped, authorized temporary UDP
firewall exception was used for injection and its restoration was verified.

## 1. Confirmed cause and remaining uncertainty

Eight actual PC incident captures and eight matching phone captures were copied
to ignored `artifacts/dropout/20260928-user` before further testing.
After deduplicating overlapping captures: five Timeout releases, two User
releases, one Calibration release. All five timeout windows contain old
challenge rejections and approximately 149–156 ms receive gaps. Android sender
and sensor samples continued normally; no focus loss in these captured windows.

The amplification mechanism is confirmed in those logs: 150 ms accepted-input
expiry disarms the legacy gate; even after valid packets return it waits for
neutral + ARM edge. Several captures have no re-arm within their three-second
post-window. One re-arms about 837 ms after expiry. This does NOT prove the
underlying radio/driver/OS reason for the initial gap. Precise network-layer
root cause remains **not yet confirmed**.

For the five timeout incidents, an accepted packet acknowledging a PC challenge
sent after the recorded release arrived 36.65 / 28.92 / 27.40 / 31.84 / 31.92 ms
after release. This is evidence that a fresh round trip returned promptly, NOT a
measurement of the new version's in-game recovery (old app ARM behavior differs).

## 2. Changes

- `SafetyGate.cs`: capability-gated RecoveryHold; post-expiry round-trip proof;
  hard-fault exclusion; unchanged legacy behavior.
- `Pwr1Codec.cs`, Android `Protocol.kt`, `UdpControllerClient.kt`: bit4 capability
  and status reason9. APK and Receiver must be upgraded together.
- `ControllerFlightRecorder.cs`: RecoveryHold, RecoveryResume, RecoveryProof
  rejection. Existing RAM ring and incident-only disk writes retained.
- `AutoDrive.kt`, `MainActivity.kt`: retain current ARM intention/calibration only
  during fresh Recovering status; keep sensor/foreground/focus/STATUS guards.
- `GravityCenter.kt`: automatic roll reference follows gravity and display
  orientation, not a currently turned phone. Flat/inverted ambiguity fails closed.
  Existing manual center remains available; no continuous recentering in corners.
- `Program.cs`: compact recovery status, no request to release controls during
  proven brief-jitter recovery.
- `F1RaceState.cs`, `WendyDamage.cs`, `WendyEngineer.cs`: named damage percentages,
  wheel/brake/blister values, separate fault bits/component wear, conservative pit
  guidance, escalating per-component warnings. No damage-panel referral.
- `WendyNaturalLanguage.cs`: polite OK/okay/alright prefix, including OK box box.
- Quiet-interval radio: after 60 seconds without a reply/alert, on a green-flag
  straight with fresh telemetry, rotate gaps/tyre wear/fuel. Same topic cooldown
  three minutes; flags and existing important alerts have priority. No filler
  when telemetry is absent, braking/turning, or in the pit lane.
- Tests, protocol/README documentation, null-receiver soak harness brief-outage
  option and recorder post-window drain.

## 3. State transitions and safety

| Condition | 0.5.6 behavior |
|---|---|
| Valid continuous packets / isolated rejection | Existing direct output path; no added buffer or smoothing |
| Previously armed capable client reaches 150 ms expiry | Immediately neutral; RecoveryHold, Armed=false |
| New valid CONTROL ACKs a challenge issued at/after expiry, before expiry+100 ms | Output that new packet; RecoveryResume; no pedal-release/center dwell |
| Queued pre-expiry challenge, stale ACK, duplicate/reorder | Reject; cannot refresh watchdog/recovery deadline |
| Recovery window expires, HELLO reconnect, epoch/READY/ARM change, output error | Hard disarm; ordinary neutral re-arm |
| Android sensor/focus/lifecycle or STATUS freshness fails | Local release retained; no auto replay |

150 ms output expiry, 100 ms challenge/sensor freshness, Android 200 ms STATUS
guard and hard re-arm dwells are not enlarged. The bounded proof path is a new
re-arm policy, not a claim that all safety behavior is identical. It permits
continuing held **current** inputs only after a new same-epoch authenticated
round trip; it never intentionally holds an old output through expiry. The OS
is not real time; native driver stalls cannot be forcibly made safe in software.

## 4. Before/after reproduction and tests

- Legacy synthetic 150 ms expiry + held input: neutral, cannot re-arm until
  release/dwell/ARM (still passes as backward-compatibility test).
- New synthetic 170 ms gap: neutral at150 ms, fresh proof/input at170 ms restores
  output, no multi-second dwell. Separate held steering/throttle/brake cases.
- Pre-expiry proof, repeated rejected proof, 250 ms boundary, late watchdog,
  output exception, sensor/focus/ARM loss, epoch change and HELLO cannot bypass
  hard disarm. Existing loss, delayed ACK, duplicate/order, sensor timestamp,
  output failure, thread-pool saturation and independent output tests retained.
- PC regression: **39/39 groups PASS**.
- Android JVM: **46 tests, 0 failures/errors**.
- Android debug APK + instrumentation APK build: PASS.
- Android lint: **0 errors, 74 warnings** (including existing literal-English UI
  resource/deprecation recommendations).
- Windows self-contained win-x64 single EXE publish: PASS.
- No new-version real F1 endurance claim. New gravity math verified for four
  display orientations, tilt and ±140°; physical phone confirmation still needed.

## 5. Observed metrics (old actual incident captures only)

- Maximum Android send gap: **16 ms**.
- Maximum PC receive gap: **155.529 ms** (includes rejected arrivals).
- Maximum Android STATUS gap: **163 ms**; sensor age: **19 ms**.
- SafetyGate releases: **5 Timeout + 2 User + 1 Calibration**, deduplicated.
- Gamepad/native output errors: **0** in these captures. Timeout-capture maximum
  output write latency below **0.72 ms**. Not proof against uncaptured stalls.
- Some diagnostic entries were dropped by design rather than blocking input.
  The recorder is not a complete packet trace and cannot establish one-way delay.

## 6. Deferred by explicit user choice

Actual numeric setup changes, automatic minor under/oversteer correction, pit
request execution, next-stop tyre selection and full strategy optimization are
all the **last stage**. See [WENDY_FINAL_STAGE.md](../../docs/WENDY_FINAL_STAGE.md).
Box box currently records a reminder and explicitly says no game command is
sent. Damage/pit advice is heuristic, not a verified optimal pit/repair model.

## Sources

- [EA F1 25 UDP v3](https://forums.ea.com/t5/s/tghpe58374/attachments/tghpe58374/f1-games-game-info-hub-en/61/4/Data%20Output%20from%20F1%2025%20v3.pdf): damage fields, fault/wear distinction; telemetry output specification, not a setting-write API.
- [Android sensor coordinates](https://developer.android.com/develop/sensors-and-location/sensors/sensors_overview): natural-device axes do not rotate with the screen; display rotation must be accounted for.

## Deployment

Candidate APK: `android/app/build/outputs/apk/debug/app-debug.apk`, version0.5.6
(18). Candidate Receiver: `release/receiver-056/PhoneWheel.Receiver.exe`.
At report creation, production Receiver/app are **not replaced**; waiting for
the user's game-paused approval. No active connection has been stopped.

### Deployment and hardware test update — completed later on 2026-09-28

The user approved testing. APK0.5.6/code18 was installed successfully, along with
the development-only instrumentation APK. Receiver replaced with the matching
0.5.6 binary; prior EXE preserved at
`release/backups/PhoneWheel.Receiver-before-056.exe`. At completion the regular
Receiver was restarted (PID47704 observed alive, owning UDP26760), and the phone
app was opened. Re-scan the new Receiver QR; the test session is no longer valid.

Final SHA256:

- APK: `30D0DAC0258D80ED7DCA34770BEDF540C5F5AD0C890565742D1308C0D9B170D0`
- EXE: `25A4E63153044EFE44E329CE51E4B98B61416129C5E798D152A431325FA1EA38`

The first attempt could receive Wi-Fi but timed out waiting for initial neutral
arming, before the user confirmed an upright/level phone. After that preparation,
automatic gravity centering and initial arming succeeded. This is not a physical
±90°/180° steering accuracy measurement. The failure is preserved in
`artifacts/dropout/20260928-013512`; it is not counted as a completed driving run.

#### Direct actual Wi-Fi, 300 seconds — qualified success

`artifacts/dropout/20260928-013641/summary.json` and `pc/*.json`:

- Real Fold5 sensor + held 50% pedal touch, **null backend, no F1 input**.
- Android hard release/re-arm: **0**. Send failures: **0**. STATUS count:9810.
- Send gap max16ms, STATUS gap max164ms, receive gap max154.707ms.
- One naturally occurring Timeout/RecoveryHold at153703.087ms, followed by
  RecoveryResume **25.207ms** later. First neutral write followed the release by
  0.052ms; measured neutral-to-nonneutral output interval **30.335ms**.
- No old-output hold through expiry; one pre-expiry recovery proof was rejected.
- SafetyGate release count1, reasonTimeout; all recovered without Android
  dropping ARM. Output errors0, monitor maximum write latency rounded0.7ms.
- ACK rejections209 over the run;77 in the retained incident window. Network
  jitter still exists. This is evidence against multi-second amplification for
  this specific real incident, not proof of a complete F1 fix.

#### Fault injection, 180 seconds — remaining boundary failure reproduced

`artifacts/dropout/20260928-014210/summary.json`, `pc/*.json`, `phone/*.csv`:

- Six160ms two-way interruptions,212 isolated60ms control delays,298 dropped
  controls,54 isolated80ms STATUS delays,108 duplicates/reorders, one1.5s outage.
- **Five of six brief outages** recovered on fresh proof without pedal release:
  release-to-resume28.206 /26.476 /29.236 /29.889 /32.765ms.
- **One brief outage did not fast-recover:** combined delay produced221ms
  STATUS gap. Android observed210ms since STATUS and lowered ARM before the
  Receiver could use the new proof. PC saw the fresh proof at51.683ms after
  release, but ARM was already0. It required ordinary re-arm about800.629ms
  after release (the harness releases the held pedal automatically).
- The intentional1.5s interruption safely released; normal reconnect/re-arm
  took2175.052ms from PC release. Held input did not bypass hard re-arm.
- Android hard releases2: one boundary failure + one expected real interruption.
  Send failures0, max send14ms, max STATUS1507ms, max receive1517.022ms.
- During measured driving:7 SafetyGate Timeout releases,5 fast resumes; output
  errors0, monitor maximum write latency rounded0.3ms. One additional Timeout
  capture after instrumentation shutdown is teardown, not an eighth driving
  failure. Do not count it as an injected outage or hide it from raw records.
- The harness exit0 means execution completed, **not all faults met the desired
  recovery behavior**. Final reliability conclusion remains **not yet confirmed**.

Separate actual loopback UDP integration test passed: active peer protected,
stale peer reauthenticated, held throttle blocked, neutral re-arm succeeds.

The 200ms Android STATUS supervisor versus PC's bounded post-expiry proof window
is a confirmed remaining amplification boundary. No timeout was enlarged to hide
it in this test turn. A follow-up design must align recovery intention with the
receiver's authoritative fresh-proof decision without holding stale game output
or bypassing true loss, sensor, lifecycle, or native-output failures.

Firewall: allowed only this phone, Python UDP26760 on the current Public network;
original Python UDP block restored, original TCP block unchanged, temporary rule
count verified0. No global firewall/profile changes.

Limitations: stationary-phone/null-output runs total8 minutes, not actual F1
endurance or ViGEm/game-load/physical steering/voice-content validation. Wendy
damage/briefing behavior remains covered by the39-group code regression, not a
new real-race spoken trial. Do not label this version fully fixed.
