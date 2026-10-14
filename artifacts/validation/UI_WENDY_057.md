# 0.5.7 UI / Wendy verification — 2026-09-28

## Scope and result

APK updated successfully on Galaxy Z Fold5. Receiver published and deployed;
the previous EXE is retained as `release/backups/PhoneWheel.Receiver-before-057.exe`.
Final normal Receiver launched with Wi-Fi/QR/Engineer enabled; direct setting
shortcuts remain OFF. New QR pairing is required after restart.

**Actual F1 action success and mixed keyboard/gamepad behavior: NOT YET CONFIRMED.**
F1 was not running during the checks, and the user confirmed that BB/Diff keys
were not assigned. No real F1 key or pit action was sent. User was asked to
prepare a practice session and assign four direct shortcuts before that stage.

## Builds and automated regression

- Android: `testDebugUnitTest assembleDebug assembleDebugAndroidTest lintDebug`
  offline build PASS, **48 unit tests**, 0 failures/errors. Lint completed;
  existing-style hardcoded-English and scanner deprecation warnings remain.
- Windows: Release tests **42/42 groups PASS**; self-contained win-x64
  single-file publish PASS.
- Python reference model: **37/37 PASS** (reference, not native hardware proof).
- Existing controller loss, jitter, duplicate/out-of-order, ACK rejection,
  post-expiry proof recovery, output exception and isolated receive tests remain.
- New tests: exact/relative/ambiguous numbers; one shortcut pulse then newer
  matching telemetry; no ACK/no retry; native-output mock failure; disabled
  keys; session/reconnect cancellation; conflicting manual changes; stale
  approval on session/reconnect/expiry/setup/temperature/invalid lap; bounded
  advisor evidence; clean-lap tyre ranges, pit priority and stale HUD; Race
  geometry and stable switch target.

SafetyGate, ACK/freshness timeouts, sender and gamepad worker are unchanged.
The only controller receive-loop addition is a rare atomic notification on an
authenticated HELLO to invalidate optional Wendy actions; no locks/native work.

## Actual Fold5 Wi-Fi — null gamepad backend, not F1

`tools/test_phone_isolation.py` (microphone opt-in NOT used):

- Real held throttle 50%, Race/Menu switch button tapped **20 times**.
- Settings/Wendy mode changes and English TTS while pedal remains held.
- Same controller object, ARM and calibration epoch; no transient pedal release.
- ACK count 349; maximum send gap **12ms**, maximum status/ACK gap **38ms**.
- Receiver release count **0**, output errors **0**, ACK rejects **0**.
- TTS speaking observed; held pedal released correctly at test end.
- This tests real touch dispatch and Wi-Fi, not dynamic steering in a live game.

`tools/test_controller_soak.py --direct --seconds 180`:

- Held-input real Wi-Fi **180 seconds**, ACK count **5839**.
- Android disarms **0**, send failures **0**, release events none.
- Maximum send gap **14ms**, status/ACK gap **37ms**.
- Receiver maximum receive gap **20ms**, null output write **0.3ms**.
- Receiver releases **0**, ACK rejects **0**, output errors **0**.
- Flight recorder contention dropped **105 diagnostic records**, save errors 0;
  these are diagnostic samples, NOT dropped controller packets.
- Raw metadata report: `artifacts/dropout/20260928-190809/summary.json` (ignored
  runtime artifact). No pairing keys, transcript, audio or addresses in report.
- No firewall changes, network profile changes, or fault proxy were used here.

The final PC build additionally includes optional-action cancellation/report
guards and bounded Wendy response serialization. They do not change controller
code; final synthetic tests/publish pass. The 180s soak is not a long-race or
actual ViGEm-in-F1 certification. Earlier 0.5.6 dropout limitations remain open.

## Supported versus pending

See `docs/WENDY_057.md`. Race/Menu UI and summary are implemented. BB/Diff direct
commands are conditional on user-verified game bindings, disabled by default,
and not real-game verified. Box remains an explicit reminder, not execution.
Next-stop tyres/wing and optimal strategy remain unsupported. Advisor uses
repeated driver reports corroborated by phase/conditions, NOT autonomous slip
detection. No new inference model or screen-reading feature was introduced.
