# Gate 검증 기록

실행일: 2026-09-12, Windows 11 x64 `10.0.26200`.

## Gate 0 — 환경과 빌드

결과: **PASS (소프트웨어 빌드 범위)**

- 저장소 루트와 상위 경로를 확인했으며 이 작업 폴더에 적용되는 `AGENTS.md`는 없었습니다.
- Python은 Codex 번들 3.12 런타임, .NET SDK는 프로젝트 로컬 10.0.400을 사용했습니다.
- Microsoft OpenJDK 17.0.20.1, Gradle 9.5.0, AGP 9.3.2, 내장 Kotlin 2.3.20, Android Platform/build-tools 37을 사용했습니다.
- `PhoneWheel.Probe`, `PhoneWheel.Host`, Android debug APK가 최종 빌드됐습니다.

실행 명령:

```powershell
.tools/dotnet/dotnet.exe build windows/src/PhoneWheel.Probe/PhoneWheel.Probe.csproj -c Release
.tools/dotnet/dotnet.exe build windows/src/PhoneWheel.Host/PhoneWheel.Host.csproj -c Release
android/gradlew.bat --no-daemon testDebugUnitTest assembleDebug
```

최신 Android 결과: `CoreTest` 10 tests, failures 0, errors 0. Android Lint 0 errors, 10 warnings. 최종 APK 크기 6,756,273 bytes, SHA-256 `3525684276622509BE6F36536A0C68B586304C0A657F730BECAA47E90298AD47`.

## Gate 1 — Windows 가상 패드

결과: **PASS (ViGEm→XInput), 게임 인식 NOT RUN**

설치된 버스의 읽기 전용 확인:

- Device: Nefarius Virtual Gamepad Emulation Bus
- Driver: 1.21.442.0, provider Nefarius Software Solutions e.U.
- Signed: true, signer Microsoft Windows Hardware Compatibility Publisher

`PhoneWheel.Probe --backend vigem --step-seconds 0.3`와 `tools/xinput_probe.py --interval-ms 4`를 동시에 실행했습니다. XInput slot 0에서 다음 고유값을 실제 관찰했습니다.

| 단계 | LX | LT | RT |
|---|---:|---:|---:|
| 중립 | 0 | 0 | 0 |
| 좌/우 50% | -16384 / 16384 | 0 | 0 |
| 브레이크 50% | 0 | 128 | 0 |
| 가속 50% | 0 | 0 | 128 |
| 동시 100% | 0 | 255 | 255 |

초기 시험에서 장치 열거 직후 LX `-3356` transient가 발견됐습니다. SDK가 unchanged zero report를 반영하지 않는 조건으로 판단해 1-count pulse 직후 neutral report를 제출하도록 수정했고, 재시험에서는 초기/종료 중립 0과 위 값만 관찰했습니다. 원본은 `xinput-probe.csv`, 수정 후 증거는 `xinput-probe-final.csv`입니다.

F1 25와 원작 Assetto Corsa를 실행하지 않았으므로 두 게임 호환성은 NOT RUN입니다.

## Gate 2 — Android 입력

결과: **구현·빌드 PASS, Fold5 실기 NOT RUN**

센서 전용 HandlerThread, game rotation vector 우선 선택, 상대 quaternion twist, 조절 가능한 반경/곡선/0–30ms low-pass, 100ms sensor stale, pointer ID 기반 독립 페달·XInput 버튼, cancel/pause 즉시 해제를 구현했습니다. 왼쪽 브레이크/오른쪽 가속/상단 버튼 기본 배치와 위치·크기 편집 영속화도 빌드됐습니다. ADB 장치 목록이 비어 있어 APK 설치, 실제 센서율·좌우 부호·각도 지그·동시 터치·접기/펼치기·배치 사용성·진동 능력은 측정하지 않았습니다.

## Gate 3 — PWR1와 안전 상태

결과: **PASS (unit/interoperability/local UDP), USB/Wi-Fi 실기 NOT RUN**

