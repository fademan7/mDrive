mDrive 0.5.17 - Preview for Windows x64
Created by fademan7 / neojshin
https://fademan7.github.io/
neojshin@gmail.com

mDrive is free software under GNU GPL version 3 only (GPL-3.0-only),
without warranty. See notices/mDrive-GPL-3.0.txt and notices/COPYRIGHT.
Matching source and build scripts: https://github.com/fademan7/mDrive/tree/v0.5.17
Third-party components retain their own licenses.

This installer includes the receiver, an offline English Vosk speech model,
and the original signed ViGEmBus 1.22.0 installer. The existing driver is kept
if its Windows service and driver file are present. No driver is uninstalled
when removing mDrive, because other applications may share it.

ViGEmBus is end-of-life. No future Windows or anti-cheat compatibility is
promised. Do not disable Windows security or game anti-cheat protections.

Stop your game and close all running mDrive receivers before installation.
Administrator approval is required for Program Files and driver installation.
English microphone recognition uses bundled Vosk, not a Windows language pack.
English speech output still needs a Windows English TTS voice. On first run,
use Audio check to check English output and the speech model. If English TTS
is missing, install an English voice using Windows language options, or select
Android voice output. No Windows language, microphone or security settings are
silently changed. Allow microphone permission yourself if Windows asks.

Connect phone and PC to the same private network, then scan the QR. If Windows
Firewall asks, allow private networks only. This installer does not add broad
firewall rules, change your network profile, or disable a firewall.

Enable Wendy - F1 2025 / 2026 in the QR window. PC/PC input and output are the defaults; either
side can use Android. Select your headset as Windows default input/output.
Choose an installed English PC voice in the QR window (en-GB male / en-US female if available). No voice cloning or automatic language-pack installation.
Wendy volume defaults to 100 percent and is independent of the game's volume. LB reserves the microphone
for F1 radio while held and for 8 seconds after release.

Speech recognition runs in a separate low-priority CPU process. No GPU, cloud
speech service, audio upload, audio recording, OCR or screen capture is used
by PC recognition. Android recognition may use its provider's cloud service.
Actual accents, headset noise, F1 load and long-run reliability need testing;
synthetic phrase tests are not an accuracy guarantee.

This preview installer is not Authenticode-signed. The included official
driver installer is signed and SHA-256 checked during packaging and setup.
An application signing certificate and clean-machine testing are still needed
before a general public release. Do not bypass OS security warnings.

Third-party notices are installed in the notices directory. No ownership of
third-party software or models is claimed by mDrive.
