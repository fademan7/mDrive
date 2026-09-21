# Wendy 0.3.0 — 2026-09-16

## 결과와 범위

Android APK와 Windows 단일 EXE를 실제 빌드했습니다. 기존 컨트롤러 회귀 검사와 합성 F1 UDP/음성 텍스트 사이드채널 시험은 통과했습니다. **이 결과는 Fold5 설치, 실제 마이크/STT/TTS, 실제 F1 UDP/주행 성능 검증이 아닙니다.** 이전 장치 접근은 자동 승인 검토 도구의 사용 한도로 거부되었으며 우회하지 않았습니다. 이번 APK는 설치하지 않았습니다.

## 변경 파일

### Android

- `android/app/build.gradle.kts`: 0.3.0 / versionCode 8.
- `android/app/src/main/AndroidManifest.xml`: PTT 마이크 권한, 시스템 SpeechRecognizer/TTS 서비스 조회.
- `android/app/src/main/java/dev/phonewheel/MainActivity.kt`: 상단 Wendy/Flag/PTT, Wendy opt-in 설정/최근 응답, lifecycle, Right Stick snapshot.
- `android/app/src/main/java/dev/phonewheel/PedalTouchView.kt`: 승인된 하단 스틱 배치 유지, RX/RY 노출.
- `android/app/src/main/java/dev/phonewheel/LookStickPreview.kt`: 기존 포인터 소유 모델 유지, 실제 사용 설명 갱신.
- `android/app/src/main/java/dev/phonewheel/Protocol.kt`: legacy Control 보존 + ControlLook.
- `android/app/src/main/java/dev/phonewheel/PacketTransport.kt`: 최대 프레임 76.
- `android/app/src/main/java/dev/phonewheel/UdpControllerClient.kt`: 스틱 사용 시 확장 Control, 기존 주기/ACK 유지.
- 새 `WendyClient.kt`, `WendyVoice.kt`, `WendyWire.kt`: 독립 저빈도 TCP, 영어 PTT/TTS, 인증.
- 새 `android/app/src/test/java/dev/phonewheel/WendyTest.kt`: wire 고정 벡터/변조/재전송/방향/nonce, Right Stick/동시 페달.

### Windows

- `windows/src/PhoneWheel.Core/Controls.cs`: LookX/Y 검증·중립.
- `windows/src/PhoneWheel.Core/Pwr1Codec.cs`: ControlLook 양방향 codec, legacy 고정 벡터 유지.
- 새 `F1RaceState.cs`, `WendyEngineer.cs`, `WendyWire.cs`: strict 2025 파서, 상태/freshness, 영어 규칙·알림, nonce/HMAC.
- `windows/src/PhoneWheel.Output.ViGEm/ViGEmGamepadOutput.cs`: RightThumbX/Y 출력.
- `windows/src/PhoneWheel.Host/Program.cs`: 별도 Wendy 서비스, 선택적 USB reverse, 새 Control kind. 회귀에서 발견한 의도적 종료와 UDP SocketException 경합 처리.
- `windows/src/PhoneWheel.Host/PairingWindow.cs`: Engineer 토글·compact 상태.
- `windows/src/PhoneWheel.Host/ReceiverTransport.cs`: USB 최대 프레임 76.
- 새 `windows/src/PhoneWheel.Host/WendyService.cs`: ON 때만 리스너/텔레메트리 수신·처리, OFF 취소.
- `windows/tests/PhoneWheel.Tests/Program.cs`, `PhoneWheel.Tests.csproj`, 새 `WendyTests.cs`, `WendyServiceTests.cs`: 파서/10종 질문/명령 거부/상태 초기화/만료/알림/안전 및 ON/OFF 반복 소켓 정리.
- `tools/test_usb_transport.py`: 새 최대 길이에 맞춰 malformed 77바이트 검사.
- 새 `tools/test_wendy_transport.py`: 독립 Python wire/F1 fixture, 실 Receiver 동시 통신, Win32 CPU/working-set 표본.
- 문서: `README.md`, `docs/PROTOCOL.md`, `docs/WENDY.md`, 이 기록.

생성: `android/app/build/outputs/apk/debug/app-debug.apk`, `release/receiver/PhoneWheel.Receiver.exe`.

## 검사