- Python 참조 37 tests PASS.
- C# 8 test groups PASS: 고정 바이트/HMAC, 모든 packet codec, 변조·truncation·NaN, uint32 wrap, 300ms dwell/ARM edge, timeout/rearm, haptic arbitration, F1 합성 layout.
- Kotlin 최신 10 tests PASS.
- C#이 만든 인증 Control을 Kotlin이 decode한 뒤 Kotlin datagram을 만들고 C#이 decode하는 실제 파일 교환 PASS.
- C# Host와 Python mock phone을 localhost UDP로 실행: Hello 인증, endpoint lock, Status challenge ACK, 300ms 중립, ARM 상승, LT/RT 동시 입력, 완전 침묵 watchdog을 확인했습니다.
- 최종 회귀의 마지막 mock Control PC monotonic timestamp `466698315186600ns`, 첫 중립 제출 `466698479155000ns`: **163.968ms** (170ms 목표 이내).
- phone socket 종료 뒤 Windows `WSAECONNRESET`으로 Host가 죽는 결함을 재현하고, 이를 단절로 처리하도록 수정했습니다. 수정 후 Host exit code 0.

증거: `host-watchdog-final.csv`, `host-watchdog-final.out.txt`, `interop/`.

## Gate 4–6

- F1 2025 Motion Ex의 공식 문서 대조 일부 layout은 C#/Python 합성 fixture PASS. 실게임 packet과 replay는 NOT RUN.
- EA 2026 전용 parser는 미구현/NOT RUN.
- 원작 AC shared memory는 공식 Kunos 구조체를 확보하지 못해 disabled.
- Android haptic lease/priority/no-queue logic은 구현 및 순수 로직 test PASS. Fold5 actuator와 사람 구별 시험은 NOT RUN.
- 30분 주행, 물리 motion-to-photon, Wi-Fi/USB 비교는 NOT RUN.

이 기록의 PASS는 적힌 범위만 뜻하며 게임 호환성·Fold5 정확도·실제 진동 판정의 PASS가 아닙니다.

## 2026-09-12 후속 — USB 설치와 더블클릭 리시버

- ADB에서 `SM_F946N` 장치 확인. `adb install -r android/app/build/outputs/apk/debug/app-debug.apk` → `Success`; Android release `16`.
- `am start -n dev.phonewheel/.MainActivity` 실행 후 실제 화면에서 왼쪽 브레이크, 오른쪽 가속 및 상단 버튼 표시를 확인했습니다. `phonewheel-device-screen.png` 참고.
- `tools/publish-receiver.ps1`로 .NET 런타임/native client를 포함한 단일 `release/receiver/PhoneWheel.Receiver.exe` 게시. 별도 런타임 설치 없는 실행과 ViGEm 초기화, 0.5초 정상 종료 exit 0 확인.
- C# 테스트 재실행: 8/8 groups PASS.
- Wi-Fi 양단 IP를 확인했지만 ADB 연결이 다시 끊겨 센서/터치 → Wi-Fi → XInput 경로는 아직 NOT RUN입니다. 연결 키 자동 입력 성공으로 기록하지 않습니다.
- Windows Wi-Fi 프로필이 Public이며, 폰 IP 한정 영구 방화벽 규칙 생성은 자동 승인 검토에 의해 거부되어 수행하지 않았습니다. 사용자 승인 또는 Windows의 프로그램 네트워크 허용이 필요할 수 있습니다.

## 후속 — 전체 높이·상단 여유 페달·연속 ±180° 조향

- `testDebugUnitTest assembleDebug lintDebug` BUILD SUCCESSFUL. 10 tests / 0 failures / 0 errors, Lint 0 errors / 10 warnings.
- 합성 회전 경로: 0 → 오른쪽 179/180/181 → 중앙 → 왼쪽 179/180/181 → 중앙. 매 표본 quaternion 부호가 번갈아 바뀌어도 출력 방향·크기 및 중앙 복귀 일치.
- 센서 간격 100ms 초과 시 추적 폐기 및 명시적 보정 요구. 최초 접촉 0%, 상단 여유 지점 100%, 유지, 해제 0% 테스트 PASS.
- 실제 Fold5에 중간 빌드를 설치해 세로 전체 페달 표시 확인. 화면 생성 이전 insets 접근으로 생긴 실행 오류를 수정하고 정상 시작 확인.
- USB 재연결 후 최종 빌드 설치 Success, am start Status ok. `phonewheel-edge-pedals.png`는 최종 전체화면이며 UI dump에서 조작 View bounds `[0,0][2316,904]` 확인.
- ADB로 실제 Fold5 터치 계층에 DOWN(2000,800) → MOVE(2000,550) → MOVE(2000,300) → UP을 입력했습니다. 각각 가속 50%, 100%, 해제 0%를 화면에서 확인. 증거: `phonewheel-pedal-half.png`, `phonewheel-pedal-full.png`, `phonewheel-pedal-release.png`. 이는 자동 주입 터치 시험이며 사람이 잡고 하는 동시 페달 시험과 구분합니다.
- 옵션 메뉴, 조향 설정, 스크롤 후 페달 민감도 필드 접근을 실제 기기에서 확인했습니다. 값 변경 없이 취소했습니다.
- 실제 기기 ±180° 회전 정확도와 양손 페달, PC 입력 전달은 미검증입니다.

