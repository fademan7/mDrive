# 이전 버전 개발 기록

현재 설치·설정 방법은 [README](README.md)를 우선합니다. 아래는 기존 README를 보존한 개발 이력이며, 당시의 미설치 상태와 이전 동작 설명을 포함합니다.

# PhoneWheel

## mDrive 0.5.3 · Controller recovery / Creator information

Gamepad output now has a dedicated worker, exception recovery with neutral-only retries, and an independent stale-report guard. ACK/haptic and safety clocks run on dedicated threads separate from Wendy work. Safety deadlines and neutral re-arm remain unchanged: never hold stale throttle to conceal a disconnect. PC shows stop/error/ACK-rejection counters; Android Options has Controller diagnostics. This fixes a verified code path that permanently stopped the old output worker after an exception, but does not prove the cause of an unrecorded in-game incident.

Both apps include **About / Creator**: [fademan7.github.io](https://fademan7.github.io/) · [neojshin@gmail.com](mailto:neojshin@gmail.com). Wendy adds game-provided weather forecasts and same-lap gap to the race leader. [0.5.3 report](artifacts/validation/WENDY_053.md)

Installed on the connected Fold5: `android/app/build/outputs/apk/debug/app-debug.apk` (0.5.3 / 15). Default `release/receiver/PhoneWheel.Receiver.exe` is now 0.5.3; previous EXE is backed up in `release/backups/PhoneWheel.Receiver-before-053.exe`. Keep the CPU `wendy` bundle beside the receiver. The build folder uses a local junction to the existing bundle; it is not a self-contained distribution ZIP.

A follow-up fixes a sensor/clock read-order race: capture input before sampling the freshness clock, so a concurrently published valid sample cannot be falsely marked as future-dated. The regression failed before the fix and passes afterward. Android **43 tests PASS**, Windows **28 groups PASS**. Real Fold5 Wi-Fi AUTO/PTT/settings/TTS isolation testing passes against a null-output receiver; this is not prolonged F1 gameplay verification. See the deployment follow-up in the report for exact measurements and limitations.

## mDrive 0.5.2 · AUTO recovery / race greetings / telemetry conversations

AUTO hides PTT; **Options → Wendy → AUTO: system (recommended)** uses the same English recognizer as PTT. This Fold5's on-device English model is not installed. Transient speech errors retry with bounded backoff; unavailable language/permission errors explain the required action. Voice mode switches do not recreate controller or Wendy connections. A delayed disconnect callback cannot erase a newer recovered ACK. Safety timeouts are unchanged.

Twenty race greeting variants, radio check, and expanded live telemetry questions are included. [English conversation reference](docs/WENDY_CONVERSATIONS.md) · [0.5.2 verification and limitations](artifacts/validation/WENDY_052.md). Run the existing `START_WIFI.cmd`; keep `release/receiver/wendy` beside the Receiver EXE. Old 0.4.0 ZIP is not this release.

## mDrive 0.5.1 · Controller isolation / English UI (previous version)

All application UI is English. Open **Options → Wendy F1 Engineer → Always Listening: system** to use the same recognizer as PTT (may use the provider's online service). **On-device** remains an explicit alternative. Microphone permission and an English recognition service are required. Automatic mode enables Wendy as well. Recording is paused during TTS and outside the foreground app.

Options/Wendy now use an in-window panel: changing voice settings does not release controller inputs, recenter, or recreate the controller socket. Editing steering/layout, Android permission prompts, and leaving the app still apply the safety release. Foreground Wi-Fi requests low latency, controller I/O has elevated thread priority, and sender exceptions cannot silently stop the periodic loop. These measures cannot guarantee against radio/PC outages.

New queries: `Tyre temperature`, `Front left tyre temperature`, `Tyre pressure`, `Front left brake temperature`, `Engine temperature`, `How old are my tyres?`, `How many laps left?`. Actual temperatures/pressure come from live UDP telemetry, not setup targets. [0.5.1 validation](artifacts/validation/WENDY_051.md)

## mDrive 0.5.0 · Hands-free / 피트 대화 / 랩 코칭 (previous version)

폰 **옵션 → Wendy F1 Engineer → Wendy 켜기 → Always Listening 켜기**로 전경에서 자동 재청취할 수 있습니다. 기본 OFF이며 온디바이스 영어 인식이 필요합니다. 지원되지 않으면 PTT를 사용하세요. 마이크는 Wendy 응답·앱 전환·연결 해제 중 중지합니다. 헤드셋 권장, 게임 음성 오인식·배터리 영향은 실기 확인이 필요합니다.

`Do I need pit in?`, `Should I pit?`, `Box box`, `Stay out`, `How was my last lap?`, `How can I improve?`를 추가했습니다. **Box box는 피트 리마인더이지 실제 게임 조작이 아닙니다.** 첫 정상 완료 랩을 기준으로 다음 랩부터 시간·브레이킹 구간을 비교합니다. 뒤차가 1.5초 이내로 3초간 유지되면 간격을 알립니다.

PC 실행은 기존 `START_WIFI.cmd` 또는 `release/receiver/PhoneWheel.Receiver.exe` 그대로입니다. `wendy` 폴더를 유지하세요. 0.5.0 APK 설치와 PC 교체 완료. 이전 0.4.0 ZIP은 구버전입니다. [0.5.0 상세·검증 결과](artifacts/validation/WENDY_050.md)

## mDrive 0.4.0 · CPU Wendy

Qwen3 0.6B Q8_0 + llama.cpp CPU 전용 번들로 영어 자연어를 정형 의도로 분류합니다. LLM은 수치를 만들거나 설정을 실행하지 않습니다. `Heard / Intent / Response`는 폰 Wendy 옵션과 PC **Wendy details**에서 확인합니다. 미인식은 `I didn't understand that.`입니다. 버튼 간격과 기존 컨트롤러 설정은 유지합니다.

PC는 `release/receiver/PhoneWheel.Receiver.exe`와 옆의 **wendy 폴더를 함께 유지**하세요. 다른 PC에는 `release/mDrive-0.4.0-win-x64.zip` 전체를 풀어 사용합니다. 별도 Python/Ollama/AI 프로그램 설치가 필요 없습니다. 첫 질문에 CPU 모델을 읽으며 Engineer OFF 시 해제됩니다. [지원 기능·제한·검증 결과](artifacts/validation/WENDY_040.md)

자동 게임 설정 변경은 미지원입니다. 충분한 온도·마모 추세가 있으면 **승인 후 다음 세션 검토 권고를 저장**하며, 실제 변경으로 표현하지 않습니다. 전체 차량 거동 원인 진단/수치 셋업 자동화까지 완료한 버전은 아닙니다.

## mDrive 0.3.2 · 중앙 버튼 간격

방향키와 ABXY 네 버튼 묶음을 각각 바깥쪽으로 최대 화면 너비의 2.5% 이동합니다. 기존 페달 폭을 보존하고, 페달과 겹치지 않는 범위까지만 이동합니다. 버튼 크기·높이, 중앙 Right Stick, 상단 PTT/플래그, 조향·페달 응답은 유지합니다. 기존 배치는 `layout_before_spacing_v6`로 백업한 뒤 한 번만 조정합니다. [변경·검증 기록](artifacts/validation/UI_SPACING_032.md)

## mDrive 0.3.1 · Wendy HUD

0.3.1 Android HUD는 **PTT(기존 중앙 버튼 자리) → PTT 상태 → 화면 중앙 정사각형 플래그 → 중앙 보정·옵션** 순서입니다. 일반 플래그는 단색, SC/VSC는 노란 바탕+문자, Checkered는 체크무늬, 미수신은 회색으로 표시합니다. PC Receiver는 0.3.0 Wendy 구현과 그대로 호환됩니다. [HUD 변경 기록](artifacts/validation/WENDY_HUD_031.md)

## mDrive 0.3.0 · Wendy F1 Engineer

영어 PTT 질문/응답·F1 UDP 상태/경고·상단 Wendy/Flag·실제 하단 Right Stick을 추가했습니다. **PC/Android 빌드 완료, 휴대폰 설치 및 실게임 음성 검증은 아직입니다.** 기존 조향·페달 설정은 유지하며 Wendy는 기본 OFF입니다. PC와 폰을 함께 업데이트하세요.

빠른 시작: Receiver의 **F1 Engineer ON** → F1 UDP **127.0.0.1:20777 / format 2025** → 폰 **옵션 → Wendy F1 Engineer → 켜기** → **PTT를 누른 채 영어로 말하고 놓기**. 영어 인식은 Android 시스템 서비스, TTS는 설치된 영어 음성입니다. 음성 공급자에 따라 인터넷이 필요할 수 있습니다. 자동 게임 설정 변경은 안전한 쓰기 경로가 없어 미지원입니다.

[사용법·지원 질문·제한](docs/WENDY.md) · [변경 파일·빌드·CPU/메모리 측정](artifacts/validation/WENDY_030.md)

아래의 0.2.x 검증 내용은 이전 버전 기록입니다.

Galaxy Z Fold5의 회전 센서와 멀티터치를 Windows의 가상 Xbox 360 패드로 전달하는 로컬 네트워크 컨트롤러입니다. 조향은 LX, 브레이크는 LT, 가속은 RT로 매핑하며 두 페달을 동시에 유지할 수 있습니다.

현재 0.2.5 Android debug APK(UI 미리보기)가 빌드되어 있으며 Windows 단일 EXE는 기존 버전을 유지합니다. **0.2.5는 아직 휴대폰에 설치하지 않았습니다.** 중앙의 방향키/ABXY 묶음을 좌우로 조금 벌리고 가운데 아래에 시점 스틱을 배치했습니다. 스틱은 손을 놓으면 중앙으로 돌아오는 로컬 터치 미리보기이며 아직 게임 RX/RY 입력을 전송하지 않습니다. 상단은 `중앙 — PIT CREW · INFO — 옵션` 배치이며 음성 기능은 미연결입니다. 이전 버튼 배치는 `layout_before_look_v5`로 백업하고, 기존 페달 폭·상단 게임 버튼·조향 설정은 보존합니다. 상세 기록은 [0.2.5 UI 검증](artifacts/validation/UI_LAYOUT_025.md)을 참고하세요.

0.2.4부터 QR 연결은 옵션으로 옮겼으며, 연결 복구 후 손을 떼고 조향 중립을 유지하면 별도 재개 버튼 없이 다시 시작합니다. 사용자는 F1 25의 2026 팩에서 정지 시 화면 각도 일치를 확인했으나 속도가 올라가면 덜 돌아간다고 보고했습니다. 게임의 속도별 패드 보정 가능성이 있으며, 앱은 차량 속도를 입력받지 않습니다. 실제 텔레메트리 이벤트 판정은 비활성입니다.

조향 비교용: **옵션 → 조향·페달 설정 → 조향 증폭 1.55배**. 한쪽 범위 116°, 곡선 1/데드존 0/평활 0으로 저장됩니다. 90°=77.6%, 116° 이상=100%이며 포화 전까지 선형입니다. 기본 한쪽 180°(90°=50%)로 돌아가려면 범위를 180으로 설정하세요. 이 값은 게임패드 스틱 입력이며, 게임 화면 휠 각도를 보장하지 않습니다. 페달의 위아래 10% 여유와 버튼 배치는 변경하지 않습니다.

## 가장 쉬운 연결 — USB 케이블

1. 휴대폰을 USB 케이블로 연결하고 USB 디버깅 허용을 누릅니다.
2. `START_RECEIVER.cmd` 또는 `release/receiver/PhoneWheel.Receiver.exe`를 더블클릭합니다. 허용된 USB 휴대폰이 한 대 있으면 **USB를 우선 선택**합니다. USB만 지정하려면 `START_USB.cmd`를 사용합니다.
3. 휴대폰 앱이 자동으로 열립니다. 편하게 잡은 자세에서 페달에서 손을 떼고 잠시 고정하면 **자동 중앙 보정 → 자동 운전 활성**이 됩니다. 중앙이 어긋날 때만 `중앙`을 누릅니다.

USB 모드는 Wi-Fi·인터넷·USB 테더링·QR 입력이 필요하지 않습니다. 포트는 loopback TCP 26761이며 PC의 LAN 인터페이스에는 열지 않습니다. **앱과 리시버를 계속 열어둔 경우** 같은 인증 세션으로 자동 재연결하며 PC는 같은 휴대폰의 전용 ADB 포트 경로를 점검/복구합니다. 복구 후 페달에서 손을 떼고 조향을 중립으로 돌리세요. 이전 가속은 자동 재개하지 않습니다. 앱을 완전히 종료했거나 PC를 재시작한 경우에는 리시버를 재실행해 새 세션으로 연결해야 합니다. ADB 장치 자체가 사라지면 케이블 재연결/디버깅 허용 확인이 필요합니다. 장시간 안정성 통과로 표시하지 않습니다.

이 PC에는 ADB가 준비되어 있습니다. EXE를 다른 PC로 옮겨 USB 연결하려면 Android SDK Platform-Tools(ADB)가 별도로 필요합니다. 프로젝트 `.tools/android-sdk/platform-tools`, `ANDROID_HOME`/`ANDROID_SDK_ROOT`, 기본 Android SDK 설치 경로 또는 PATH에서 찾습니다. ADB는 EXE에 포함되지 않습니다. 여러 휴대폰이 연결되어 있으면 하나만 남기세요.

케이블이 꽂혀 있어도 Wi-Fi를 쓰려면 `START_WIFI.cmd`를 실행하세요. USB와 Wi-Fi는 실행 시 선택하며 세션 도중 자동 전환하지 않습니다.

## 가장 쉬운 연결 — QR

1. PC와 휴대폰을 같은 Wi-Fi에 연결합니다.
2. `START_WIFI.cmd`를 더블클릭합니다. USB 휴대폰이 연결되지 않았다면 기본 EXE도 Wi-Fi 모드로 열립니다.
3. 휴대폰 **옵션 → QR / PC 연결 → QR 스캔**에서 PC 창의 QR을 비춥니다. 카메라 권한을 요청하면 허용합니다.
4. 휴대폰에 **Wi-Fi 연결됨**이 표시되면 편하게 잡고 페달에서 손을 떼세요. 자세 안정/중립 대기 후 자동으로 시작합니다.

IP·Session·Key를 타이핑할 필요가 없습니다. QR은 인터넷 서비스로 전송하지 않고 기기에서 읽습니다. QR에 세션 비밀 키가 포함되므로 캡처해서 공유하지 마세요. PC를 다시 실행하면 새 QR을 스캔하세요. 앱을 완전히 종료한 뒤 재연결할 때도 PC 리시버를 재시작하세요.

Wi-Fi 모드의 **PC 응답 없음**이라면 QR 입력은 끝났지만 통신이 되지 않는 상태입니다. 같은 Wi-Fi인지, Windows 방화벽에서 이 프로그램의 UDP 26760 수신이 허용되어 있는지 확인해야 합니다. 방화벽을 통째로 끄지 마세요. 이 업데이트는 방화벽 규칙을 변경하지 않습니다. USB 모드는 이 Wi-Fi 조건과 무관합니다.

QR 없이 입력하려면 휴대폰 **옵션 → PC 연결 → 수동 입력**을 사용합니다. 최신 APK는 `android/app/build/outputs/apk/debug/app-debug.apk`입니다.

## 현재 검증 상태

| 범위 | 결과 |
|---|---|
| Python 참조 모델 | PASS, 37 tests |
| C# codec/상태 머신/F1 합성 parser/QR payload/진동 | PASS, 12 groups (USB 재연결 안전 포함) |
| Kotlin codec/조향/멀티터치/페어링/UDP·USB/자동 시작/Wendy·Right Stick | PASS, 34 tests (0.3.0) |
| Kotlin↔C# 인증 Control datagram | PASS, 양방향 파일 교환 |
| Windows Probe→ViGEm→XInput | PASS, LX ±16384, LT/RT 128, 동시 255/255, 종료 중립 |
| 실제 UDP 단절 watchdog | PASS, 마지막 Control→중립 163.968ms (목표 170ms 이내) |
| Android APK | PASS, debug APK 생성 |
| Fold5 APK 설치·화면 실행 | 0.2.4까지 확인, SM-F946N / Android 16. 0.2.5 설치 미실행 |
| Fold5 → USB → XInput | PASS, 가속 30/50/100%→RT 76/128/255 및 해제 0 (ADB 터치 주입) |
| USB TCP 부분 패킷 중단 watchdog | PASS, loopback 152.720ms (실제 케이블 지연 실측과 구분) |
| XInput 진동 → Fold5 진동 서비스 | PASS, 최종 APK에서 0.3초 시험 요청→약 279ms 실행/정지; 체감 별도 확인 필요 |
| Fold5 손으로 회전·양손 터치 조작감 | 미검증 |
| F1 25/2026 Season Pack, 원작 AC | 사용자 주행 확인, 화면 조향 1:1 미검증 |
| 실제 텔레메트리 판정·진동 | DISABLED/NOT RUN |

상세 증거와 실행 환경은 [Gate 검증 기록](artifacts/validation/GATE_RESULTS.md)에 있습니다.

## 고정 빌드 구성

- Windows: .NET SDK 10.0.400, `net10.0-windows`, `win-x64`
- ViGEm client: `Nefarius.ViGEm.Client` 1.21.256
- Android: AGP 9.3.2, Gradle 9.5.0, 내장 Kotlin 2.3.20
- Android SDK: compile/target 37, minSdk 33, build-tools 37.0.0
- JDK: 17
- QR: Windows QRCoder 1.6.0, Android ZXing Embedded 4.3.0

`global.json`, Gradle wrapper와 각 프로젝트 파일이 이 버전을 고정합니다. 이 작업 폴더의 `.tools`는 로컬 검증용 SDK 캐시이며 배포 파일이 아닙니다.

## Windows 설치와 Probe

### 더블클릭으로 리시버 실행

`START_RECEIVER.cmd` 또는 `release/receiver/PhoneWheel.Receiver.exe`를 더블클릭하세요. EXE는 Windows x64용 .NET 런타임과 ViGEm 클라이언트를 포함한 단일 파일이며, .NET을 따로 설치하지 않아도 됩니다. ViGEmBus 드라이버는 별도로 필요하며 이 PC에서는 이미 확인했습니다. 창을 열어 둔 동안 가상 Xbox 패드가 유지됩니다.

리시버는 승인된 USB 휴대폰이 있으면 USB를 우선 선택하고, 없으면 활성 LAN 주소의 Wi-Fi QR 창을 띄웁니다. PC 창에는 **폰 수신**과 **게임 출력**을 따로 표시합니다. 운전 시작 전에는 폰 수신 30%, 게임 출력 0%가 정상입니다. 운전 활성 상태에서는 두 값이 같아야 합니다. 이 표시는 게임 자체가 읽은 값의 실측을 대체하지 않습니다. 연결창을 닫거나 콘솔에서 `Q` 또는 `Ctrl+C`로 종료할 수 있습니다. 기존 리시버를 먼저 종료하고 하나만 실행하세요. CLI 옵션을 지정할 때 연결창도 필요하면 `--qr`을 추가합니다.

USB 디버깅은 APK 설치·진단뿐 아니라 USB 주행 통신에도 사용합니다. Wi-Fi 모드만 Windows 방화벽의 UDP 26760 수신 허용이 필요합니다. 공용 네트워크에서는 일괄 허용하지 말고 현재 휴대폰 IP에 한정한 규칙을 사용하세요. Wi-Fi 자동 선택 IP가 다르면 `PhoneWheel.Receiver.exe --wifi --bind <PC IP> --qr`로 지정할 수 있습니다.

첫 테스트는 게임의 주행을 시작하기 전에 진행하세요.

1. 휴대폰을 좌우로 천천히 돌려 PC의 조향 수치가 각각 음수/양수로 바뀌는지 확인합니다.
2. 왼쪽 브레이크·오른쪽 가속을 아래에서 위로 따로 드래그해 0→100% 변화를 확인합니다.
3. 두 손가락으로 두 페달을 동시에 100%로 만든 뒤, 하나씩 떼어 해당 값만 0이 되는지 확인합니다.
4. 옵션을 열거나 앱을 다른 화면으로 전환하면 PC의 모든 값이 0으로 돌아오는지 확인합니다.
5. 복귀 후 손을 떼고 중립을 유지하면 자동 준비합니다. F1 입력 설정에서 조향=LX, 브레이크=LT, 가속=RT를 확인합니다.

2026-09-12: 연결된 Fold5 SM-F946N(Android 16)에 APK 설치와 화면 실행을 확인했습니다. PC 리시버 단일 EXE 실행도 확인했습니다. 휴대폰 실제 회전/동시 터치의 PC 도달 및 게임 테스트는 아직 완료하지 않았습니다.

리시버 재생성은 PowerShell에서 `./tools/publish-receiver.ps1`을 실행합니다. SDK가 필요하며 게시 결과는 `release/receiver`입니다.

1. [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)를 설치합니다.
2. 가상 패드가 필요하면 보관된 공식 [ViGEmBus 1.22.0 릴리스](https://github.com/nefarius/ViGEmBus/releases/tag/v1.22.0)의 서명된 설치 프로그램을 사용합니다. 이 프로젝트는 종료된 드라이버에 의존하므로 설치 전 게시자와 서명을 확인하십시오. 드라이버 설치만으로 패드가 생성되지는 않습니다.
3. PowerShell에서 다음을 실행합니다.

```powershell
dotnet build windows/src/PhoneWheel.Probe/PhoneWheel.Probe.csproj -c Release
dotnet run --project windows/src/PhoneWheel.Probe/PhoneWheel.Probe.csproj -c Release -- --backend vigem --step-seconds 2
```

동시에 다른 PowerShell에서 XInput 값을 기록합니다.

```powershell
python tools/xinput_probe.py --seconds 30 > artifacts/validation/xinput-local.csv
```

관찰 순서는 중립 → LX -50% → LX +50% → LT 50% → RT 50% → LT/RT 동시 100% → 중립입니다. 기대값은 LX `-16384/+16384`, 반 트리거 `128`, 동시 트리거 `255/255`입니다. `joy.cpl`만으로 독립 트리거를 판정하지 마십시오.

드라이버가 없는 개발 PC에서는 다음처럼 출력 로직만 확인합니다.

```powershell
dotnet run --project windows/src/PhoneWheel.Probe/PhoneWheel.Probe.csproj -c Release -- --backend csv --csv artifacts/validation/probe.csv --step-seconds 0.1
```

## Windows Host 실행

빌드 후 로컬 네트워크 어댑터의 실제 IPv4 주소로 bind합니다.

```powershell
dotnet build windows/src/PhoneWheel.Host/PhoneWheel.Host.csproj -c Release
dotnet run --project windows/src/PhoneWheel.Host/PhoneWheel.Host.csproj -c Release -- --backend vigem --bind 192.168.0.10 --port 26760
```

Host는 매 실행마다 32바이트 키와 0이 아닌 session ID를 만들고 다음 네 값을 출력합니다: `pcIp`, `pcPort`, `sessionIdHex`, `keyBase64`. 키를 로그 공유나 스크린샷에 남기지 마십시오. Windows 방화벽에는 선택한 개인 네트워크 어댑터의 UDP 26760 인바운드만 허용합니다.

ViGEm 없이 연결·watchdog만 확인하려면 `--backend null`, 값을 CSV로 보려면 `--backend csv --csv host.csv`를 사용합니다. Host는 인증된 Hello의 최초 source IP/port를 세션에 고정합니다.

## Android APK 빌드와 설치

JDK 17과 Android SDK Platform 37/build-tools 37.0.0이 필요합니다. `ANDROID_HOME`이 SDK를 가리키는 상태에서 실행합니다.

```powershell
cd android
./gradlew.bat testDebugUnitTest assembleDebug
adb devices -l
adb install -r app/build/outputs/apk/debug/app-debug.apk
```

현재 생성된 APK는 [app-debug.apk](android/app/build/outputs/apk/debug/app-debug.apk)입니다. debug 서명 빌드이므로 개인 시험용으로만 사용하십시오.

## 연결, 중앙 보정, 운전 시작

1. USB는 케이블 연결/USB 디버깅 허용 후 PC EXE를 실행합니다. USB 테더링은 필요 없습니다. Wi-Fi는 같은 LAN에서 `START_WIFI.cmd`를 실행합니다.
2. USB는 앱이 자동 연결됩니다. Wi-Fi는 새 PC QR을 스캔합니다.
3. Fold5를 접은 커버 화면 가로 모드로 편하게 잡고 페달에서 손을 뗍니다.
4. 0.2.2부터 약 500ms 동안 2° 이내로 자세가 안정되면 현재 자세를 자동 중앙으로 잡습니다. 주행 중에는 중앙이 따라 움직이지 않습니다. `중앙` 버튼은 필요할 때 수동으로 다시 잡는 용도입니다.
5. 폰은 센서/연결 정상, 조향 중립, 모든 터치 해제 상태를 700ms 확인한 뒤 자동으로 ARM을 올립니다. PC의 독립 300ms 중립 조건은 그대로 유지됩니다.
6. 옵션/앱 전환 시 입력을 해제하고 복귀하면 안전 준비를 거칩니다. 통신 장애 후에는 연결·센서 정상, 모든 터치 해제, 조향 중립 700ms를 확인해 자동 시작합니다. 센서 연속성이 끊기면 안정 자세에서 다시 자동 보정하며, 필요하면 `중앙`을 누릅니다.
7. 화면 왼쪽 1/4 전체 높이가 브레이크, 오른쪽 1/4 전체 높이가 가속입니다. 위아래 10%는 각각 100%/0% 유지 구간이고, 가운데 80%는 선형입니다. 손가락이 가로 영역 경계를 넘어가도 처음 잡은 페달이 유지됩니다. 흰 선/원은 Android가 보고한 접촉 위치입니다.

화면은 가로 전체화면으로 고정됩니다. `옵션 → PC 연결`에서 연결하며, `옵션 → 버튼 크기·위치 편집`에서 페달을 선택한 뒤 `작게`/`크게`로 폭을 화면의 20–25%로 바꿀 수 있습니다. 페달은 양끝과 위아래 끝에 고정됩니다. 나머지 버튼은 중앙 영역에서 끌어 이동하고 크기를 바꿀 수 있습니다. 편집 시 주행 입력은 해제되며 배치는 자동 저장됩니다. `복원`으로 기본 배치로 돌아갑니다. 예전 배치 저장값은 남겨두고 새 전체 높이 배치에 별도 저장 키를 사용합니다.

0.2.2 페달은 사용자 선택에 따라 `clamp((0.9H - y) / 0.8H, 0, 1)`로 변경했습니다. 아래에서 10%=0%, 30%=25%, 50%=50%, 70%=75%, 90%=100%입니다. 중간 구간은 선형이지만 전체 화면 이동량과 출력량의 1:1은 아닙니다. 이전 95% 스냅은 제거했습니다. 손을 떼면 0%이며 예전 저장 민감도는 적용하지 않습니다. 조향 기본 90°=50%, 180°=100%는 변경하지 않았습니다.

앱 전환·잠금·센서 정체·보정 변경·150ms 통신 단절에서는 게임 입력을 해제합니다. 터치 취소는 해당 터치 입력을 놓습니다. 다시 활성화할 때는 중립 대기와 새로운 ARM 상승이 필요하며 눌린 페달 상태로 자동 출발하지 않습니다.

## 게임 설정

### F1 25 / 2026 Season Pack

- 일반적인 F1 휠 범위는 **총 360°**, 즉 중앙에서 왼쪽 180°/오른쪽 180°입니다. Fanatec의 F1 25 권장값도 휠 베이스와 게임을 360°로 맞춥니다. EA는 F1 25의 조향 값을 full-lock left `-1`부터 full-lock right `+1`까지 정규화합니다.
- PhoneWheel은 가상 Xbox 360 패드입니다. 게임의 `Wheel Rotation`과 앱의 물리 범위는 별개입니다. 앱은 **한쪽 180°/총 360°**를 기본으로 하며, 한쪽 범위를 45–180°로 설정할 수 있습니다. 0.2.1 기본 설정은 **선형**으로 45°=25%, 90°=50%, 180°=100%입니다. 과거 0.2.0의 비선형 증폭(곡선 0.7)은 기본값에서 제거했습니다. 사용자가 따로 저장한 조향 설정은 보존하므로 선형으로 쓰려면 곡선 1, 데드존 0, 평활 0을 사용하세요. 범위를 줄이면 비례 관계는 유지되지만 최대 입력에 더 빨리 도달합니다.
- 화면은 가로 방향에 고정됩니다. 조향은 연속 센서 표본 사이의 각도 변화를 이어서 추적하므로 179→180→181°에서 반대 방향으로 뒤집히지 않고 같은 쪽 100%를 유지합니다. 같은 경로로 중앙에 돌아오면 0°가 됩니다. 좌우 각각 180° 범위 내 왕복을 전제로 하며 계속 한 방향으로 여러 바퀴 돌리는 사용은 대상이 아닙니다.
- 앱 전환이나 100ms 초과 센서 공백으로 회전 이력이 끊기면 입력을 해제하고 `중앙` 재보정을 요구합니다. 화면을 고정하는 것만으로 방향이 구분되는 것은 아니며, 연속된 회전 이력을 이용합니다.
- 중앙 응답 곡선 기본값은 `1.0`, 추가 데드존 `0°`, 추가 평활 `0ms`입니다. 센서 자체 필터·전송 지연·XInput 정수 양자화는 남으므로 지연이 없다는 뜻은 아닙니다. 게임에서만 조향이 작으면 비선형 곡선으로 덮지 말고 폰 각도→앱 %→PC 수신/출력→게임 입력을 나눠 확인하세요. 곡선/데드존/평활 옵션은 선택적으로 사용할 수 있습니다.
- 입력 장치에서 조향=LX, 브레이크=LT, 가속=RT로 매핑합니다.
- 자동 변속과 자동 클러치는 게임에서 켭니다. 앱은 변속이나 클러치를 자동 조작하지 않습니다.
- 처음에는 패드 deadzone/linearity/saturation을 기본값으로 기록하고, 좌우 25/50/100% 응답을 확인한 뒤 조정합니다.
- 텔레메트리 시험은 UDP 출력 ON, 주소 `127.0.0.1`, 포트 `20777`, Broadcast OFF, 형식 `2025`, 60Hz로 시작합니다.

현재 코드는 공식 F1 25 v3 문서의 2025 Motion Ex 273바이트 일부를 엄격히 파싱하는 합성 테스트까지만 통과했습니다. 실캡처, 2026 전용 parser, 잠김/헛돎 의미는 검증되지 않았고 Host는 게임 텔레메트리 진동을 송신하지 않습니다.

각도 근거: [Fanatec F1 25 권장 설정](https://www.fanatec.com/eu/en/explorer/games/f1-2024/f1-25-fanatec-recommended-settings/), [EA F1 25 v3 UDP 사양 PDF](https://forums.ea.com/t5/s/tghpe58374/attachments/tghpe58374/f1-games-game-info-hub-en/61/4/Data%20Output%20from%20F1%2025%20v3.pdf). Xbox의 일부 실제 휠은 2026 Season Pack 이후 게임 내 범위 대신 휠 본체 범위를 사용하므로 [EA 공지](https://forums.ea.com/blog/f1-games-game-info-hub-en/wheel-rotation-on-fanatec-moza-turtle-beach-and-logitech-prors50-on-xbox/13454763)를 별도로 확인해야 합니다. PhoneWheel의 XInput 패드 경로에는 이 예외가 직접 적용되지 않습니다.

### 원작 Assetto Corsa

LX/LT/RT 게임 매핑은 Probe로 먼저 확인하십시오. 공유메모리 어댑터는 Kunos 원본 SDK의 size/packing/offset/단위/휠 순서를 확보하지 못해 의도적으로 비활성입니다. ACC/EVO 구조체를 원작 AC의 근거로 사용하지 않습니다.

## 진동

`옵션 → 진동 시험 (휴대폰)`은 연결과 무관한 250ms 시험입니다. PC 게임패드 진동을 전달하려면 **옵션 → 게임 기본 진동 켜기**를 선택하고 손을 떼어 자동 준비를 기다리세요. 게임에서도 패드 진동을 켜야 합니다. 기본값은 OFF입니다. 이 모드는 엔진·변속 진동도 포함할 수 있어 원래 요구인 중요 이벤트 전용 모드와 구분합니다.

ViGEm motor feedback을 인증 Haptic event 8로 보내고 휴대폰에서 재생합니다. 입력 해제/앱 전환 시 취소, 100ms lease, 새 motor feedback 없이 최대 500ms 후 종료합니다. 따라서 게임이 같은 motor 값을 오래 유지하는 진동은 짧게 잘릴 수 있습니다. 동일 이벤트 갱신은 패턴을 재시작하지 않으며 오래된 UI 작업은 재생하지 않습니다.

잠김/헛돎 전용 telemetry detector는 실제 F1/AC 캡처의 의미를 검증하지 못해 여전히 DISABLED입니다. TC/ABS 설정값을 개입 이벤트로 취급하지 않습니다. PhoneWheel은 모터가 달린 FFB 휠이 아니므로 조향 저항을 만들 수 없습니다.

## 오류 해결

- `VigemBusNotFoundException`: 공식 ViGEmBus 설치·서명·장치 관리자 상태를 확인한 뒤 재부팅하고 Probe를 다시 실행합니다. 보안 또는 안티치트를 끄지 마십시오.
- USB 파일 전송은 되지만 연결 실패: `adb devices -l`에 허용된 기기가 있는지 확인하세요. 2026-09-13 실기 시험은 성공했지만 이후 ADB 장치가 사라졌고 Windows에는 Samsung USB/모뎀/WPD만 남았습니다. 이 상태는 Wi-Fi/방화벽과 무관하며 개발자 옵션의 USB 디버깅 및 PC 허용 여부 확인이 필요합니다. 원인은 아직 확정하지 않았고 USB 재연결 후 리시버 재실행이 필요합니다.
- 패드가 보이지만 트리거가 이상함: `tools/xinput_probe.py`로 LT/RT를 독립 확인하고 Steam Input/다른 remapper/물리 패드를 한 번에 하나씩 분리합니다.
- 앱이 `인증 대기`에 머묾: PC IP/UDP 26760/방화벽/같은 어댑터인지 확인합니다. session과 key를 새 Host 실행 값으로 다시 입력합니다.
- 연결 복구 후 운전이 시작되지 않음: 모든 손가락을 화면에서 떼고 조향을 중립으로 돌리세요. 중심이 맞지 않으면 `중앙`을 누릅니다. 앱/PC를 완전히 종료했다면 리시버를 재실행하세요.
- Android 17에서 연결 거부: 앱의 로컬 네트워크 권한을 허용합니다. 권한이 없으면 입력은 해제 상태입니다.
- 조향 방향이 반대: 게임 매핑을 바꾸기 전에 실제 오른쪽 회전에서 앱/게임 방향을 기록합니다. 기본 센서 부호는 `-1`입니다.
- 센서가 멈춤: 앱은 100ms 정체 시 마지막 값을 새 값처럼 보내지 않고 중립 처리합니다. 장치 로그로 실제 sensor timestamp 간격을 확인해야 합니다.

## 검증 재현

```powershell
python verification/run_verification.py
dotnet run --project windows/tests/PhoneWheel.Tests/PhoneWheel.Tests.csproj -c Release
cd android
./gradlew.bat testDebugUnitTest assembleDebug
```

하드웨어 시험 순서와 합격 기준은 [HARDWARE_TEST_PLAN.md](docs/HARDWARE_TEST_PLAN.md), PWR1 바이트 계약은 [PROTOCOL.md](docs/PROTOCOL.md)를 따릅니다.
