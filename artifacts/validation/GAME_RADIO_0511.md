# 0.5.11 — game radio owns LB (2026-10-07)

## Implemented

- Removed OCR reader/navigation/self-test sources, setup window, build dependency
  and experimental launcher. Production Wendy no longer constructs menu output.
  Pre-existing system Tesseract installation was not uninstalled; archived local
  experiment build/cache files are not used by the current receiver.
- LB keeps bit 0x0100. The controller snapshot is published before the independent
  Wendy callback, just as before. No injected T key or phone-audio forwarding.
- LB down invalidates recognizer callbacks, cancels recognition/TTS, destroys the
  recognizer, clears unsent Wendy requests and blocks automatic/manual listening.
- While held, and for 8000 ms after release, local UI shows GAME RADIO. A new press
  extends the pause. Actual F1 menu closure is not observed. AUTO resumes afterward
  only if its existing enabled/foreground/connection/retry conditions allow it.
- Same Wendy TCP socket stays open. Optional authenticated gameRadio flag suppresses
  server classification/replies and invalidates prior conversational approval work.
  PC status shows GAME RADIO rather than reporting a microphone fault.
- Removed LB-based Wendy PTT selection from options; user-facing Wendy is AUTO.
- Existing 120-degree linear steering and larger default buttons retained.

## Verification completed

- Android offline unit tests / debug APK / instrumentation APK / lint: successful;
  **51 unit tests, 0 failures, 0 errors**. Existing QR scanner deprecation warnings.
- Windows Release test suite: **44/44 groups** including controller timing,
  loss/recovery, held inputs, output exception and authenticated Wendy conversation.
- Added hold/release/repeated-LB grace tests; same-socket radio mute/resume test.
- Published self-contained default receiver to `release/receiver`, version 0.5.11.
  No OCR/traineddata payload present in that distribution.
- Installed versionCode 23 / 0.5.11 and test APK on connected SM-S942N.
- Physical-device detached UI test passed: both pedals held at 50%, all 12 original
  button bits, LB callback pauses Wendy without starting recognition, right stick,
  cancellation. No controller packets/game inputs or microphone were used by this test.
- SafetyGate, GamepadWorker, ReceiverTransport and Android UdpControllerClient
  have no changes in this working-tree task set. Existing deadlines are unchanged.

## Not verified / limits

- No new live F1 speech-command run or long driving/latency soak in this turn.
- The real F1 radio must be mapped to LB and use its own PC microphone. This change
  neither opens the radio with a keyboard macro nor makes Android the game's microphone.
- The eight-second grace is not menu-state verification. If speaking longer, hold
  LB or press it again. Direct keyboard T outside the app does not trigger this guard.
- Old untracked experiment source removal is not a Git revert; historical test
  notes are retained, but the withdrawn OCR source is no longer in the workspace.