## 후속 — PC QR 창과 휴대폰 QR 스캔 연결

- Windows `PairingWindow`(별도 STA/UI 스레드)와 QRCoder 1.6.0 추가. 단일 EXE를 더블클릭하면 QR 및 간단한 연결 절차/입력 상태 표시. 창을 닫으면 리시버 취소 및 중립 해제.
- Android ZXing Embedded 4.3.0 스캐너 및 상단 `QR 연결` 버튼 추가. IP/포트/세션/키를 한 번에 전달. 외부 웹 서비스·QR 이미지 저장·키 영구 저장 없음. 기존 수동 입력 유지.
- 두 언어가 동일한 공개 고정 페어링 URI를 검사. Android는 다른 QR, 중복/추가 필드, 잘못된 주소/포트/세션/키, 초과 길이를 거부합니다.
- Android DNS/소켓 생성은 별도 스레드로 이동. 재연결 작업 세대 번호로 이전 작업/콜백 폐기. 일시적 UDP 송신 오류가 주기 송신을 영구 중단하지 않도록 처리. PC 상태 응답이 끊기면 운전 요청 해제 및 네트워크 안내 표시.
- `dotnet build ...PhoneWheel.Host.csproj -c Release`: 오류/경고 0. `dotnet run --project windows/tests/PhoneWheel.Tests/PhoneWheel.Tests.csproj -c Release`: 9/9 PASS.
- `gradlew.bat testDebugUnitTest assembleDebug lintDebug --offline`: BUILD SUCCESSFUL, 12 tests / 0 failures / 0 errors; lint 0 errors / 16 warnings. 기존 Activity 호환을 위한 IntentIntegrator 사용에는 deprecation 경고가 있습니다.
- `tools/publish-receiver.ps1`로 단일 EXE 재생성. `--backend vigem --bind 127.0.0.1 --port 26761 --qr --run-seconds 2`: exit 0, 가상 패드 초기화·중립 종료 확인. 별도 Windows 창을 실제 실행하고 QR·안내문·연결 대기 표시가 잘리지 않음을 시각 확인.
- Fold5 APK 업데이트 `Success`, 시작 `Status: ok`. 상단 QR 버튼 → 카메라 이번만 허용 → 실제 스캐너 화면 안내 확인. 뒤로 돌아와 앱 실행 유지 확인.
- 물리적으로 폰 카메라를 PC 화면에 비추는 QR 판독과 실제 Wi-Fi 인증/주행 입력 전달은 아직 NOT RUN입니다. 스캐너 화면 실행이나 고정 벡터 통과를 연결 성공으로 표현하지 않습니다. 방화벽 변경 없음.

## QR 스캔 후 연결 초기화 오류 수정

