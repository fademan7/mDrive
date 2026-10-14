> [English / current overview](README.md) · 한국어 안내입니다. 기존 README를 보존한 문서이므로 일부 이전 버전 설명이 포함되어 있습니다. 최신 기능·지원 범위는 영문 README를 기준으로 확인하세요.

# mDrive

Android 휴대전화를 Windows 레이싱 게임용 컨트롤러로 사용하는 프로젝트입니다. 휴대전화를 돌려 조향하고, 화면 왼쪽 브레이크·오른쪽 가속 페달을 조작합니다. **Wendy F1 Engineer**는 F1 UDP 텔레메트리를 읽고 영어 음성 질문에 답하거나 레이스 경고를 알려줍니다.

현재 소스: **Android 0.5.16 / Receiver 0.5.16 — Wi-Fi 전용** · 개발·실기 확인 환경: Windows 11 x64, Galaxy Z Fold5 및 SM-S942N.

0.5.16: F1 텔레메트리가 아니라 **게임이 가상 Xbox 패드에 보내는 진동**을 전달합니다. 게임에서 패드 진동을 켜세요. 지속 효과의 잘못된 500ms 만료와 송신 사이의 짧은 효과 누락을 수정했습니다. QR 창의 `Rumble: Xbox events → phone packets`에서 수신/전달을 확인할 수 있습니다. 실제 Xbox→Wi-Fi→SM-S942N 경로와 사용자의 세 단계 체감 확인을 통과했습니다. [검증 결과](artifacts/validation/XINPUT_RUMBLE_0516.md)

0.5.15: APK와 리시버를 함께 업데이트하세요. 통신 장애 시 150ms 중립 출력과 100ms 인증 기한은 유지하고, 같은 활성 운전 세션의 새 입력이 인증되면 손을 떼는 대기 없이 복귀합니다. 센서·포커스·보정·출력 오류에는 적용하지 않습니다. 게임에서 보내는 진동은 기본 활성화됩니다(게임의 진동 설정도 켜야 합니다). Wendy는 Audio/Radio check, 데미지를 포함한 피트 질문, 짧은 부위별 답변을 지원하며 자동 브리핑은 마지막 답변 후 12초 이상, 이전 안내 후 45초 이상인 여유 구간에서 평가합니다. 실제 F1 장시간 재검증은 별도입니다.

0.5.13부터 PC 영어 인식은 **Vosk 오프라인 모델 동봉**으로 바뀌었습니다. Windows 영어 인식 언어팩이 필요하지 않습니다. 40MB 모델을 기본으로 선택했습니다. 128MB 비교 모델은 실시간 시험에서 긴 처리 지연이 나와 배포 기본값에서 제외했습니다. 영어 **출력**은 여전히 Windows 영어 TTS 음성이 필요하며 QR 창의 **Audio check**에서 확인할 수 있습니다. 0.5.14에서는 Android 앱 복귀 시 소켓 자동 복구도 보완했습니다.

입력·출력은 기본 PC이며 QR 창에서 각각 PC/Android를 선택합니다. Wendy 전용 볼륨(0–100%)과 설정 저장, 축소 QR, 별도 콘솔 없는 창은 유지합니다.

Android 0.5.9: 기본 조향 범위는 **좌우 각각 120°**이며 선형입니다.
폰 0°=0%, 60°=50%, 120°=100% 입력(180° 휠 기준 1.5배), 범위 밖은 최대값으로 제한합니다.
업데이트 후 첫 실행에서 조향 설정만 한 번 적용하며, 이후 사용자가 바꾼 설정은 유지합니다.
게임의 조향 보정·휠 애니메이션에 따라 실제 화면 각도는 별도 확인이 필요합니다.

0.5.11: OCR·메뉴 자동 조작 시험을 철회했습니다. 게임 명령은 F1 자체 음성 인식을 사용합니다.
LB를 누르면 Wendy의 인식·TTS를 멈추고, 놓은 뒤 8초 후 AUTO를 재개합니다.
재입력하면 대기 시간이 연장됩니다. `GAME RADIO` 표시 중 연결·ARM·페달·조향은 그대로입니다.
게임 메뉴 종료 감지가 아닌 시간 기반 대기입니다. 필요하면 LB를 더 길게 유지하세요.
LB 게임 입력은 그대로이며, F1의 라디오 매핑과 PC 마이크는 게임에서 설정합니다.
휴대전화 음성을 F1에 전송하거나 키보드 T를 자동 입력하지 않습니다.

