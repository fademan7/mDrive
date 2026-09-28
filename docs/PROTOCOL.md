# PWR1 통신 계약 v1

## 0.5.0 hands-free 알림 허용

PWR1 변경 없음. WDY1 요청에 선택 boolean `allowAlerts`(기본 false)를 추가합니다. Android가 AUTO/LISTENING이지만 발화 시작 전일 때만 true이며, 발화 시작 즉시 false가 됩니다. 서버는 IDLE 또는 LISTENING+allowAlerts 상태에서만 proactive 응답을 보냅니다. 폰은 아직 발화 전인지 다시 확인한 뒤 마이크를 취소하고 TTS를 재생합니다. PTT LISTENING 중에는 알림으로 끊지 않습니다. 경쟁 조건에서 늦게 도착한 경고는 폐기하며 입력/발화를 덮어쓰지 않습니다. 새로운 `kind=plan`은 Wendy 내부 피트 리마인더일 뿐 게임 명령 성공을 의미하지 않습니다.

## 0.4.0 Wendy 비동기 확장

PWR1/컨트롤러 변경 없음. WDY1의 응답 JSON에 선택 필드 `heard`(최대 240), `intent`/`source`(각 64)와 `kind=pending`을 추가합니다. 최초 요청을 즉시 접수한 뒤 200ms 상태 폴링의 후속 응답으로 답을 전달합니다. 원래 요청 상태가 PROCESSING이고 같은 레이스 generation일 때만 결과를 사용합니다. 연결 단절 시 분류 요청을 취소하고 재전송하지 않습니다. 모델 분류는 최대 15초이며 휴대폰 대기 시간은 20초로 확대됐습니다. 새 PC와 Android 0.4.0을 함께 사용하세요.

## 0.3.0 호환 확장

기존 kind 1 Control 68바이트는 유지합니다. Right Stick을 사용하는 새 kind 4 ControlLook은 payload 28바이트/총 76바이트이며, 기존 20바이트 뒤에 offset 20 LookX, offset 24 LookY를 float32 LE(-1..1)로 추가합니다. 위쪽이 +RY입니다. 오른쪽 스틱도 NaN/범위 검증, 중립 대기, watchdog/해제에 포함합니다. Android는 LookX/Y가 0이면 기존 kind 1을 전송합니다. USB framing 허용 최대 길이는 76이며 실제 타입별 길이는 codec으로 엄격히 검사합니다. 구 Receiver는 확장 입력을 거부하므로 PC/폰 동시 업데이트가 필요합니다.