- 사용자가 `PC 연결 실패 · 주소/네트워크 권한 확인`을 보고했습니다. 기존 빌드의 `javap -c -p UdpControllerClient.class`에서 생성자가 `DatagramSocket.getPort()`를 읽어 `connect()`에 전달하는 것을 확인했습니다. Kotlin `apply` 수신 객체의 `port`가 외부 목적지 포트를 가려 미연결 값 -1을 사용했습니다. 이 오류는 네트워크 권한이나 PC 방화벽 검사 이전에 발생합니다.
- `also { udp -> ... }`로 소켓 객체를 명시해 생성자 목적지 포트를 사용하고, 초기화 실패 시 소켓을 닫도록 수정했습니다. UI는 권한 예외·주소 해석 예외·기타 초기화 오류 유형을 구분하며 비밀 키를 출력하지 않습니다.
- `UdpControllerClientTest.sendsAuthenticatedHelloToConfiguredPort`: 실제 loopback UDP 수신 소켓에 앱의 UdpControllerClient를 연결하고 전송된 Hello의 HMAC/세션을 검증합니다. 수정 후 테스트 PASS. 이는 JVM 실소켓 시험이며 폰→PC Wi-Fi 실기 검증을 대체하지 않습니다.
- 이 수정 작업 시 ADB 장치 목록이 비어 있어 휴대폰 업데이트 설치와 실기 재연결은 아직 수행하지 못했습니다. PC 실행파일 변경 및 방화벽 변경은 없습니다.

## 후속 — 중간 페달 반응 조정 및 수정 APK 설치

- 사용자 피드백에 따라 기본 명목 이동 거리 0.55H → 0.62H(약 12.7% 증가), 상단 유지 구간 0.15H → 0.10H. 브레이크·가속 모두 `PedalResponse`의 동일 설정을 사용합니다. 사용자 저장 민감도·레이아웃은 초기화하지 않았습니다.
- 1000px 높이/민감도 1/시작 y=900의 시험에서 310px 이동 50%, 기존 550px 이동 약 88.7%, 620px 이동 100% 확인. 중간 y=500에서 시작하면 이전 상단 y=150에서 87.5%, 새 상단 y=100에서 100%. 상단 유지/손 뗄 때 0%, 민감도 0.5와 2 동작 회귀 PASS.
- `gradlew.bat --offline testDebugUnitTest assembleDebug`: BUILD SUCCESSFUL, 14 tests / 0 failures / 0 errors. 실제 loopback UDP 인증 Hello 송신 회귀 포함.
- USB 디버깅 재연결 후 Fold5(개인 기기 식별자 생략)에 업데이트 설치 `Success`, MainActivity 시작 `Status: ok`/COLD 600ms. 이번 설치에는 앞서 수정한 목적지 포트 버그도 포함합니다.
- 사람의 페달 조작감 및 QR 재스캔 후 폰→PC 실제 통신 성공은 사용자 확인 대기입니다. 방화벽/PC 실행파일 변경 없음.

## 후속 — USB 케이블 자동 연결 및 실제 XInput 전달