기존 패드 화면·Wendy Summary·타이어 수명·셋업 제안은 유지합니다. LB는 더 이상 Wendy PTT를 시작하지 않습니다. BB/Diff 직접 단축키 실행은 **0.5.14에서 제거**했습니다. Box/다음 타이어/윙 자동 조작은 지원하지 않습니다. [설정·지원 범위](docs/WENDY_057.md).

0.5.6은 짧은 통신 공백 뒤 **새 인증 왕복으로 증명된 입력만 빠르게 복귀**하도록 개선하고, 중력 기준 자동 중앙 정렬과 Wendy의 부위별 데미지·피트 안내/조용한 구간의 상태 브리핑을 추가했습니다. **APK와 Receiver를 함께 업데이트**해야 합니다. 150ms 입력 만료·100ms 인증 기한은 그대로이며 오래된 페달/조향을 유지하지 않습니다. 장시간 실제 F1 dropout의 완전 해결은 아직 확인되지 않았습니다. [0.5.6 검증과 제한](artifacts/validation/CONTROLLER_WENDY_056.md) · [진단 방법](docs/CONTROLLER_DROPOUT.md)

제작: **fademan7 / neojshin** · [홈페이지](https://fademan7.github.io/) · [이메일](mailto:neojshin@gmail.com)

## 다운로드 · 빠른 설치

[0.5.16 프리뷰 릴리스](https://github.com/fademan7/mDrive/releases/tag/v0.5.16)에서 PC와 Android를 함께 업데이트하세요.

1. **PC:** [Windows x64 설치 파일](https://github.com/fademan7/mDrive/releases/download/v0.5.16/mDrive-0.5.16-Setup-x64.exe)을 실행합니다. 리시버·오프라인 영어 인식 모델·공식 ViGEmBus 설치본이 포함됩니다. 실행 중인 게임과 mDrive는 먼저 종료하세요.
2. **Android:** [mDrive APK](https://github.com/fademan7/mDrive/releases/download/v0.5.16/mDrive-0.5.16.apk)를 휴대전화에서 설치합니다. Android 13 이상이 필요하며 개발용 debug 서명 APK입니다. 기존 설치와 서명이 다르면 업데이트할 수 없으므로 데이터를 지우기 전에 확인하세요.
3. PC와 휴대전화를 같은 Wi-Fi에 연결하고 PC의 mDrive를 실행합니다. 휴대전화 메뉴의 **QR connection**으로 PC QR을 스캔하세요. 게임 연결에 USB/ADB는 필요하지 않습니다.
4. 게임에서 Xbox 패드를 선택하고 조향·페달을 매핑합니다. 진동을 켜세요. Wendy를 사용할 때는 아래 **F1 UDP 설정**과 **Audio check** 안내를 따르세요.

설치 파일은 미서명 프리뷰입니다. 보안 경고를 우회하거나 보안 기능을 끄지 마세요. 릴리스에 SHA-256 확인 파일도 제공합니다. 다른 Android 기기·깨끗한 Windows 환경·장시간 실제 레이스 안정성은 별도 확인이 필요합니다.

소스와 빌드 스크립트는 이 저장소에, 설치 바이너리는 GitHub Releases에 보관합니다. 직접 빌드하려면 아래 절차를 따르세요.

## 1. 준비할 것

- **PC:** Windows x64. 가상 Xbox 360 패드용 [ViGEmBus 공식 배포본](https://github.com/nefarius/ViGEmBus/releases/tag/v1.22.0)을 설치합니다. 유지보수가 종료된 드라이버이므로 해당 PC에서 설치·게임 호환성을 확인해야 합니다. 보안·안티치트 기능을 끄지 마세요.
- **휴대전화:** Android 13(API 33) 이상, 회전 센서. 게임 입력은 Wi-Fi로만 연결합니다. USB 디버깅은 개발용 APK 설치에만 필요합니다.
- **빌드 도구:** .NET SDK **10.0.400**(`global.json` 고정), JDK **17**, Android SDK **API 37**, Build Tools **37.0.0**, Platform-Tools(ADB). Gradle **9.5.0**과 Android Gradle Plugin **9.3.2**는 프로젝트에서 고정합니다.
- **Wendy:** PC 입력은 동봉 Vosk 모델과 기본 마이크를 사용합니다. PC 출력에는 Windows 영어 TTS 음성과 기본 출력 장치가 필요합니다. Android를 선택한 기능에는 휴대전화의 영어 음성 인식 서비스/마이크 권한 또는 영어 TTS가 필요합니다. Android 시스템 인식은 공급자에 따라 인터넷을 사용할 수 있습니다.

## 2. 처음 빌드하고 설치하기

### 0.5.14 사용 변경

- Wendy 활성화는 **F1 2025 / 2026 Season Pack 전용**입니다. 다른 게임에서도 컨트롤러는 사용할 수 있지만 Wendy 레이싱 데이터는 지원하지 않습니다.
- 텔레메트리가 없거나 일시정지 상태에서는 자발적인 레이스 질문/안내를 하지 않습니다. Radio check, 인사, 안부, 감사, 격려, 짧은 농담을 영어로 주고받을 수 있습니다. 규칙 기반의 제한된 짧은 대화이며 자유 주제 지식 챗봇은 아닙니다.
- 주행 중에는 레이싱 대화에 집중하고, 여유 있는 직선에서 간격/마모/연료/날씨/차량상태/타이어온도/예보를 순환 안내합니다. 오래된 값은 말하지 않습니다.
- **PC voice**에서 이 앱에 제공되는 영어 TTS 음성을 선택합니다. en-GB Male / en-US Female 음성이 설치되어 있으면 고를 수 있습니다. 특정 인물/캐릭터 음성을 복제하지 않으며, Windows 음성마다 억양과 품질은 다릅니다. Android 출력 음성은 Android TTS 설정을 사용합니다.
- 기본 음량 100%. 구버전 85% 기본값만 최초 로드에서 100%로 올리고 다른 기존 사용자 값은 보존합니다.
- 휴대전화가 다른 앱으로 이동하면 안전하게 중립 출력합니다. 돌아오면 Wi-Fi 상태를 확인하고 오래 응답 없는 소켓을 다시 열며, 최신 센서와 기존 중립 확인 후 자동 ARM합니다. 통신이 정상인 짧은 지연에는 소켓을 바꾸지 않습니다. ARM/이전 페달 입력은 복원하지 않습니다.
- 앱이 시스템에 의해 재생성되면 Android가 보관한 Activity 상태에서 페어링만 복원합니다. PC 리시버를 재시작해 QR 키가 바뀌면 QR 재스캔은 여전히 필요합니다.

### 통합 설치 파일 (미서명 프리뷰)

빌드된 `mDrive-0.5.16-Setup-x64.exe`는 리시버·Vosk 영어 모델·공식 ViGEmBus 설치본을 포함합니다. 게임을 멈추고 리시버를 닫은 뒤 실행하세요. 드라이버가 이미 있으면 유지하고, 없으면 공식 설치본을 실행합니다. 관리자 승인이 필요할 수 있으며 재부팅 요구·실패를 성공으로 숨기지 않습니다. 앱 삭제 시 공유 드라이버나 사용자 오디오 설정을 지우지 않습니다.

설치 후 QR 창에서 **Audio check → Wendy · F1 2025 / 2026 → Radio check** 순서로 확인합니다. 영어 TTS가 없다면 Windows에서 영어 음성을 추가하거나 Android 출력을 선택합니다. 마이크 권한·Windows 언어·방화벽은 자동 변경하지 않습니다. Windows 방화벽 안내가 나오면 개인 네트워크만 허용하세요.

현재 앱/설치 파일은 자체 코드 서명이 없는 프리뷰입니다. 공개 배포 전 코드 서명, 드라이버 없는 깨끗한 Windows 환경, 실제 헤드셋·F1 장시간 검증이 필요합니다. 보안 경고를 우회하지 마세요.

설치 파일 재현 빌드(PowerShell):

```powershell
.\tools\build-app-icon.ps1
.\tools\publish-receiver.ps1 -OutputDirectory release/distribution-0516
.\tools\prepare-speech-assets.ps1
.\tools\prepare-distribution-notices.ps1
.\tools\build-installer.ps1 -Compiler 'C:\path\to\Inno Setup 6\ISCC.exe'
```

출력: `release/installer/mDrive-0.5.16-Setup-x64.exe`. Inno Setup 6.7.3으로 확인했습니다. 컴파일러의 상업용 사용에는 해당 라이선스를 별도 확인하세요. 모델/드라이버 다운로드의 SHA-256과 공식 드라이버 서명을 빌드 시 검사합니다. 대용량 파일은 Git에 포함하지 않습니다. [배포 고지](installer/THIRD-PARTY.txt).

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

출력: `release\receiver\PhoneWheel.Receiver.exe`. 자체 포함 단일 EXE이므로 빌드된 Receiver를 실행할 PC에는 .NET SDK가 필요하지 않습니다. **ViGEmBus는 EXE에 포함되지 않습니다. 게임 연결에 ADB는 필요 없습니다.** 실행 중인 리시버를 교체할 때는 게임을 멈추고 리시버를 먼저 닫으세요.

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

기존 개발 APK 위에 업데이트하려면 같은 서명 키를 사용해야 합니다. 이 작업 환경의 기존 키는 `.tools/android-user/debug.keystore`이며, 빌드 전에 `$env:ANDROID_USER_HOME = 'C:\paper\mdrive\.tools\android-user'`로 지정합니다. 다른 PC에서는 자신의 기존 키 위치를 사용하세요. 서명 불일치 시 앱을 삭제하지 말고 키를 먼저 확인해야 레이아웃 등 사용자 설정을 보존할 수 있습니다. 개인 키는 Git에 올리지 않습니다.

휴대전화에서 **개발자 옵션 → USB 디버깅**을 켜고, USB를 연결해 이 PC의 디버깅을 허용한 다음 설치합니다.

```powershell
& "$env:ANDROID_HOME\platform-tools\adb.exe" devices
& "$env:ANDROID_HOME\platform-tools\adb.exe" install -r .\android\app\build\outputs\apk\debug\app-debug.apk
```

목록에 `device`가 보여야 합니다. `unauthorized`라면 잠금을 풀고 승인 창을 확인하세요. 앱 이름은 휴대전화에서 **PhoneWheel**로 표시됩니다. 업데이트는 기존 앱 위에 설치하고 데이터를 삭제하지 마세요. 다른 PC에서 새 debug 서명으로 빌드하면 기존 설치와 서명이 달라 업데이트가 거부될 수 있습니다. 이 경우 기존 서명 키를 사용해야 하며, 무작정 앱을 삭제하면 저장한 배치와 설정이 사라집니다. 서명 키는 저장소에 올리지 않습니다.

## 3. PC와 연결하기

리시버는 **한 개만 실행**하세요. 게임을 멈춘 상태에서 연결하고 입력이 정상인지 확인한 후 주행합니다.

`START_RECEIVER.cmd`, `START_WIFI.cmd` 또는 Receiver EXE를 실행하면 모두 Wi-Fi QR 연결로 시작합니다. USB 자동 감지·ADB reverse·USB TCP 모드는 제거됐습니다. 오래된 `--usb` 옵션과 USB QR은 거절됩니다.

### Wi-Fi — 같은 로컬 네트워크에서 연결

1. PC와 휴대전화를 같은 로컬 네트워크에 연결합니다. 게스트 Wi-Fi의 기기 간 통신 차단/AP 격리를 피하세요.
2. `START_RECEIVER.cmd` 또는 `START_WIFI.cmd`를 더블클릭합니다.
3. Windows 방화벽에서 Receiver의 **개인 네트워크** 통신을 허용합니다. 방화벽 자체를 끄거나 인터넷 공유기 포트를 개방할 필요는 없습니다.
4. 폰의 **mDrive → QR connection**에서 바로 PC 창의 QR을 스캔합니다. 카메라/로컬 네트워크 권한을 요청하면 허용합니다.
5. 페달에서 손을 떼고 편한 자세로 고정해 `Driving active`를 확인합니다.

PC 리시버를 재시작하면 새 세션이 만들어집니다. **새 QR로 다시 연결**하세요. PC 주소가 바뀌었을 때도 리시버를 다시 실행하고 새 QR을 사용합니다. QR과 연결 키는 공개하지 마세요. USB 케이블 연결 여부로 통신 모드가 바뀌지 않습니다.

| 용도 | 연결 |
|---|---|
| Wi-Fi 컨트롤러 | PC LAN 주소 / UDP **26760** |
| Wendy 음성 명령·응답 | TCP **26762** |
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
- **mDrive → Steering / pedals**에서 조향 범위·응답을 조절합니다. 기본 한쪽 범위 180°는 90° 회전에서 50%, 180°에서 100% 입력입니다. 기존 사용자 설정은 업데이트 시 유지되며 다를 수 있습니다.
- 현재 페달은 아래 10% 지점이 0%, 위 10% 지점이 100%이고, 가운데 구간은 선형입니다.
- **mDrive → Control layout**에서 버튼 위치/크기 등을 조절합니다. 레이아웃·조향 편집은 게임을 멈추고 하세요. 기존 패드 화면이 항상 표시되며 모드 전환 버튼은 없습니다.
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

1. Wi-Fi 컨트롤러 연결을 먼저 완료합니다.
2. PC QR 창에서 **Wendy · F1 2025 / 2026**를 켭니다. **Microphone**과 **Voice output**을 각각 **PC / Android**로 선택합니다. 기본값은 PC/PC이며 네 가지 조합을 모두 지원합니다. 폰은 연결 후 자동으로 상태를 받습니다.
3. **게임 라디오:** F1에서 LB를 라디오 메뉴에 매핑하고 PC 마이크로 말합니다. LB를 누르는 동안과 놓은 뒤 8초 동안 Wendy는 듣거나 말하지 않습니다. 게임 라디오가 더 오래 열려 있으면 LB를 다시 누르거나 유지하세요.
4. **PC 입력:** Windows 기본 마이크와 동봉 `vosk-model-small-en-us-0.15`를 사용합니다. CPU 전용 별도 프로세스에서 동작하며 Windows 영어 인식 엔진은 필요하지 않습니다. 음성은 파일에 저장하거나 외부로 보내지 않습니다. 모델이 없으면 설치 파일로 재설치하거나 **Android 입력**을 선택하세요. 단독 EXE만 복사하면 모델이 누락되므로 `speech` 폴더도 필요합니다. `--voice-probe`는 모델/영어 출력 음성 상태를 진단용 표준 출력에 씁니다.
5. **Android 입력:** 시스템 영어 인식을 반복 사용합니다. 권한이 필요하면 폰 **mDrive → Wendy → Connect voice status**에서 허용합니다. **Retry voice**는 Android 음성 서비스만 재시작하며 컨트롤러 연결을 바꾸지 않습니다.
6. **Wendy volume:** QR 창에서 0–100%로 조절합니다. PC 출력은 현재 재생에도 반영하고 Android 출력은 다음 응답부터 반영합니다. PC는 Windows 기본 재생 장치(헤드셋)를 사용합니다. Windows 볼륨 믹서에서 게임 볼륨을 낮추면 Wendy가 더 잘 들립니다. 자동 게임 볼륨 감소/100% 초과 증폭은 하지 않습니다.

입력/출력/볼륨은 PC의 `%LOCALAPPDATA%\mDrive\audio.json`에 저장하며 Wendy 활성화는 실행할 때 켭니다. 양쪽에서 소리가 중복 재생되지 않습니다. 인식·TTS는 컨트롤러 워커와 분리되어 있으며 음성 데이터 자체는 PC/폰 사이로 전송하지 않습니다. Android 출력은 휴대전화 미디어 볼륨도 적용됩니다. Wendy 응답·LB 게임 라디오 중에는 마이크를 멈춥니다. 앱이 배경으로 가거나 창 포커스를 잃으면 기존 안전 규칙대로 조작/Android 마이크가 해제될 수 있으므로 권한 승인은 게임을 멈추고 진행합니다.

**게임 없이도 `Radio check`에 응답해야 합니다.** 타이어·간격 등 실제 차량 수치 질문은 신선한 텔레메트리가 있어야 답합니다.

### 선택 사항: CPU 음성 의도 분류 모델

주요 레이싱 질문은 문장 앞의 요청 표현과 핵심 주제·방향·바퀴를 분리해 즉시 처리합니다. 예를 들어 `Tell me the time gap`과 `Could you tell me the gap ahead please`는 앞차 간격으로 인식합니다. 알려진 질문은 모델을 로드하지 않습니다. 애매하게 여러 주제를 함께 묻는 경우 하나씩 질문하도록 안내하며, 나머지 표현은 선택 모델 또는 Rules fallback을 사용합니다. 더 다양한 표현을 분류하려면 다음을 실행합니다.

```powershell
powershell -NoProfile -File .\tools\prepare-wendy-model.ps1
```

스크립트가 고정 버전 llama.cpp CPU 런타임과 Qwen3 0.6B Q8_0를 다운로드하고 해시를 검증합니다. 결과는 `release\receiver\wendy`입니다. EXE를 이동할 때 이 폴더도 함께 이동하고 포함된 제3자 라이선스를 유지하세요. 대용량 다운로드가 필요합니다.

이 선택 기능은 **CPU 전용 로컬 모델**을 사용합니다. 초기 설계 이후 추가된 기능이며, Ollama·GPU 추론·OpenAI API·화면 캡처/OCR은 사용하지 않습니다. PC에는 음성 파일 대신 인식된 텍스트가 전달됩니다. Android 시스템 인식 공급자의 클라우드 사용 여부는 별개입니다.

### 질문 예와 현재 제한

- `Tell me the time gap`, `Could you tell me the gap behind please`, `Radio check`, `How are my tyres?`, `Tyre temperature`, `What's the gap ahead?`, `What's the gap behind?`
- `How much fuel do I have?`, `What's my ERS?`, `Any damage?`, `Is it going to rain?`, `What's the gap to the leader?`
- `What flag is out?`, `What lap am I on?`, `What's my position?`, `Do I need pit in?`, `How was my last lap?`, `Coach me`

`Race update`는 순위·랩·플래그·앞뒤 간격을 짧게 요약합니다. `Say again`은 마지막 정보 질문을 최신 텔레메트리로 다시 조회합니다. 타이어 질문 후 `What about the rear left?`도 가능합니다. 후속 질문 문맥은 30초 또는 세션 변경·재연결 시 초기화되며 설정 명령·승인은 반복 실행하지 않습니다. `Hello`, `How are you`, `Thanks`, `Let’s chat`, `I’m nervous`에는 짧게 응답합니다. 범용 지식 챗봇이나 자유로운 장문 대화 기능은 아닙니다.

플래그·SC/VSC·손상·마모·연료 등 중요 이벤트를 쿨다운과 함께 알립니다. 첫 완전한 유효 랩을 기준으로 수집한 뒤 다음 랩부터 비교 코칭을 할 수 있습니다. 최적 코너 속도나 최적 전략을 보장하는 기능은 아닙니다.

**`Box box` / `Box this lap`은 피트 리마인더만 저장하며 게임의 피트 요청 버튼을 누르지 않습니다.** BB/Diff 키 매핑 UI와 실제 키 출력은 0.5.14에서 제거했습니다. 해당 값 조회와 수동 설정 조언만 제공하며 자동 변경을 약속하지 않습니다. 타이어·윙 자동 조작은 미지원입니다. [지원 명령/설정/제한](docs/WENDY_057.md).

전체 문장과 조건: [Wendy 대화 목록](docs/WENDY_CONVERSATIONS.md) · [상세 설명과 이전 변경 이력](docs/WENDY.md).

## 6. 문제가 생겼을 때

| 증상 | 확인할 내용 |
|---|---|
| `No Wi-Fi reply` | 같은 LAN, 새 QR, 리시버 실행 여부, 개인 네트워크 방화벽, 게스트/AP 격리·VPN 영향 |
| 운전은 되지만 Wendy가 안 됨 | PC Wendy · F1 2025 / 2026 / Audio check, Vosk speech 폴더, 기본 마이크·영어 TTS, Android 사용 시 TCP 26762·마이크 권한·미디어 볼륨 |
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

이전 0.5.3 기록: Android **43 unit tests**, Windows **28 test groups** 통과. 실기기 Wi-Fi에서 AUTO/PTT 전환·설정·TTS 중 페달 50% 유지와 해제를 null 출력 리시버로 검증했습니다. **실제 게임에 테스트 입력을 보내는 검증이나 장시간 무중단 레이스 인증은 아닙니다.** 100ms 신선도 검사와 150ms 입력 단절 해제 정책은 유지합니다.

[0.5.3 검증 결과와 제한](artifacts/validation/WENDY_053.md) · [하드웨어 테스트 계획](docs/HARDWARE_TEST_PLAN.md) · [통신 규격](docs/PROTOCOL.md) · [이전 README 기록](README_HISTORY.md)

`tools/test_phone_isolation.py`는 개발용 테스트 APK와 연결된 기기가 필요한 **명시적 실기기 테스트**입니다. 마이크 테스트는 별도 opt-in이며 게임을 멈춘 상태에서만 실행하세요. 일반 설치/사용에는 필요하지 않습니다.
