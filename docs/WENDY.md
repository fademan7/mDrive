# Wendy F1 Engineer — mDrive 0.5.3

0.5.3 prioritizes dedicated controller output/ACK/watchdog workers and safe output-error recovery. Wendy adds current-session game weather forecasts and same-lap leader gaps. [Build/deployment status and tests](../artifacts/validation/WENDY_053.md). Both app and receiver expose creator website/email via About. No unsupported game mutations were enabled.

## 0.5.2

See the [current English conversation/mode guide](WENDY_CONVERSATIONS.md) and [validation report](../artifacts/validation/WENDY_052.md). AUTO uses the system provider by default; selecting it hides PTT. Speech errors retry up to a 30-second backoff unless a language/permission problem requires user action. A separate supervisor prevents a lost IDLE restart. Optional speech failures do not stop controller workers. Wi-Fi radio loss still triggers the existing safe release.

Race greetings have 20 variants, session deduplication and safety-alert priority. New read-only questions include radio/help, speed, gear, RPM, DRS, compound, track/air temperature, session timer, pit limiter/count, wing setup, lap validity/time, sectors and ERS mode; named damage components are supported. No game menu automation has been added.

## 0.5.1 priority fixes

- The full app/receiver UI is English; the Android app locale also covers the QR scanner.
- Options and Wendy settings are in-window panels. They do not clear held pedal pointers, disarm/recenter the controller, or restart its connection. Real Activity background/focus loss, steering changes and layout editing retain the safety release. `Wendy OFF` deliberately stops only Wendy.
- Hands-free has **system** (same service as PTT, may use internet) and **on-device** modes. It no longer forces on-device recognition. Selecting hands-free enables Wendy, permission completion retries, foreground return clears a suspended retry state. `Retry voice service` recovers without touching the controller. Read the online-provider disclosure before selecting system mode. Default remains PTT; speech is English only. SpeechRecognizer sessions have gaps and can be stopped by the provider; this is not guaranteed uninterrupted streaming.
- Controller I/O threads use Android MORE_FAVORABLE priority; Wendy network stays background priority. Foreground Wi-Fi holds a non-reference-counted LOW_LATENCY Wi-Fi lock, released on app pause/destroy or USB mode. The OS may not honor latency requests and radio failures remain possible. The 100ms freshness/150ms safety timeout is unchanged.
- Sender callback/encoding exceptions are counted and cannot cancel all future scheduled sends. Wendy panel displays maximum sender gap and failures; metadata-only voice state/provider/error codes go to `WendyVoice` logcat. No transcript/audio is written to logs.
- Temperature/pressure/age/laps-remaining have separate structured intents with deterministic common-phrase routing. Specific wheel queries preserve FL/FR/RL/RR identity. Tyre temperature gives inner/surface values for a wheel and inner values for all four; brake/engine are Celsius, current tyre pressure is PSI. Stale data is unavailable, never replaced with old values or guessed setup targets. Mixed components such as engine+tyre temperature are rejected as ambiguous.
- In-game setting changes and pit request execution still require a verified write/binding adapter and are not implemented.

The following 0.5.0 section documents the earlier on-device-only behavior; 0.5.1 mode selection above supersedes it. [0.5.1 test report](../artifacts/validation/WENDY_051.md)

## 0.5.0 사용법과 제한