- `UsbConnector`가 승인된 USB ADB 장치 한 대를 탐색하고, `adb reverse --no-rebind tcp:26761 tcp:26761`로 케이블 통로를 구성합니다. 동일 매핑은 재사용하고 다른 목적지 매핑은 덮어쓰지 않습니다. Wi-Fi ADB 주소/에뮬레이터는 자동 선택하지 않습니다. 여러 폰이 연결되면 선택을 요구합니다. 기기 디버깅 권한은 사용자가 허용해야 합니다.
- 기본 EXE는 승인된 USB 기기가 있으면 USB 우선, 없으면 Wi-Fi QR. `START_USB.cmd` / `START_WIFI.cmd`로 명시적 선택 가능. 이 PC의 SDK ADB를 사용하며 ADB는 단일 EXE에 포함되지 않습니다. Windows 방화벽 변경 및 USB 테더링 설정 변경 없음.
- USB 전용 loopback TCP + 2-byte big-endian 길이 + 기존 PWR1 인증 데이터그램. 프레임 48–68 bytes만 허용. 수신 프레임 분할/병합 처리, TCP_NODELAY, PC Status 송신 deadline 100ms. 기존 HMAC/ACK freshness/150ms watchdog/중립 대기 유지. 재연결 시 새 리시버 세션 및 명시적 중앙/운전 필요; 자동 운전 재개 없음.
- 휴대폰 명시적 MainActivity Intent로 임시 연결 정보를 전달하고 사용 후 extra 제거. 세션 키를 파일/환경설정에 저장하지 않습니다. USB URI는 127.0.0.1만 허용. USB 안내에는 Wi-Fi 오류 메시지 대신 케이블/리시버 재실행 안내 표시.
- 최종 Windows 게시 성공, C# 9/9 groups PASS. Android 17/17 tests PASS, lint 0 errors / 15 warnings. JVM USB 테스트는 인증 Hello 송신, 분할된 Status 수신, 병합된 프레임 읽기, 잘못된 길이 거부 및 USB loopback 주소 제한을 포함합니다.
- `tools/test_usb_transport.py` 최종 단일 EXE 검사 PASS: 잘못된 초기 프레임 길이 거부 → 분할 Hello → 인증 Status → 중립 대기/활성 → LT/RT 동시 255 → TCP 프레임을 일부만 보낸 채 정지. 마지막 완전 Control 송신에서 중립까지 **152.874ms** (최초 실행 152.728ms). `usb-tcp-watchdog.csv` 참고. 이는 PC loopback 시험이며 물리 케이블 지연 실측과 구분합니다.
- Wi-Fi UDP loopback 회귀: 기존 mock phone 인증, 운전 활성, 동시 LT/RT 255 (45개 CSV 표본), 최종 0/0 확인. `wifi-regression.csv`. 최초 시도는 리시버 bind 전에 mock을 시작해 실패했으며 포트 준비를 확인하고 재시험했습니다. PowerShell Process.ExitCode가 반환되지 않아 외부 wrapper 판정은 실패했으나 실제 CSV 출력 assertion 재실행은 PASS. 실제 LAN Wi-Fi/방화벽 경로는 아직 미검증입니다.
- Fold5 USB 지원 APK 설치 Success. PC 기본 EXE 실행 시 폰 앱 자동 실행, **USB 연결됨 · 중앙 보정 필요**, PC authenticated endpoint `127.0.0.1` 확인. `adb reverse --list`는 `UsbFfs tcp:26761 tcp:26761`로 실제 USB 전달 확인. 중앙 보정 → 운전 시 휴대폰에 USB 운전 활성 표시.
- 실제 Fold5 터치 계층에 ADB DOWN/MOVE를 입력한 상태에서 Windows 시스템 XInput을 직접 읽음: 가속 유지 **LT=0, RT=255**, 브레이크 유지 **LT=255, RT=0**, 해제 **LT=0, RT=0**. 각 상태 1초 기록. `usb-throttle-xinput.csv`, `usb-brake-xinput.csv`, `usb-release-xinput.csv`. 초기 연속 캡처 `usb-xinput.csv`에는 페달 변화가 잡히지 않아 위 상태별 직접 측정으로 재확인했습니다. 손으로 잡고 하는 양손 시험이나 게임 주행 시험은 아닙니다.
- PC USB 창을 computer-use로 직접 확인하고 긴 안내 문구의 줄바꿈을 정리했습니다. 시험 후 입력을 해제하고, 최종 빌드 리시버를 다시 열어 USB 자동 연결 상태로 제공합니다.

## 2026-09-13 — 0.2.0 위치 페달, 조향 응답, 수신 표시, 진동

