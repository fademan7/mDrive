# 0.5.15 validation — 2026-10-14

## Confirmed evidence

- Existing real-play PC incident `controller-20261013-155109-896-0e6c8845ef064893973c35c837f680af.json`: receive gap 161.561 ms, Timeout -> Recovering -> Timeout -> User. Fresh valid packets returned at 262134 ms but the recovery eligibility deadline had already expired; ARM then fell from flags 31 to 30. This confirms the amplification into neutral re-arm in this incident, not the underlying Wi-Fi scheduling cause.
- Other recent captured timeout recovered in 12.826 ms. Three inspected incidents contained no OutputError/OutputStall; maximum recorded native write 0.5034 ms. Diagnostic buffers are finite, not continuous proof of all driving.
- Rumble default was false with no current UI enabling it. Enabled the default and added level-change updates. User confirmed phone vibration, but initial amplitude-only levels were hard to distinguish; final patterns additionally vary duty cycle.

## Changes

- SafetyGate network-only recovery eligibility survives late proof/authenticated reconnect. Neutral output at 150 ms unchanged. Every resumed CONTROL still requires <=100 ms post-expiry challenge, fresh sequence, same epoch, READY/ARM/FastRecovery. HELLO clears challenge history, never authorizes output. Sensor/focus/epoch/ARM/output faults still revoke eligibility. Legacy peers still use neutral re-arm.
- Android retains live ARM intent over missing ACKs, never restores old controls/calibration after lifecycle loss. Closed sockets are replaced immediately on the receive worker; silence/ordinary socket timeouts retain the existing backoff.
- Radio/audio/microphone test variants, pit questions containing damage/tyre context, narrow damage spelling correction. Specific part queries no longer append unrelated full damage reports; ambiguous wing asks front/rear. Full reports require all/full/complete. Pit advice uses fresh damage/wear/weather/window, no optimal-strategy claim.
- Non-speaking AUTO noise no longer postpones briefings. Quiet useful briefing eligibility: >=45 s since previous alert/session and >=12 s since spoken reply, fresh race data, green flag, out of pits, driving with low steer/brake. Urgent alerts retain priority. Loading microphone displays PROCESSING, not ready IDLE.
- No LLM/GPU expansion, game-setting writes, firewall changes or recorded voice/audio.

## Tests

- Windows 49/49 groups passed (local socket tests rerun outside sandbox network restrictions).
- Android 57/57 unit tests, APK/test APK and lint passed. Existing deprecated ZXing API warnings remain.
- Extended held steering/throttle/brake proof tests: 170 ms and 300/1000/5000/30000 ms outages; stale/unknown challenge, duplicate/out-of-order, epoch/READY/ARM/output failure isolation, legacy behavior, no old-value replay.
- Actual SM-S942N + Wi-Fi + null receiver: held throttle 50%, closed transport fault, no finger release, same calibration, active recovery. Initial implementation: 2189 ms to recovered ACK/state observation; after immediate closed-socket replacement: 559 ms (includes waiting for >5 ACKs/UI state), max ACK gap 405 ms, max send gap 11 ms, one Timeout release, zero null-output errors. A retry before the second run stopped at initial calibration because the phone was not centered; it did not test recovery.
- Haptic test submitted all three game-rumble levels on device; user confirmed vibration. Not an F1/ViGEm feedback end-to-end test.
- Final duty-cycle pattern APK was rebuilt, unit/lint checked, reinstalled and the device test repeated successfully. Physical distinction of final levels still awaits user comparison.
- Normal installed receiver reopened using Computer Use and verified: PC input/output, volume 100, Wendy OFF, QR waiting, zero output errors. Phone returned to app; new QR scan and Wendy ON are user steps.
- Installer built (first transient resource-update failure, unchanged retry succeeded), installed PC 0.5.15.0 with exit 0. No driver replacement/reboot/security changes.

## Not yet confirmed

- Underlying cause of the real 162 ms delivery burst (phone scheduler vs Wi-Fi/AP vs Windows) needs correlated Android flight data.
- Final F1 long-drive recovery, headset recognition improvement, proactive speech frequency and game-provided kerb/lock/engine rumble remain unverified. No fabricated telemetry lock/kerb detector was added. The game must supply rumble and have vibration enabled.
- Network loss cannot be made physically instantaneous or lossless; stale inputs are still neutralized. This is not a claim that every controller dropout is fixed.