- **Always Listening**: 폰 Wendy 옵션에서 켜고 끕니다. 기본 OFF, 앱 재실행 때 재승인이 필요한 세션 옵션입니다. Wendy도 ON이어야 합니다. Android 온디바이스 영어 인식만 사용하며, 설치된 영어 모델이 없거나 제공 서비스가 지원하지 않으면 오류를 표시하고 PTT로 전환하도록 안내합니다. 자동으로 클라우드 연속 인식에 fallback하지 않습니다.
- 마이크 권한은 사용자가 승인합니다. 앱 전경·PC 연결 상태에서만 인식 세션을 반복합니다. 무음 종료 후 1.2초 간격, 오류는 3/6초 backoff 후 3번째 일시 중단. 권한·언어 오류는 즉시 중단. Wendy TTS 중에는 인식을 취소/파괴하고 끝난 뒤 1.2초 후 재개합니다. 상단 AUTO/LISTENING 표시. OS SpeechRecognizer는 진정한 무중단 스트리밍이 아니므로 세션 사이 짧은 공백이 있습니다.
- 헤드셋 권장. 주변 사람이나 F1 게임 음성을 질문으로 오인할 수 있고 배터리 사용이 늘 수 있습니다. 녹음 파일은 저장/전송하지 않습니다. PTT는 기존 시스템 공급자에 따라 클라우드를 사용할 수 있습니다. [Android SpeechRecognizer 문서](https://developer.android.com/reference/android/speech/SpeechRecognizer)
- `Do I need pit in?`, `Should I pit?`, `When should I pit?`: 게임의 현재 피트 전략 윈도·예상 복귀 순위, 큰 손상/70% 마모/빗속 드라이 타이어를 근거로 답변합니다. 최적 전략 계산기나 미래 날씨 예측기는 아닙니다. 근거가 없으면 모른다고 답합니다.
- `Box box`, `Box this lap`: **이번 랩 피트 리마인더만 저장**. 실제 게임 요청을 보내지 않는다고 명시합니다. 70% 지점 이후 1회 리마인더. 인식 신뢰도 >= .75 및 정확한 허용 문장 필요. `Stay out`, `Cancel that`, `Don't box`는 리마인더 취소. 다음 랩/세션에서는 오래된 계획을 재사용하지 않습니다. `Pit status`는 현재 피트 상태와 미확정 리마인더를 구분합니다.
- `How was my last lap?`, `What's my lap time?`: 마지막 정상 랩 시간·이전 정상 랩 대비 차이·클린 세션 베스트 갱신을 보고합니다. 랩 종료 시 자동으로 짧게 읽습니다. 이벤트 우선/쿨다운 때문에 음성 보고가 생략될 수 있고 15초가 지나면 쌓아두지 않습니다.
- `How can I improve?`, `Coach me`: 기준 랩 상태 또는 최근 구간 비교. 첫 정상 완료 랩을 학습한 직후 다음 랩부터 사용하며 더 빠른 정상 랩으로 갱신합니다. 중간에 연결했다면 완전한 한 랩을 추가로 돌아야 합니다.
- 기준은 현재 세션 메모리에만 저장합니다. 트랙/길이/날씨/타이어 컴파운드/셋업 변경·새 타이어(나이 감소)·세션/flashback 때 다시 학습합니다. 피트·무효·황기·SC/VSC/formation·손상·중단/긴 수신 공백 랩은 제외합니다. 온도/연료/ERS에 따른 물리 보정은 없으므로 최적 랩·최적 진입 속도로 표현하지 않습니다.
- 최대 10Hz, 20m 구간 최대 1501개 × 현재/기준 2랩. 브레이킹 시작부터 직선 복귀까지 기준 구간을 만들고 같은 위치에서 비교합니다. 속도 차이 >= 10km/h와 구간 손실 >= .3s가 함께 있을 때만 비교 코멘트. 이름/번호가 검증된 코너 지도가 아니므로 'last braking zone'이라고 부릅니다. 앞차 2초 이내·브레이크·큰 조향 시 코멘트 생성을 억제하며 랩당 2개/30초 간격/5초 만료. 빠른 entry가 원인이라고 단정하지 않습니다.
- 뒤차 알림: **순위상 바로 뒤이며 같은 랩인 차량**의 gap이 1.5초 이내로 3초간 유지되면 시간 차를 보고합니다. 한 조건 유지 중 반복하지 않고 45초 쿨다운. 위치센서·사각지대·옆차 레이더가 아니며 선수 이름은 아직 없습니다.
- 컨트롤러 입력·센서·페달·저장 배치는 변경하지 않았습니다. 게임 설정 직접 변경, pit request 키 입력, 타이어/윙 자동 변경은 여전히 미지원입니다.

[0.5.0 빌드·설치·테스트 결과](../artifacts/validation/WENDY_050.md)

## 0.4.0 사용법과 변경

이전 로컬 LLM 금지 조건은 2026-09-17 사용자 요청으로 CPU 전용 Tiny LLM 허용으로 변경됐습니다. [0.4.0 결과·전체 지원 범위](../artifacts/validation/WENDY_040.md)를 우선 참조하세요. 아래 0.3.x 설명에서 로컬 LLM이 없다는 부분은 이전 버전 기록입니다.

- Receiver EXE 옆 `wendy` 폴더에 Qwen3 0.6B Q8_0와 llama.cpp b10964 Windows CPU 런타임을 동봉합니다. 따로 설치할 AI 프로그램/Python은 없습니다.
- PC **Wendy details** / 폰 **Wendy 옵션**에서 Heard, Intent, Response, Source를 확인합니다. 원음은 PC에 보내지 않고 전사문도 디스크에 자동 기록하지 않습니다.
- 모델은 enum JSON으로 의도/거동 피드백만 분류합니다. 수치·답변·제안·실행은 프로그램이 담당합니다. 모델 실패는 Rules fallback으로 표시하며 미인식은 타이어로 대체하지 않습니다.
- 첫 음성 질문에 모델을 로드합니다. 추론 2스레드, BelowNormal, GPU layers 0/device none, CPU DLL만 포함합니다. 모델은 127.0.0.1 임시 포트+임의 토큰으로만 통신합니다. 외부 OpenAI API가 아닙니다.
- 분류는 최대 15초, 폰 응답 대기는 20초입니다. 그동안 Wendy 폴링/플래그와 컨트롤러 통신은 계속됩니다. Engineer OFF 또는 PC 종료 시 모델을 정리합니다.
- penalties/warnings, pit status, differential 조회와 Blue flag·경고/페널티 증가·지속 고온·간격/페이스 추세 알림을 추가했습니다.
- 충분한 다중 랩 고온/불균등 마모와 신선한 셋업·무손상이 확인될 때 다음 세션 검토를 제안합니다. 명확한 영어 승인(인식 신뢰도 0.75 이상), 동일 세션, 30초 이내의 제안이 있어야 저장합니다. 거절/다른 질문/세션 변경 후에는 이전 승인을 재사용하지 않습니다.
- 승인한 권고는 `%LOCALAPPDATA%\mDrive\wendy-recommendations.json`에 최대 100개 저장하며 PC 상세 창에서 세션 종료 후에도 읽습니다. 수치 셋업 자동 변경이나 즉시 게임 명령 실행은 아직 없습니다.
- Black/White를 트랙 제한 경고 수로 추정하지 않습니다. Understeer/oversteer/wheelspin/locking 등의 피드백 분류는 있지만 물리 원인·셋업 처방은 검증된 근거가 부족해 비활성입니다.

## 0.3.x 기록

## 이번 버전

0.3.1의 상단 배치: **PTT → PTT 상태 → 정중앙 정사각형 플래그 → 중앙 보정·옵션**. 녹색/황색/적색/청색 플래그는 단색 정사각형, SC/VSC는 황색 바탕과 문자, Checkered는 흑백 체크무늬입니다. 미수신은 회색 `—`이며 녹색으로 추정하지 않습니다. PTT 옆 상태를 누르면 Wendy 설정과 최근 응답을 볼 수 있습니다. 이 HUD 변경은 조향·페달·Wendy 처리·PC Receiver를 바꾸지 않습니다.

영어 Push-To-Talk, 규칙 기반 답변, 영어 TTS, F1 2025 UDP 상태/이벤트, PC ON/OFF와 휴대폰 Wendy/Flag UI를 추가했습니다. 하단 스틱은 이제 실제 Xbox Right Stick입니다. 기존 휠/페달 계산·자동 중앙·진동 설정을 유지합니다.

**게임 설정을 자동 변경하는 명령은 실행하지 않습니다.** 인식과 거부 응답만 구현했습니다. UDP는 관측 채널이며 현재 프로젝트에는 검증된 게임 설정 쓰기 API나 사용자별 직접 키 바인딩이 없습니다. MFD 화면/선택 항목을 추정하는 키 연타는 운전 입력을 망가뜨릴 수 있어 제외했습니다. 변경했다고 거짓 확인하지 않습니다.

Ollama, 로컬 LLM, OpenAI API, PC GPU 추론, 화면 캡처, OCR, Vision 의존성을 추가하지 않았습니다. PC는 음성 파일을 받지 않습니다. 휴대폰의 Android 시스템 음성 인식 공급자가 영어 음성을 처리합니다. 공급자에 따라 인터넷/클라우드 처리가 필요할 수 있으며 앱의 Wendy 활성화 안내에 표시합니다. 영어 TTS는 설치된 비네트워크 음성을 선택합니다. 영어 음성이 없으면 ERROR와 설치 안내를 표시합니다. 특정 여성 목소리나 이름의 TTS 음성을 보장하지 않습니다.

## 실행 및 연결

1. 최신 PC `release/receiver/PhoneWheel.Receiver.exe`와 Android `android/app/build/outputs/apk/debug/app-debug.apk`를 함께 사용합니다. 기존 APK 위에 업데이트하고 앱 데이터를 삭제하지 마세요.
2. Receiver를 평소처럼 실행해 USB 또는 Wi-Fi 컨트롤러를 연결합니다.
3. Receiver 창의 **F1 Engineer: ON / OFF**를 켭니다. 기본값은 OFF이며 실행마다 명시적으로 켭니다.
4. F1 25 / 2026 Season Pack의 Telemetry Settings에서 UDP Telemetry를 ON, Broadcast를 OFF, IP를 `127.0.0.1`, 포트를 `20777`, **UDP Format을 2025**로 설정합니다. 권장 시작 송신률은 30 또는 60Hz입니다. 현재 PC에서 게임과 Receiver를 함께 실행하는 구성입니다. 다른 앱이 20777을 점유하면 해당 앱의 포트를 바꾸거나 게임/Receiver 포트를 맞춰야 합니다.
5. 휴대폰 상단 Wendy 영역 또는 **옵션 → Wendy F1 Engineer → Wendy 켜기**를 누릅니다. 컨트롤러 연결 정보를 그대로 사용하며 별도 QR/키는 없습니다.
6. 처음에는 PTT를 눌러 마이크 권한을 허용한 다음, 다시 **PTT를 누른 채 영어로 말하고 놓습니다**. 권한 팝업·옵션 화면에서는 기존 안전 정책대로 운전 입력이 해제되므로 정차 후 최초 설정하세요.
7. 응답은 폰에서 재생합니다. Wendy 영역을 누르면 최근 인식/응답/오류 문구도 확인할 수 있습니다. 끄려면 동일 화면에서 Wendy 끄기 또는 PC Engineer OFF를 사용합니다.

USB: 기존 PWR1 TCP 26761과 별도로 Wendy TCP 26762를 사용합니다. Receiver의 기존 USB 유지 작업이 Engineer ON일 때만 승인된 같은 장치에 추가 ADB reverse를 설정합니다. Wi-Fi/테더링이 필요하지 않습니다. 해당 매핑 실패는 컨트롤러를 중단시키지 않습니다.

Wi-Fi: 기존 UDP 26760은 그대로이며 Wendy는 같은 PC IP의 TCP 26762입니다. 필요하면 Windows 방화벽에서 이 프로그램의 **개인 네트워크 TCP 26762** 수신을 허용하세요. 이번 구현은 방화벽을 자동 변경하거나 끄지 않습니다. F1 UDP는 loopback 20777이므로 외부 네트워크에 노출하지 않습니다.

진단 CLI: `--engineer --engineer-port 26762 --telemetry-port 20777`. 휴대폰 Wendy 포트는 이번 UI에서 26762로 고정됩니다. 비기본 engineer-port는 합성 테스트용입니다.

## 영어 질문

| 질문 예 | 응답 범위 |
|---|---|
| How are my tyres? | 가장 많이 마모된 타이어와 % |
| What's my front left tyre wear? | 요청한 바퀴의 마모 % |
| What's the gap ahead? | 한 순위 앞의 활성 차량과 같은 랩일 때 간격 |
| What's the gap behind? | 한 순위 뒤 차량의 앞차 간격, 같은 랩일 때만 |
| How much fuel do I have? | 연료 질량과 게임 MFD 연료 랩 표시값 |
| What's my ERS? | 저장 에너지 MJ; 임의 배터리 용량으로 %를 만들지 않음 |
| Any damage? | 앞날개 또는 모니터링한 타이어/브레이크/차체/기어박스/엔진 손상 |
| What's the weather? | 현재 날씨; 전략/예보 추론 없음 |
| What flag is out? | 현재 확인 가능한 플래그 |
| What lap am I on? | 현재 랩 |
| What's my position? | 현재 순위 |
| What's my brake bias? | 현재 앞 브레이크 바이어스 % 조회 |

연료 MFD 랩 값은 현재 HUD 의미 그대로 안내하며, 이를 별도의 ‘앞으로 주행 가능한 총 랩’으로 바꾸지 않습니다. 선두·최하위·랩 차이·유효한 인접 차량이 없으면 간격 unavailable입니다. 모르는 값을 0으로 채우지 않습니다.

## 영어 명령 — 실행은 보류

다음 요청을 감지하지만 **어떤 게임 키/설정 변경도 실행하지 않습니다**:

- Set brake bias to 54.
- Set differential to 55.
- Box this lap.
- Soft tyres next stop.
- Increase front wing by one. / Decrease front wing by one.

높은 신뢰도라도 `Game setting changes are not enabled. Please use the game controls.`로 답합니다. 신뢰도 0.75 미만/미제공, 숫자 대안 불일치, `54 or 55` 같은 애매한 설정값은 거부합니다. 성공 확인/변경 후 텔레메트리 대조는 실제 쓰기 어댑터를 추가할 때 구현해야 합니다. 추후 `WendyEngineer.Answer`의 명령 분기를 검증 가능한 어댑터로 확장할 수 있지만 현재는 의도적으로 출력 모듈 참조가 없습니다.

## 먼저 알려주는 내용

- Yellow/Red/SC/VSC/Checkered: 상태 변화. Flag UI는 즉시 업데이트하고 음성은 cooldown 적용.
- 타이어 마모: 40/55/70% 단계. 단계별 중복 방지, 5% 히스테리시스, 120초 재알림 제한.
- 손상: 앞날개/기타 모니터링 손상 10% 이상. 수리 후 재진입 전까지 동일 경고 반복 안 함.
- 낮은 연료: 3kg 미만. 4kg 초과 시 재진입 가능. 완주 전략 계산 아님.
- 현재 날씨 변화: 60초 cooldown. 이전 상태보다 바뀐 실제 분류를 말함.
- 피트 레인 진입 / 팀메이트 피트 진입: 60초 cooldown.

전역 음성 간격은 8초입니다. Listening/Processing/Speaking 중에는 새 자동 안내를 생성하지 않으며 음성 backlog를 쌓지 않습니다. 너무 짧은 이벤트가 cooldown 중 사라지면 음성으로 재생하지 않습니다. 중요한 상황이라도 게임 HUD/플래그가 기준이며 Wendy 음성에만 의존하지 마세요.

## 상태·안전·제한

- PC: OFF/ON, Telemetry Connected/Waiting, Wendy Idle/Listening/Processing/Speaking/Error, Flag.
- Android: IDLE/LISTENING/PROCESSING/SPEAKING/ERROR 및 GREEN/YELLOW/RED/BLUE/SC/VSC/CHECKERED. 확인 불가 시 Flag `—`(UNKNOWN), 거짓 GREEN 금지.
- PTT만 지원. Always Listening, Wake Word, 음성 기록 저장 없음. 최대 인식 대기 10초, 응답 처리 6초, TTS 20초 제한.
- 텔레메트리/개별 차량 값은 2초, Session 문맥은 3초 만료. 일시정지/관전 상태에서는 레이스 답변과 자동 안내 중지.
- 형식/길이/버전/플레이어 인덱스/NaN/범위/순번을 검사. 세션 변경과 flashback은 오래된 관측을 폐기. 공식 규격 2025 v1의 Session/Lap/Event/CarStatus/CarDamage만 해석.
- 2026 전용 UDP 형식, 분할 화면의 보조 플레이어, 랩 차이가 있는 간격, AI 전략, 정확한 연료 총주행 랩 추산, 전체 부품 수명/블리스터 질의, 예보 전략은 미지원.
- Red/Checkered는 이벤트 기반입니다. 이벤트 손실 시 완전한 상태 복원을 보장하지 않습니다. Red는 재출발 Lights Out/새 세션/flashback 등에 해제합니다.
- Wendy 장애는 컨트롤러의 인증 상태/ARM/센서 설정을 수정하지 않습니다. 폰의 앱 포커스 상실 때에는 원래 안전 정책을 그대로 따릅니다.
- Wendy OFF일 때 PC는 텔레메트리 소켓, Wendy 리스너, 분석 worker를 만들지 않습니다. 폰도 Wendy OFF일 때 음성 엔진과 별도 네트워크 worker를 종료합니다. ‘CPU 사용량이 수학적으로 동일’하다는 보장은 하지 않습니다.

## 통신

컨트롤러는 기존 PWR1 유지. Right Stick이 0일 때 기존 68바이트 Control, 움직일 때 새 kind 4 / 76바이트 ControlLook. 뒤에 RX, RY float32 두 값 추가. 새 Receiver는 둘 다 처리합니다. 구 Receiver에서 Right Stick을 움직이면 패킷 거부/입력 해제가 발생할 수 있으므로 **PC/폰 동시 업데이트가 필요합니다**. 앱 좌표의 아래 방향을 뒤집어 XInput 위가 +RY가 되도록 했습니다. 게임의 Look Around 매핑에 따라 좌/우/뒤 보기 효과를 확인하세요.

Wendy는 별도 TCP WDY1, 초당 최대 5회. PC가 연결마다 32바이트 nonce를 보내며 이후 각 방향별 증가하는 8바이트 순번 + UTF-8 JSON + 32바이트 HMAC-SHA256. MAC 입력은 `WDY1 || nonce || direction || sequence || JSON`. 방향 0=Android→PC, 1=PC→Android. 길이는 4바이트 BE prefix, JSON 최대 2048바이트. PC 세션의 기존 랜덤 32바이트 키를 사용하되 컨트롤러 순번/ACK/lock은 공유하지 않습니다. 재연결 시 미완료 명령은 폐기하고 새 nonce를 사용합니다. 음성과 개인 키를 로그에 기록하지 않습니다. HMAC은 인증이지 암호화가 아니므로 같은 LAN에서 평문 텍스트를 감청할 가능성까지 제거하지는 않습니다.

## 근거

- [EA F1 25 공식 UDP v3](https://forums.ea.com/t5/s/tghpe58374/attachments/tghpe58374/f1-games-game-info-hub-en/61/4/Data%20Output%20from%20F1%2025%20v3.pdf): PDF 스킬로 관련 구조체를 확인하고 packed offset/단위/길이를 대조했습니다.
- [Android SpeechRecognizer](https://developer.android.com/reference/android/speech/SpeechRecognizer): 시스템 인식 서비스와 lifecycle.
- [Android TextToSpeech](https://developer.android.com/reference/android/speech/tts/TextToSpeech): 영어 음성 선택 및 발화 완료/오류 callback.

실제 빌드·검사·성능 범위는 [0.3.0 검증 기록](../artifacts/validation/WENDY_030.md)을 참조하세요.
