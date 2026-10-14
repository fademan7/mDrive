# Receiver 0.5.13: offline English speech and installer validation

Final verification: 2026-10-08. Android remains 0.5.12; no Android changes were required for this distribution update.

## Deliverable

- `release/installer/mDrive-0.5.13-Setup-x64.exe`
- Size: 81,578,277 bytes.
- SHA-256: `21B474EAFF9CDBF14DECE56CE9EA9246912743104E1420F78169678D6A796E4A`
- Includes the self-contained receiver, Vosk small English 0.15 model, third-party notices, and original signed ViGEmBus 1.22.0 installer.
- Preview, not an Authenticode-signed general release. Application signing, clean-machine validation, and commercial installer-tool licensing review remain necessary before general distribution.

## Implementation boundaries

Vosk microphone capture and native recognition run in a separate low-priority process. PCM is held only in bounded memory and is not sent over IPC or persisted. The parent receives recognition results/status, not audio. Epoch changes discard old recognition results. Audio discontinuities invalidate the utterance instead of allowing a partial stale command through. Wendy OFF releases the recognition process/model.

The speech-only buffer is limited to ten 50 ms frames with a 500 ms freshness bound. This does **not** change controller freshness, recovery, ACK validation, transport, or gamepad output thresholds. Controller freshness remains 150 ms. PC/Android input/output routing, volume and LB radio priority remain supported. English speech output still uses an installed Windows English TTS voice; the installer does not silently install Windows language features or change microphone/firewall permissions.

## Synthetic speech comparison

These measurements use generated English TTS, not the user's microphone/accent or F1 noise. Peak memory includes benchmark/TTS/runtime overhead and is not an isolated production-engine measurement. Single-host measurements are not universal performance guarantees.

| Model | Batch intent matches | Batch real-time factor | Peak process working set |
|---|---:|---:|---:|
| 128 MB English lgraph | 10/10 | 0.3532 | 505.0 MiB |
| 40 MB small English | 10/10 | 0.07845 | 252.6 MiB |

Both batch silence checks returned no text. Batch success did not imply real-time success:

- Initial 250 ms speech buffer: 128 MB model caused five resets, with a 2,136.8 ms maximum decoder call; it lost utterances. Small model caused one reset during startup, with a 245.5 ms maximum call.
- Final 500 ms speech-only buffer and small model: zero resets; maximum decoder call 235.3 ms; all three real-time phrases recognized: “radio check”, “tell me the time gap”, “any front wing damage”.
- The small model is the shipped default. The larger comparison model is not included in the installer.
- Decoder latency is **not** controller latency. Actual headset/F1 load validation remains outstanding.

## Build and regression

- Release self-contained single-file receiver publish: passed.
- Inno Setup installer compilation: passed.
- Final Windows regression run: **46/46 groups passed**.
- Included controller jitter/loss tests: maximum simulated receive gap 100 ms, zero safety releases, one rejected ACK.
- Included 170 ms outage test: neutral at 150 ms; fresh-input recovery at 170 ms, no stale replay or neutral dwell.
- Existing gamepad output exception, replay rejection, recovery, recorder, four audio routes and LB priority tests passed.
- New tests cover bounded PCM, overflow, freshness, epochs and minimum word confidence.
- Android tests were not rerun for this receiver-only update.

## Installer smoke test

Installed with `/CURRENTUSER /VERYSILENT /NORESTART` into the previously nonexistent exact test directory `C:\paper\mdrive\artifacts\setup-smoke-0513`.

- Install exit code 0; installed receiver version 0.5.13.0.
- Installed `--voice-probe`: offline English model installed; one English output voice available.
- Installed real native worker test: idle-mode epoch acknowledgements and EOF shutdown passed. This test never opened the microphone or gamepad.
- Installer detected the existing ViGEmBus service/file and retained it; driver install was skipped. No reboot required.
- Official uninstaller exit code 0. Test directory, test uninstall registry entry and test Start Menu shortcuts were removed. Shared driver service/file remained present. No user preference cleanup was requested or performed.
- Test install/uninstall logs are in `artifacts/setup-smoke-0513-install.log` and `artifacts/setup-smoke-0513-uninstall.log`.

## Not yet verified / release limitations

- Real headset speech, accents, game audio and extended F1/controller coexistence were not tested in this update.
- Fresh Windows installation with no ViGEmBus, missing English TTS, an older/broken existing driver, upgrade and reboot paths need separate testing. Service/file presence is not a driver health guarantee.
- ViGEmBus is end-of-life; future Windows/game anti-cheat compatibility is not guaranteed.
- The new Audio check button was functionally implemented but its final GUI appearance was not visually reviewed in this pass.
- The installer/application are unsigned; the bundled upstream driver installer has a valid publisher signature and pinned SHA-256 verification.
- No existing receiver was deliberately stopped or replaced, and no microphone recording or real F1 command was performed. Final process inventory showed no receiver running.
- No commit, push, public upload or Android installation was performed for this update.