| 검사 | 결과 |
|---|---|
| Windows Core / Host Release 빌드 및 self-contained single EXE publish | PASS |
| C# 콘솔 테스트 | 17/17 groups PASS |
| Android `testDebugUnitTest assembleDebug lintDebug --offline` | PASS |
| Kotlin JUnit | 34 tests, 0 failures/errors |
| Android lint | 0 errors, 25 warnings (리소스 문자열, SDK 경고 등) |
| 기존 USB framing/동시 트리거/중립재활성/부분 프레임 watchdog | PASS, 152.096ms / 최종 반복 160.210ms |
| 기존 Wi-Fi endpoint 보호/재인증/중립 재활성 | PASS |
| Wendy 실제 PC 소켓 + 독립 Python 클라이언트 | 10 질문, 미지원 설정 명령, 알림 중복 방지, HMAC 거부, nonce 재접속, 개별 데이터 만료 PASS |
| Wendy OFF | TCP 리스너 없음, UDP 포트 미점유 확인 |
| Wendy ON + 합성 telemetry 240 packets/s + 목표 120Hz 컨트롤러 | 지속 ARM 유지, ACK 수신 PASS |
| 실제 Fold5 설치/STT/TTS/PTT/게임 Right Stick | NOT RUN |
| 실제 F1 UDP 필드와 HUD 대조, 장시간 주행 latency/FPS | NOT RUN |

검사 명령:

```powershell
.\.tools\dotnet\dotnet.exe run --project windows/tests/PhoneWheel.Tests/PhoneWheel.Tests.csproj --no-restore -c Release
.\tools\publish-receiver.ps1
python tools/test_usb_transport.py
python tools/test_wifi_recovery.py
python tools/test_wendy_transport.py
```

Android는 workspace JDK17/ANDROID_HOME/ANDROID_USER_HOME/GRADLE_USER_HOME을 사용했습니다. 실행 위치 `android`, 명령 `./gradlew.bat --offline testDebugUnitTest assembleDebug lintDebug`.

## CPU / Memory / 입력 영향

아래는 같은 신규 Receiver에서 ON/OFF를 비교한 단기 합성 측정입니다. headless/null 출력, loopback USB 프레이밍, 1.2초 워밍업 + 약 6초 측정. F1 게임·실제 ViGEm·Receiver GUI·휴대폰 음성 인식/TTS 비용은 포함하지 않습니다. PC의 다른 작업도 완전히 통제하지 않았습니다. 재현 명령은 위 스크립트입니다.

| 측정 | OFF | ON |
|---|---:|---:|
| CPU (논리 코어 1개를 100%로 계산) | 5.47% | 9.62% |
| 평균 working set | 36.77MiB | 42.66MiB |
| PWR1 ACK RTT p95 | 8.77ms | 8.95ms |

ON의 증분 약 6MiB / 논리 코어 0.04개 수준이 이 표본에서 관측됐습니다. ACK RTT는 입력→게임 화면 지연이 아니며 0.18ms 차이를 유의미한 정확도나 보장으로 해석하지 마세요. 기본 센서/전송 주기, 100ms freshness, 150ms watchdog, ViGEm 직렬 worker를 변경하지 않았습니다. Wendy는 컨트롤러 소켓·락·송신 큐에 들어가지 않습니다. 다만 CPU/GC/OS 스케줄러는 공유하므로 실게임 영향이 절대 0이라고 주장하지 않습니다.

최종 실행파일로 모든 시험을 반복해 통과했습니다. 이때 CPU는 OFF 7.81% / ON 10.86%(단일 코어 기준), working set 36.56 / 42.68MiB, ACK RTT p95 9.01 / 8.94ms였습니다. 두 표본 차이는 측정 환경의 변동을 보여주며 ON이 더 빠르다는 결론을 내리지 않습니다. 최종 APK 7,548,273바이트, EXE 116,642,333바이트입니다.

## 실기 확인 순서

장치 권한을 사용할 수 있게 되면 업데이트 APK를 설치하고 Receiver를 재시작합니다. 정차 상태에서 Engineer를 OFF로 둔 기존 주행, 두 페달+Right Stick, 연결 단절 시 RX/RY 해제를 먼저 확인합니다. 다음 Engineer ON, F1 UDP format 2025/20777, 휴대폰 Wendy ON, 영어 TTS 설치 및 마이크 허용 후 10개 질문을 실제 HUD와 비교합니다. 마지막으로 ON/OFF 장시간 주행과 FPS/입력 RTT, PTT 중 양손 페달 유지, 앱 전환·마이크 권한 거부·네트워크 단절을 확인해야 합니다. 값과 동작이 다르면 설정 명령을 추가하지 말고 읽기 경로부터 수정합니다.
