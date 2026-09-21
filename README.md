# mDrive

Android 휴대전화를 Windows 레이싱 게임용 컨트롤러로 사용하는 프로젝트입니다. 휴대전화를 돌려 조향하고, 화면 왼쪽 브레이크·오른쪽 가속 페달을 조작합니다. **Wendy F1 Engineer**는 F1 UDP 텔레메트리를 읽고 영어 음성 질문에 답하거나 레이스 경고를 알려줍니다.

현재 버전: **0.5.3** · 개발·실기 확인 환경: Windows 11 x64 + Galaxy Z Fold5.

제작: **fademan7 / neojshin** · [홈페이지](https://fademan7.github.io/) · [이메일](mailto:neojshin@gmail.com)

> 이 저장소에는 소스와 빌드 스크립트가 들어 있습니다. APK·Receiver EXE·Android SDK·대용량 Wendy 모델은 Git에 포함하지 않습니다. 처음 내려받았다면 아래 빌드를 먼저 진행하세요. 다른 Android 기기나 장시간 실제 레이스의 안정성은 별도 확인이 필요합니다.

## 1. 준비할 것

- **PC:** Windows x64. 가상 Xbox 360 패드용 [ViGEmBus 공식 배포본](https://github.com/nefarius/ViGEmBus/releases/tag/v1.22.0)을 설치합니다. 유지보수가 종료된 드라이버이므로 해당 PC에서 설치·게임 호환성을 확인해야 합니다. 보안·안티치트 기능을 끄지 마세요.
- **휴대전화:** Android 13(API 33) 이상, 회전 센서. USB 연결에는 데이터 전송 가능한 케이블과 USB 디버깅 승인이 필요합니다.
- **빌드 도구:** .NET SDK **10.0.400**(`global.json` 고정), JDK **17**, Android SDK **API 37**, Build Tools **37.0.0**, Platform-Tools(ADB). Gradle **9.5.0**과 Android Gradle Plugin **9.3.2**는 프로젝트에서 고정합니다.
- **Wendy:** 휴대전화의 영어 음성 인식 서비스, 영어 TTS 음성, 마이크 권한. 시스템 음성 인식은 공급자에 따라 인터넷을 사용할 수 있습니다.

## 2. 처음 빌드하고 설치하기

PowerShell에서 저장소를 내려받습니다. 이미 소스를 갖고 있다면 해당 폴더에서 시작하세요.

```powershell
git clone https://github.com/fademan7/mDrive.git
cd mDrive
```

### PC Receiver

.NET SDK를 설치한 뒤 프로젝트 루트에서 실행합니다.

```powershell
dotnet --version
powershell -NoProfile -File .\tools\publish-receiver.ps1
```

출력: `release\receiver\PhoneWheel.Receiver.exe`. 자체 포함 단일 EXE이므로 빌드된 Receiver를 실행할 PC에는 .NET SDK가 필요하지 않습니다. **ViGEmBus와 USB용 ADB는 EXE에 포함되지 않습니다.** 실행 중인 리시버를 교체할 때는 게임을 멈추고 리시버를 먼저 닫으세요.

### Android APK

아래 경로를 자신의 JDK/Android SDK 설치 위치에 맞춥니다. Android Studio의 SDK Manager로 API 37, Build Tools 37.0.0, Platform-Tools를 설치해도 됩니다.

```powershell
$env:JAVA_HOME = 'C:\path\to\jdk-17'
$env:ANDROID_HOME = "$env:LOCALAPPDATA\Android\Sdk"
cd android
.\gradlew.bat assembleDebug
cd ..
```

출력: `android\app\build\outputs\apk\debug\app-debug.apk`.

휴대전화에서 **개발자 옵션 → USB 디버깅**을 켜고, USB를 연결해 이 PC의 디버깅을 허용한 다음 설치합니다.

```powershell
& "$env:ANDROID_HOME\platform-tools\adb.exe" devices
& "$env:ANDROID_HOME\platform-tools\adb.exe" install -r .\android\app\build\outputs\apk\debug\app-debug.apk
```

목록에 `device`가 보여야 합니다. `unauthorized`라면 잠금을 풀고 승인 창을 확인하세요. 앱 이름은 휴대전화에서 **PhoneWheel**로 표시됩니다. 업데이트는 기존 앱 위에 설치하고 데이터를 삭제하지 마세요. 다른 PC에서 새 debug 서명으로 빌드하면 기존 설치와 서명이 달라 업데이트가 거부될 수 있습니다. 이 경우 기존 서명 키를 사용해야 하며, 무작정 앱을 삭제하면 저장한 배치와 설정이 사라집니다. 서명 키는 저장소에 올리지 않습니다.

## 3. PC와 연결하기

리시버는 **한 개만 실행**하세요. 게임을 멈춘 상태에서 연결하고 입력이 정상인지 확인한 후 주행합니다.

### USB — Wi-Fi 없이 연결

1. 휴대전화의 USB 디버깅을 승인하고 잠금을 풉니다. 승인된 USB 휴대전화는 한 대만 연결합니다.
2. PC에서 `START_USB.cmd`를 더블클릭합니다.
3. 휴대전화 앱이 자동으로 열리고 연결됩니다. **QR·Wi-Fi·USB 테더링은 필요하지 않습니다.**
4. 페달에서 손을 떼고 편한 자세로 잠시 고정하면 자동 중앙 보정 후 `Driving active`가 표시됩니다.

ADB는 `ANDROID_HOME\platform-tools`, 기본 Android SDK 설치 위치, PATH 또는 프로젝트 `.tools\android-sdk\platform-tools`에서 찾습니다. ADB가 없는 다른 PC에서는 Platform-Tools도 설치하세요. `START_RECEIVER.cmd`는 승인된 USB 휴대전화가 있으면 USB를 우선 선택하고, 없으면 Wi-Fi로 실행합니다.

### Wi-Fi — 같은 로컬 네트워크에서 연결

1. PC와 휴대전화를 같은 로컬 네트워크에 연결합니다. 게스트 Wi-Fi의 기기 간 통신 차단/AP 격리를 피하세요.
2. `START_WIFI.cmd`를 더블클릭합니다. USB 케이블이 있어도 Wi-Fi 모드로 실행됩니다.
3. Windows 방화벽에서 Receiver의 **개인 네트워크** 통신을 허용합니다. 방화벽 자체를 끄거나 인터넷 공유기 포트를 개방할 필요는 없습니다.
4. 폰의 **Options → QR / PC connection → Scan QR**에서 PC 창의 QR을 스캔합니다. 카메라/로컬 네트워크 권한을 요청하면 허용합니다.
5. 페달에서 손을 떼고 편한 자세로 고정해 `Driving active`를 확인합니다.

PC 리시버를 재시작하면 새 세션이 만들어집니다. **새 QR로 다시 연결**하세요. PC 주소가 바뀌었을 때도 리시버를 다시 실행하고 새 QR을 사용합니다. QR과 연결 키는 공개하지 마세요. USB/Wi-Fi는 실행 시 선택하며 주행 도중 자동 전환하지 않습니다.

| 용도 | 연결 |
|---|---|
| Wi-Fi 컨트롤러 | PC LAN 주소 / UDP **26760** |
| USB 컨트롤러 | ADB reverse / loopback TCP **26761** |
| Wendy 음성 명령·응답 | TCP **26762**; USB는 별도 ADB reverse |
| F1 텔레메트리 | PC loopback UDP **20777** |

## 4. 게임 컨트롤러 설정

게임 실행 전에 리시버를 켜고, 게임 입력 설정에서 **Xbox 360 Controller**를 선택합니다.

| 조작 | 게임패드 출력 |
|---|---|
| 휴대전화 회전 | Left Stick X — 조향 |
| 왼쪽 페달 | LT — 브레이크 |
| 오른쪽 페달 | RT — 가속 |
| 하단 중앙 스틱 | Right Stick — 게임에서 시점 조작으로 지정 |
| 방향키 / ABXY / LB / RB / Back / Start | 대응하는 게임패드 버튼 |

- 게임의 조향·페달 캘리브레이션 화면에서 축 방향과 0~100% 범위, 두 페달 동시 입력을 먼저 확인합니다. 필요하면 자동 변속/자동 클러치를 게임에서 켭니다.
- **Options → Steering / pedals**에서 조향 범위·응답을 조절합니다. 기본 한쪽 범위 180°는 90° 회전에서 50%, 180°에서 100% 입력입니다. 기존 사용자 설정은 업데이트 시 유지되며 다를 수 있습니다.
- 현재 페달은 아래 10% 지점이 0%, 위 10% 지점이 100%이고, 가운데 구간은 선형입니다.
- **Options → Edit control layout**에서 버튼 위치/크기 등을 조절합니다. 레이아웃·조향 편집은 게임을 멈추고 하세요.
- 시작 시 자동 중앙 보정됩니다. 중앙이 어긋났을 때만 **Center**를 누르고 손을 떼어 재준비합니다.

가상 Xbox 패드는 물리적 레이싱 휠과 다릅니다. 특히 F1의 속도별 패드 조향 보정 때문에 **입력값과 운전석 휠 애니메이션 각도가 항상 1:1인 것은 아닙니다.** 정지/주행을 나누어 비교하고 게임의 조향 설정을 함께 조정하세요. Assetto Corsa에서는 패드 입력을 사용할 수 있지만 Wendy의 AC 텔레메트리 연동은 지원하지 않습니다.

## 5. F1과 Wendy 설정

### F1 UDP

대상: **F1 25 / 2026 Season Pack**, PC에서 게임과 Receiver를 함께 실행하는 구성입니다. 게임의 Telemetry Settings를 다음과 같이 설정합니다.

| 설정 | 값 |
|---|---|
| UDP Telemetry | **On** |
| UDP Broadcast Mode | **Off** |
| UDP IP Address | **127.0.0.1** |
| UDP Port | **20777** |
| UDP Send Rate | **30 또는 60Hz** |
| UDP Format | **2025** |

**2026 Season Pack이어도 UDP 형식은 2025를 선택합니다.** 현재 2026 전용 패킷 파서는 없습니다. Receiver에서 **F1 Engineer ON**을 선택한 뒤 실제 세션에 들어가 `Telemetry: Connected`를 확인합니다. Engineer는 기본 OFF이고 다시 실행하면 켜야 합니다. OFF여도 컨트롤러는 사용할 수 있습니다.

### 음성 입력과 응답

1. USB/Wi-Fi 컨트롤러 연결을 먼저 완료합니다.
2. PC에서 **F1 Engineer ON**, 폰에서 **Options → Wendy F1 Engineer → Wendy ON**을 켭니다.
3. **Push to talk:** PTT를 누른 채 영어로 말하고 놓습니다. 마이크 권한은 처음에 허용합니다.
4. **AUTO: system (recommended):** PTT와 같은 시스템 영어 인식 서비스를 반복 사용합니다. PTT 버튼은 숨겨지고 웬디가 말할 때는 마이크를 잠시 멈춥니다. 인식 서비스에 따라 인터넷을 사용하며, 완전히 끊김 없는 연속 녹음은 아닙니다.
5. **AUTO: on-device:** 기기에 영어 오프라인 인식 모델이 설치돼 있어야 합니다. 개발용 Fold5에서는 영어 모델이 설치되지 않아 system 모드로 검증했습니다.
6. **Retry voice service:** 음성 인식 서비스를 다시 준비합니다. 컨트롤러 연결을 다시 만드는 버튼은 아닙니다.

Wendy/AUTO는 앱을 다시 실행하면 다시 선택하는 세션 옵션입니다. 영어 TTS 음성을 설치하고 휴대전화 미디어 볼륨도 확인하세요. 게임 소리를 오인하지 않도록 헤드셋 사용을 권합니다. 앱이 배경으로 가거나 창 포커스를 잃으면 안전을 위해 조작/마이크가 해제될 수 있으므로 권한 승인은 게임을 멈추고 진행합니다.

**게임 없이도 `Radio check`에 응답해야 합니다.** 타이어·간격 등 실제 차량 수치 질문은 신선한 텔레메트리가 있어야 답합니다.

### 선택 사항: CPU 음성 의도 분류 모델

모델 없이도 정형 문장은 규칙 기반으로 처리하며, 모델이 없거나 실패하면 제한된 Rules fallback을 사용합니다. 더 다양한 표현을 분류하려면 다음을 실행합니다.

```powershell
powershell -NoProfile -File .\tools\prepare-wendy-model.ps1
```

스크립트가 고정 버전 llama.cpp CPU 런타임과 Qwen3 0.6B Q8_0를 다운로드하고 해시를 검증합니다. 결과는 `release\receiver\wendy`입니다. EXE를 이동할 때 이 폴더도 함께 이동하고 포함된 제3자 라이선스를 유지하세요. 대용량 다운로드가 필요합니다.

이 선택 기능은 **CPU 전용 로컬 모델**을 사용합니다. 초기 설계 이후 추가된 기능이며, Ollama·GPU 추론·OpenAI API·화면 캡처/OCR은 사용하지 않습니다. PC에는 음성 파일 대신 인식된 텍스트가 전달됩니다. Android 시스템 인식 공급자의 클라우드 사용 여부는 별개입니다.

### 질문 예와 현재 제한

- `Radio check`, `How are my tyres?`, `Tyre temperature`, `What's the gap ahead?`, `What's the gap behind?`
- `How much fuel do I have?`, `What's my ERS?`, `Any damage?`, `Is it going to rain?`, `What's the gap to the leader?`
- `What flag is out?`, `What lap am I on?`, `What's my position?`, `Do I need pit in?`, `How was my last lap?`, `Coach me`

플래그·SC/VSC·손상·마모·연료 등 중요 이벤트를 쿨다운과 함께 알립니다. 첫 완전한 유효 랩을 기준으로 수집한 뒤 다음 랩부터 비교 코칭을 할 수 있습니다. 최적 코너 속도나 최적 전략을 보장하는 기능은 아닙니다.

**`Box box` / `Box this lap`은 피트 리마인더만 저장하며 게임의 피트 요청 버튼을 누르지 않습니다.** 브레이크 바이어스·디퍼렌셜·타이어·윙 설정을 음성으로 실제 변경하는 기능은 아직 지원하지 않습니다. 확인할 수 없는 명령을 성공했다고 답하지 않습니다.

전체 문장과 조건: [Wendy 대화 목록](docs/WENDY_CONVERSATIONS.md) · [상세 설명과 이전 변경 이력](docs/WENDY.md).

## 6. 문제가 생겼을 때

| 증상 | 확인할 내용 |
|---|---|
| USB 기기가 없음 | 데이터 케이블, 잠금 해제, USB 디버깅 승인, `adb devices`의 `device`, ADB 설치 경로 |
| `No Wi-Fi reply` | 같은 LAN, 새 QR, 리시버 실행 여부, 개인 네트워크 방화벽, 게스트/AP 격리·VPN 영향 |
| 운전은 되지만 Wendy가 안 됨 | PC Engineer ON + 폰 Wendy ON, TCP 26762, 시스템 영어 인식/마이크 권한/미디어 볼륨 |
| `Telemetry: Waiting` | 게임 세션 진입, UDP 2025 / 127.0.0.1 / 20777, 다른 앱의 포트 점유 |
| AUTO 오류 반복 | 우선 AUTO system 선택, 영어 인식 서비스와 인터넷 확인, Retry voice service |
| 폰 입력은 오는데 게임이 반응하지 않음 | ViGEmBus, 게임 컨트롤러 지정, 중복 리시버/매핑 프로그램, PC의 Game output·Stops·Pad errors 표시 |
| 주행 중 조작 해제 | 게임을 멈추고 폰 **Options → Controller diagnostics**와 PC의 마지막 해제 사유 확인 |

실제 입력/센서/출력이 끊기면 오래된 가속을 유지하지 않고 해제합니다. 복구 후 페달에서 손을 떼고 조향을 중립으로 유지해야 다시 활성화됩니다. 진단 화면·QR을 공유할 때 연결 키나 다른 개인정보가 노출되지 않게 주의하세요.

## 7. 개발·검증

프로젝트 루트에서:

```powershell
dotnet run --project windows/tests/PhoneWheel.Tests/PhoneWheel.Tests.csproj -c Release
cd android
.\gradlew.bat testDebugUnitTest assembleDebug lintDebug
cd ..
python verification/run_verification.py
```

최종 0.5.3 기록: Android **43 unit tests**, Windows **28 test groups** 통과. 실기기 Wi-Fi에서 AUTO/PTT 전환·설정·TTS 중 페달 50% 유지와 해제를 null 출력 리시버로 검증했습니다. **실제 게임에 테스트 입력을 보내는 검증이나 장시간 무중단 레이스 인증은 아닙니다.** 100ms 신선도 검사와 150ms 입력 단절 해제 정책은 유지합니다.

[0.5.3 검증 결과와 제한](artifacts/validation/WENDY_053.md) · [하드웨어 테스트 계획](docs/HARDWARE_TEST_PLAN.md) · [통신 규격](docs/PROTOCOL.md) · [이전 README 기록](README_HISTORY.md)

`tools/test_phone_isolation.py`는 개발용 테스트 APK와 연결된 기기가 필요한 **명시적 실기기 테스트**입니다. 마이크 테스트는 별도 opt-in이며 게임을 멈춘 상태에서만 실행하세요. 일반 설치/사용에는 필요하지 않습니다.