Wendy는 PWR1에 문자열·음성을 끼워 넣지 않습니다. 별도 TCP 26762 WDY1 인증 채널과 PC loopback UDP 20777 F1 수신을 사용하며 OFF에는 생성하지 않습니다. [WDY1 세부 계약](WENDY.md#통신)을 참고하세요. 이하 기존 v1 설명의 최대 길이 68은 legacy kind만의 값입니다.

프로젝트 자체 설계입니다. EA의 UDP 포맷과는 별개입니다. 이하 숫자는 외부 제품의 성능 주장이 아닌 구현 설정입니다.

## 전송과 세션

PC는 사용자가 선택한 로컬 어댑터의 UDP 26760에서 수신합니다. 폰은 단일 UDP 소켓의 실제 source port로 응답을 받습니다. 포트는 설정 가능하게 합니다. 폰 입력·PC 상태/진동과 게임 텔레메트리 소켓을 분리합니다.

PC가 새 페어링을 시작할 때 CSPRNG로 32바이트 키, 0이 아닌 uint64 session ID를 생성합니다. 표시할 페어링 데이터는 `protocol`, `pcIp`, `pcPort`, `sessionIdHex`, `keyBase64`입니다. 이 정보는 사용자가 직접 폰에 전달합니다. 키에는 짧은 PIN을 그대로 쓰지 않습니다. 초기 수동 입력과 후속 QR은 같은 데이터를 사용합니다. 실제 키는 소스·기본 프로필·로그에 넣지 않습니다.

폰 Hello를 인증한 뒤 PC가 source endpoint를 해당 세션에 고정합니다. 활성화 상태에서 다른 IP/port의 Hello로 endpoint를 변경하지 않습니다. 앱 프로세스 재시작으로 순번이 초기화되거나 endpoint가 달라졌다면 입력 해제 후 새 페어링을 진행합니다. 단순 일시적 손실 후 같은 세션·endpoint로 돌아오면 순번을 계속 증가시키되 운전 시작 절차를 다시 거칩니다.

모든 패킷은 little endian, padding 없음입니다. 정수는 unsigned를 명시한 크기로 읽습니다. float는 IEEE754 binary32입니다. 문자열 직렬화, 플랫폼 기본 packing, JSON float 변환을 주행 루프에 사용하지 않습니다. 최대 데이터그램은 v1에서 68바이트이며 다른 크기는 해당 타입 정의와 정확히 일치해야 합니다.

## 공통 헤더: 32바이트

| Offset | 크기 | 이름 | 의미 |
|---:|---:|---|---|
| 0 | 4 | magic | ASCII `PWR1` |
| 4 | 1 | version | 1 |
| 5 | 1 | kind | 0 Hello, 1 Control, 2 Haptic, 3 Status |
| 6 | 2 | payloadLength | 헤더와 MAC을 제외한 바이트 수 |
| 8 | 8 | sessionId | 현재 페어링 세션 |
| 16 | 4 | sequence | 방향별 송신 순번 |
| 20 | 8 | sentMonotonicUs | 송신 기기의 단조 시계. 서로 다른 기기끼리 직접 비교 금지 |
| 28 | 4 | ackSequence | 최근 받은 상대 방향의 순번. 최초 Hello에서는 0 |

헤더 뒤 payload, 그 뒤 **HMAC-SHA256(header || payload)의 앞 16바이트**를 붙입니다. 검증에는 constant-time 비교를 사용합니다. 길이·magic 검사로 비정상 입력을 빨리 제한할 수 있으나, 세션이나 출력 상태를 바꾸기 전에 MAC 검증을 완료해야 합니다. 방향에 허용된 kind만 처리합니다. 키는 32바이트로 고정합니다.

폰의 Hello/Control은 하나의 송신 순번을 공유합니다. PC의 Status/Haptic도 하나의 송신 순번을 공유합니다. 새 세션은 1부터 시작하고 uint32 순환을 허용합니다. 후보가 최신인지 판단하는 식은 `0 < ((new-old) mod 2^32) < 2^31`입니다. 중복·역순·정확히 반 바퀴 떨어진 값은 거부합니다. 세션 변경을 수신 데이터만으로 자동 승인하지 않습니다.

## Payload

### Hello — kind 0, payload 0, 총 48바이트

초기 연결에만 사용합니다. 최대 초당 3회 재시도하고 PC의 인증된 Status를 받은 뒤 Control을 시작합니다. Hello만으로 입력을 활성화하거나 입력 watchdog을 연장하지 않습니다.

### Control — kind 1, payload 20, 총 68바이트

| Payload offset | 형식 | 이름 | 범위 |
|---:|---|---|---|
| 0 | float32 | steer | -1…1 |
| 4 | float32 | throttle | 0…1 |
| 8 | float32 | brake | 0…1 |
| 12 | uint16 | buttons | XInput 유효 비트만; 초기 주행에서는 0 |
| 14 | uint16 | flags | 아래 5비트만 사용 (0.5.6) |
| 16 | uint32 | calibrationEpoch | 보정·화면 좌표 설정이 바뀔 때 증가 |

flags: bit0 운전 시작 의사(ARM), bit1 센서 유효, bit2 Activity가 foreground, bit3 터치 입력 계층 준비, bit4 FAST_RECOVERY 지원(0.5.6). bit3는 손가락을 대고 있다는 뜻이 아닙니다. 손을 모두 떼어 페달이 0이어도 터치 계층이 정상일 수 있습니다. 나머지 비트는 0이어야 합니다. `buttons & ~0xF3FF`가 0이 아니면 거부합니다. bit4는 READY/ARM을 대체하지 않습니다. 구버전 PC는 bit4를 거부하므로 APK와 Receiver를 함께 업데이트해야 합니다.

송신자는 계산 후 범위를 clamp합니다. 수신자는 범위를 벗어난 값과 NaN/Inf를 정상값으로 바꿔서 쓰지 않고 거부합니다. 잘못된 패킷은 watchdog 갱신 대상이 아닙니다.

고정 레이아웃 검사용 입력: session `0x0102030405060708`, sequence `0x01020304`, sent time `0x0102030405060708`, ACK `0x0A0B0C0D`, steer -0.5, throttle 1, brake 0.5, buttons `0x1000`, flags `0xE`, epoch 1. 기대 헤더/본문 바이트는 `reference/test_controller_core.py`에 리터럴로 명시했습니다. 공개 키 `bytes(range(32))`는 이 테스트에서만 사용합니다.

### Status — kind 3, payload 4, 총 52바이트

| Payload offset | 형식 | 이름 | 의미 |
|---:|---|---|---|
| 0 | uint8 | state | 0 입력 해제, 1 운전 활성 |
| 1 | uint8 | reason | 0 정상, 1 시간초과, 2 센서, 3 비활성화, 4 보정, 5 사용자 해제, 6 세션, 7 출력 오류, 8 권한, 9 새 입력 복귀 인증 대기 |
| 2 | uint16 | reserved | 0 |

PC는 20Hz로 보내고 header ACK에 최근 수용한 Control sequence를 넣습니다. 수용한 Control이 없으면 ACK=0이며 폰은 이를 RTT 표본으로 쓰지 않습니다. 출력 오류는 일반 연결 성공과 구분해 표시합니다.

### Haptic — kind 2, payload 8, 총 56바이트

| Payload offset | 형식 | 이름 | 의미 |
|---:|---|---|---|
| 0 | uint8 | event | 0 정지, 1 잠김, 2 헛돎, 3 일반 그립, 4 충돌, 5 연석, 6 변속, 7 엔진, 8 게임패드 원시 진동(앱 0.2.0 확장) |
| 1 | uint8 | level | 정지=0, 그 외 1/2/3 |
| 2 | uint16 | leaseMs | 정지=0, 그 외 1…150, 기본 100 |
| 4 | uint32 | reserved | 0 |

패턴의 구체적 timing/amplitude는 폰의 버전이 있는 프로필에서 결정합니다. 같은 이벤트 갱신은 pattern start를 재설정하지 않고 큐를 누적하지 않습니다. 입력 해제/앱 전환 시 즉시 `cancel()` 합니다. event 8은 25ms ON/25ms OFF 반복을 lease로 제한하고, UI 큐 지연만큼 남은 lease를 줄입니다. PC motor source는 500ms 이내만 유효하고 운전 해제 시 폐기합니다. 구버전은 알 수 없는 event 8을 거부하므로 이 확장은 PC/폰 모두 0.2.0 동시 업데이트가 필요합니다. 기본 비활성이고 TC/ABS 의미를 부여하지 않습니다.

### USB framing (앱 0.2.0 — 0.5.5에서 제거됨)

아래 USB framing은 과거 기록입니다. 0.5.5부터 controller는 Wi-Fi UDP만 사용하며, TCP 26761 / ADB reverse / USB QR은 지원하지 않습니다.

승인된 ADB reverse를 통해 PC와 폰의 127.0.0.1:26761 TCP를 연결합니다. 각 PWR1 패킷 앞에는 **2바이트 big-endian 패킷 길이**가 붙고 PWR1 본문은 기존 little-endian 그대로입니다. 길이 48–68만 허용하고 정확한 packet kind별 길이는 codec이 검사합니다. 분할 수신은 ReadExactly로 조립하며 별도 watchdog은 계속 실행합니다. TCP_NODELAY 및 PC 송신 100ms deadline을 적용합니다. Wi-Fi는 기존 UDP 26760입니다. USB 재접속은 새 PC 세션과 명시적 재활성화를 요구합니다.

## 서로 다른 시계 없이 오래된 패킷 제한

순번만으로는 “새 순번이지만 500ms 지연된 패킷”을 구분할 수 없습니다. 다음 ACK 방식으로 상한을 제한합니다.

1. PC는 자신이 전송한 Status/Haptic 순번과 PC 단조 시각을 최근 이력에 저장합니다.
2. 폰은 실제 인증하여 받은 최신 PC 순번을 이후 Control의 ACK로 되돌립니다.
3. PC는 그 ACK가 자신의 이력에 있고 발행 후 100ms 이내인 Control만 수용합니다. PC→폰 전송, 다음 입력 주기, 폰→PC 전송이 모두 이 상한 안에 들어야 합니다.
4. 반대 방향으로 폰은 Control 송신 이력을 저장합니다. Haptic의 ACK가 자신의 최근 Control에 해당하고 송신 후 100ms 이내일 때만 새로운 진동 상태를 받아들입니다.
5. PC는 별도로 텔레메트리 source age를 검사합니다. ACK가 최신이어도 차량 데이터가 오래되었으면 Haptic을 생성하지 않습니다.

시계 동기화가 필요하지 않지만 통신이 느리면 일부 유효 패킷도 보수적으로 거부됩니다. 카운터와 분포를 남겨 임계값 변경을 검토합니다. 이 상한을 one-way latency의 실측값이라고 표시하지 않습니다. 최근 순번 이력은 시간 기준으로 정리하고 최대 512개 등 메모리 상한도 적용합니다.

## 입력 상태 머신

시작 상태는 입력 해제입니다. `ARM=0`, 유효성 flags=0xE, steer 절댓값 ≤0.05, 두 페달 **정확히 0**, buttons=0인 유효 패킷을 최소 300ms 연속 수신한 다음, 중립인 `ARM=1` 상승 전이를 받아야 활성화합니다. 사이에 150ms 이상의 수신 공백이 있으면 대기를 초기화합니다. ARM=1을 계속 보내는 것만으로 복구되지 않습니다.

0.5.6의 한정된 예외: 이미 활성화된 bit4 지원 연결만, 마지막 유효 입력 +150ms 시점에 **즉시 중립 출력**하고 state=0/reason=9로 전환합니다. 이 만료 시각 이후에 PC가 발급한 challenge를 ACK하는 새 CONTROL이 다음 100ms 안에 도착해야 복귀합니다. 동일 세션/epoch, 최신 sequence, challenge age≤100ms, READY와 ARM이 모두 필요합니다. 복귀 시 그 새 CONTROL만 출력하며 보관된 과거 입력을 재생하지 않습니다. 거절 패킷은 복귀 deadline을 연장하지 않습니다. watchdog이 늦게 실행되어도 deadline은 원래 만료 시각 기준입니다.

100ms 복귀 기한 만료, HELLO 재연결, 센서/포커스/READY 상실, ARM=0, epoch 변경, 출력 오류는 hard disarm으로 전환하여 기존 중립 재활성화를 요구합니다. Android는 유효한 reason=9 동안에만 현재 ARM 의사·보정을 유지하며 로컬 센서/포커스/STATUS 신선도 검사를 계속합니다. bit4 없는 구형 송신자의 상태 전이는 바뀌지 않습니다. 150ms 안전 출력 만료나 100ms 인증 기한을 늘린 기능이 아닙니다. 실제 네트워크 단절 동안의 입력 복원 또는 무중단 보장은 아닙니다.

활성 상태에서도 ARM=0, 센서 무효, 비활성 Activity, 보정 epoch 변경, 출력 오류는 즉시 해제합니다. 150ms 동안 새로운 유효 Control이 없으면 전 축·버튼을 중립으로 만듭니다. watchdog은 패킷 수신 콜백 밖에서 독립적으로 실행해야 합니다. PC UI가 멈춰도 동작하도록 출력 worker에 둡니다.

Android에서도 입력 상태를 독립 관리합니다. 센서가 멈췄는데 마지막 회전값을 새 timestamp로 계속 보내지 않습니다. 센서 timestamp 100ms 이상 정체, Activity 종료, 터치 취소, 권한 회수 시 폰이 먼저 해제하고 다음 전송에도 해당 flags를 반영합니다.

서버 종료 시 중립 제출과 virtual device 해제를 순서대로 수행합니다. 프로세스 강제 종료·PC 절전·USB 제거 시 실제 드라이버의 해제 행동은 하드웨어 테스트로 확인합니다. 이 계약의 타이머 수치가 OS의 실시간 실행을 보장하지는 않습니다.

## 구현 범위와 이식 검사

현재 Python은 Control codec, PC SafetyGate, 우선순위 참조 로직만 구현합니다. Hello/Status/Haptic의 실제 codec·socket·Android lifecycle·전송 challenge 관리의 완성 통합은 Codex가 구현해야 합니다. 모든 데이터 방향에 인증·길이·reserved 검사를 적용한 뒤 Kotlin↔C#의 바이트 동일성과 손실·중복·역순·지연 동작을 검증합니다.