- 작업 시작 때 폰에는 2026-09-12 22:26 설치의 이전 APK가 남아 있었고, 22:51 빌드는 설치되지 않았습니다. 최종 0.2.0(versionCode 2) APK를 실제 Fold5에 업데이트 설치했습니다. 사용자 배치는 보존합니다.
- 페달 계산을 시작점 상대 거리/속도 배율에서 `1 - y/H` 절대 위치 방식으로 변경했습니다. 터치 위치와 바 경계가 일치하고 95% 이상에서만 100%로 스냅합니다. 처음 중간을 누르면 바로 50%, 손을 떼면 0%입니다. 예전 저장 민감도를 사용하지 않습니다. 네트워크 응답 없음 처리에서 매초 touch pointer를 취소하던 동작도 제거했습니다. 안전 출력 해제는 유지합니다.
- 조향 기본 응답 곡선 1.0→0.7, 한쪽 범위 180°/평활 8ms/데드존 0.5° 유지. 30°≈28%, 90°≈61%, 180°=100%입니다. 저장한 설정은 보존합니다. 임의 중앙 자세에서도 화면 법선 기준 회전 추적, ±180 경계/중립 복귀는 합성 quaternion 시험 PASS. 실제 손 회전 정확도와 F1 게임 반응은 미검증이며 사용자에게 45° 회전 시 앱 표시를 요청했습니다.
- PC UI가 인증된 최근 **폰 수신**과 안전 게이트의 **게임 출력**을 따로 표시합니다. 운전 해제 상태의 폰 수신 30%/출력 0%는 정상입니다. 잘못된 MAC은 수신 표시를 갱신하지 않고 150ms 만료 시 데이터 없음으로 전환합니다.
- C# 11/11 groups PASS. Android `gradlew.bat --offline testDebugUnitTest assembleDebug lintDebug` BUILD SUCCESSFUL, 19 tests/0 failures/0 errors, lint 0 errors/18 warnings. ZXing legacy API deprecation 경고가 남습니다. `tools/publish-receiver.ps1`로 최종 단일 EXE 게시 성공.
- `tools/test_usb_transport.py` 최종 EXE 회귀 PASS: 잘못된 길이/분할 Hello/인증 Status/동시 LT·RT 255/불완전 프레임 중 watchdog. 마지막 완전 Control→중립 152.720ms. PC loopback 시험이며 케이블 지연 실측은 아닙니다.
- 실제 USB: EXE 실행→폰 앱 자동 실행→USB 연결됨 확인. ADB 주입으로 화면 H=904에서 가속 DOWN(y=633)→PC 폰 수신/게임 출력 모두 30%, XInput RT=76. 동일 pointer MOVE(y=452)→RT=128, MOVE(y=45)→RT=255, UP→RT=0. 장시간 held pointer가 중간에 사라지지 않았습니다. `pedal-30-v020.png`는 퍼센트 반올림 정리 전 29% 표시였으며 최종 빌드에서 PC처럼 반올림하도록 수정했습니다. 최종 APK에서 브레이크 y=452→LT=128, RT=0 및 해제 확인. 사람이 양손으로 조작한 시험과 구분합니다.
- 진동: ViGEm FeedbackReceived의 motor 값→최신 snapshot→인증 Haptic event 8→Android vibrator 경로 추가. 사용자 선택형 게임 기본 진동이며 기본 OFF; 엔진/변속 진동도 포함될 수 있어 중요 이벤트 전용 detector와 구분합니다. 원래 telemetry detector는 DISABLED. PC source 500ms 만료, 운전 해제 시 폐기, 폰 lease 100ms, UI 대기 시간 차감, 낮은 우선순위/no queue 유지.
- `tools/test_xinput_rumble.py --slot 0`: 실행 전 모든 XInput 슬롯 부재, PhoneWheel 실행 후 슬롯 0 생성 확인 후 한정 시험. 0.3초 motor 요청 후 반드시 stop. 최종 APK의 게임 기본 진동 ON에서 `dumpsys vibrator_manager`에 dev.phonewheel MEDIA effect 시작 10:48:50.754/종료 10:48:51.031, 기록 duration 279ms, cancelled_by_user 확인. 자체 250ms 시험도 실행됐으나 뒤이은 중앙 보정이 104ms에 취소했으므로 250ms 완주로 기록하지 않습니다. 진동 체감은 사용자 확인이 필요합니다.
- Wi-Fi: 앞선 새 QR 세션에서 실제 LAN 인증 Control 수신 및 PC 'Wi-Fi · 출력 차단 / 폰 수신 0' 표시를 확인했습니다. PC 172.30.1.94/폰 172.30.1.39, 읽기 전용 검사에서 현재 EXE의 Public TCP/UDP Allow 규칙 확인. 이 작업은 방화벽을 변경하지 않았습니다. 실제 Wi-Fi 게임 주행은 미검증입니다.
- **최종 남은 USB 문제:** 10:49 무렵 테스트 후 ADB 장치가 사라졌습니다. 두 번의 `adb devices -l`은 빈 목록이고 Windows에는 Samsung USB Composite/Modem/WPD Fold5만 있으며 ADB Interface가 없습니다. PC 창은 수신 끊김, XInput은 0/0/0으로 안전 해제됐습니다. 디버깅이 꺼졌는지 등 원인은 확정하지 않았으며 사용자에게 USB 디버깅/PC 허용 상태 확인을 요청했습니다. USB 연결 유지가 해결됐다고 주장하지 않습니다. 진동 옵션 OFF 복원을 시도하는 도중 끊겨 최종 저장값은 재확인이 필요합니다. PC 리시버는 입력 해제 상태로 열려 있습니다.
