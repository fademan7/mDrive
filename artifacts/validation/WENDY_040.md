# Wendy 0.4.0 구현·검증 — 2026-09-17

## 결과와 범위

Tiny LLM 자연어 분류/확장 조회/진단 표시/보수적 추세 감지/승인 후 차고 검토 권고 저장까지 구현했습니다. **전체 차량 동역학 진단과 자동 수치 셋업 변경이 완성된 것은 아닙니다.** 기존 controller 코덱/송신 주기/조향/페달/Right Stick/안전 게이트는 변경하지 않았습니다.

## 모델과 배포

- [공식 Qwen3 0.6B GGUF](https://huggingface.co/Qwen/Qwen3-0.6B-GGUF), Q8_0, 639,446,688 bytes (약 610 MiB), Apache-2.0.
- 모델 revision `23749fefcc72300e3a2ad315e1317431b06b590a`, SHA256 `9465e63a22add5354d9bb4b99e90117043c7124007664907259bd16d043bb031`.
- [공식 llama.cpp b10964](https://github.com/ggml-org/llama.cpp/releases/tag/b10964), Windows CPU x64, MIT. 안정 릴리스 v0.4.1이 가리키는 빌드. 다운로드 ZIP 18,427,629 bytes, SHA256 `917f39c076402c421224824607397af20f53625a60defc20e8dd22446bf4c5d7`.
- `tools/prepare-wendy-model.ps1`이 고정 URL/해시 검증/라이선스 포함을 수행합니다. `tools/package-wendy-receiver.ps1`은 self-contained Receiver와 모델/런타임을 함께 ZIP 배포합니다. 최종 사용자는 Python/Ollama/별도 AI 프로그램을 설치하지 않습니다. ViGEmBus와 USB 사용 시 ADB는 기존 요구사항 그대로입니다.
- 약 821 MB(783 MiB) 압축 해제 용량. 이전 단일 EXE 약 117 MB 대비 약 705 MB 증가. 모델은 EXE 안이 아니라 옆 `wendy` 디렉터리에 있으므로 EXE만 복사하면 안 됩니다.

## Pipeline 진단과 오분류 수정

기존 구현은 모든 문장 중 tyre/tire 포함 여부를 먼저 검사하는 substring 규칙이었습니다. 무조건 타이어로 fallback하는 코드는 없었지만, 문맥 구분 없이 타이어 단어를 우선했고 자연어 표현 범위가 좁았습니다. 마이크 실발화 전사와 분류를 구분할 수 없었던 문제에 Heard / Intent / Response / Source 표시를 추가했습니다.

0.6B 모델에 모든 필드를 한 번에 생성시킨 첫 시도는 20/30으로 실패했습니다. 의도 분류와 거동 세부 추출을 나눈 후 30/30 대표 문장 테스트가 통과했습니다. 이 테스트의 일부는 프롬프트 예시이므로 일반 영어 전체에 대한 정확도 수치가 아닙니다. 소음 환경 Android STT 정확도도 별도 실기 검증이 필요합니다.

LLM은 intent/symptom/phase/condition/wheel enum만 반환합니다. 추가 필드·숫자 enum·잘못된 조합을 거부하고 telemetry/설정값/자유 답변을 출력 경로에 연결하지 않습니다. 일반 질문의 특정 바퀴는 원문의 명시적 좌우/앞뒤만 사용합니다. 승인·거절과 변경 요청은 안전 규칙으로 별도 처리합니다. 미인식은 `I didn't understand that.`이며 타이어로 대체하지 않습니다. 모델 실패 때는 제한적 Rules fallback을 표시합니다.

## 지원 의도

GET_GAP_AHEAD, GET_GAP_BEHIND, GET_TYRE_STATUS, GET_FUEL, GET_ERS, GET_DAMAGE, GET_POSITION, GET_LAP, GET_WEATHER, GET_FLAGS, GET_PENALTIES, GET_PIT_STATUS, GET_BRAKE_BIAS, GET_DIFFERENTIAL, DRIVER_FEEDBACK, CONFIRM, REJECT, CHANGE_SETTING(실행 제외), UNKNOWN.

답변 수치는 [EA 공식 F1 25 UDP v3](https://forums.ea.com/t5/s/tghpe58374/attachments/tghpe58374/f1-games-game-info-hub-en/61/4/Data%20Output%20from%20F1%2025%20v3.pdf)의 2025/version1 패킷만 사용합니다. 새 파서는 Car Setup 1133 bytes/50-byte car 및 Car Telemetry 1352 bytes/60-byte car를 길이/범위 검증 후 읽습니다. Lap의 penalty/warnings/pit 값도 사용합니다. 수신·필드별 신선도, pause/spectator/세션/flashback 안전성을 유지합니다. 날씨는 현재 상태이며 예보/전략 추론은 없습니다.

## Driver feedback와 셋업

분류 슬롯: UNDERSTEER, OVERSTEER, REAR_INSTABILITY, POOR_TRACTION, WHEELSPIN, FRONT_LOCKING, REAR_BRAKING_INSTABILITY, HIGH_SPEED_INSTABILITY, KERB_INSTABILITY, TYRE_OVERHEATING, UNEVEN_WEAR, EXCESSIVE_DEGRADATION, AERO_BALANCE, BOTTOMING, WEAK_ROTATION, STRAIGHT_LINE_SPEED. Entry/mid/exit/straight/unknown 및 throttle/brake/coasting/kerb/high-speed/unknown을 표현할 수 있습니다. 전 슬롯을 실발화/실차 패턴으로 검증한 것은 아닙니다.

- 실제 구현한 근거 결합: 고온이 30초 이상·두 랩 이상 지속하거나 마모 편차 12 percentage points 이상·최대 마모 20% 이상이 세 랩에 걸쳐 지속 + 신선한 현재 셋업 + 감지된 손상 없음.
- 그때만 다음 세션에 압력/캠버/차량 밸런스를 검토하도록 제안합니다. 정확한 압력/윙/디퍼렌셜 값 변경량은 처방하지 않습니다.
- 명확한 승인, confidence >= 0.75, 동일 세션, 30초 내 유효한 제안이 있어야 권고를 저장합니다. 거절/다른 질문/만료/세션 변경은 이전 제안을 무효화합니다. 승인 상태를 게임 변경 완료로 말하지 않습니다.
- 저장: LocalAppData/mDrive/wendy-recommendations.json, 최대 100개. PC 상세 창에 이전 세션 권고와 근거 표시. 일반 음성 전사/녹음은 저장하지 않습니다.
- **즉시 변경 가능한 setting: 없음.** 현재 UDP는 읽기 전용이며 검증된 설정 쓰기 어댑터나 사용자별 안전한 직접 바인딩이 없습니다. 기존 컨트롤러에 메뉴 키 연타를 주입하지 않았습니다.
- **미지원:** under/oversteer·traction·wheelspin·locking·kerb·bottoming·aero/drag의 원인/수치 처방 및 이를 기반으로 한 자동 변경. 운전자 말만으로 “텔레메트리도 같은 증상”이라고 확인하지 않습니다. 물리 파라미터/차종/타이어/주행 보조별 검증과 실제 게임 데이터가 더 필요합니다.

## Proactive alerts

기존 yellow/red/SC/VSC/checkered, wear 40/55/70%, 손상, low fuel, 날씨/피트 변화에 다음을 추가했습니다.

- Blue flag.
- 누적 페널티/미처리 drive-through/stop-go 및 track-limits 경고 증가.
- inner tyre temperature >= 110°C가 10초 이상 지속. 이는 제품 경고 임계값이며 모든 타이어의 공식 overheating 경계라는 주장이 아닙니다.
- 같은 앞차 gap 4표본/15초에서 연속 0.25초 이상 감소/증가.
- 연속 정상 랩 4개에서 매번 0.7초 이상 늦어짐. 원인은 미확정이라고 말합니다.
- 다중 랩 온도/불균등 마모에 대한 다음 세션 검토 제안.

분석은 최대 10Hz, 고정 길이 큐입니다. SC/피트/비활성/오래된 패킷/날씨 변경에서 추세를 초기화합니다. 전체 8초 쿨다운과 항목별 15~300초 제한을 사용합니다. 2025 FIA 플래그 enum에 Black/White가 없어 track warning을 그 플래그로 단정하지 않습니다.

## 성능과 격리

컨트롤러와 별도 TCP/worker/락. CPU 분류를 telemetry 락 밖에서 await하고 폰 상태 폴링은 계속합니다. 고정 2 CPU threads, BelowNormal, poll 0, context 2048, parallel 1, GPU layers 0/device none/no-op-offload. CPU DLL만 있는 패키지이며 실행 중 ggml-cpu-zen4.dll 확인, CUDA/Vulkan/OpenCL backend 없음. GPU 사용량 하드웨어 카운터를 측정한 것은 아닙니다.

모델 첫 로드 약 3.6~4.5초, 이후 대표 조회 약 0.24~0.42초, 거동 세부 추출 약 1.3~3.3초. 모델 working set 약 909~964 MiB. 연속 추론 부하는 단일 코어 100% 기준 196.9%(두 스레드), 3초 Idle 측정 5.73%. 이 PC의 논리 CPU 16개 기준 전체 CPU로는 약 12.31% / 0.36%에 해당합니다. 이 수치는 F1과 실제로 주행하면서 측정한 FPS/게임 입력 지연이 아닙니다.

6초 합성 비교: OFF Receiver 3.64% of one core / 37.90 MiB / ACK p95 9.05ms. ON 모델 로드 전 Receiver 4.85% / 46.03 MiB / p95 9.01ms. 이후 실제 CPU 추론을 포함한 ACK p95 9.00ms이며 컨트롤러 active 상태 유지. 모델 메모리는 Receiver 메모리와 별도로 위에 기재했습니다.

OFF에는 모델/telemetry/network worker를 시작하지 않습니다. 최초 질문에 로드하고 OFF 때 정리합니다. 강제 Receiver 종료에서도 모델이 남지 않도록 Windows kill-on-job-close를 적용했으며 재시험했습니다.

## 빌드·테스트·설치

- .NET Release publish/self-contained/single-file 성공. C# 19/19 groups PASS.
- Android `--offline testDebugUnitTest assembleDebug lintDebug`: 성공. 39 tests, lint 0 errors / 26 warnings.
- 실제 Qwen CPU 자연어 30/30 PASS. [문장별 결과·시간·메모리·로드 모듈](wendy-model-040.txt).
- USB TCP malformed/fragmentation/독립 트리거/partial frame watchdog/재연결/held-input/중립복구: PASS, 152.337ms watchdog.
- Wi-Fi loopback active peer 보호/reauth/held throttle 차단/중립복구: PASS.
- Wendy 10개 실제 응답/명령 제외/MAC 변조 거부/새 nonce/신선도/실제 CPU 추론 중 컨트롤러 동작/강제종료: PASS. [측정 로그](wendy-040-transport.txt).
- Fold5 SM-F946N APK `adb install -r`: Success, 0.4.0 versionCode 11 확인. 이전 설정/앱 데이터 삭제 안 함.
- 실제 F1 주행 중 음성 인식/TTS/경고·셋업 효과·FPS는 미검증입니다. 업데이트된 PC와 새 QR 연결 후 사용자가 비교해야 합니다.

## 변경 파일

### 적용 확인 (2026-09-18)

- 기본 실행 경로 `release/receiver/PhoneWheel.Receiver.exe`를 검증한 새 EXE로 교체했고, `release/receiver-040` 산출물과 SHA256 일치를 확인했습니다.
- 이전 EXE는 `release/backups/PhoneWheel.Receiver-before-040.exe`에 보존했습니다.
- 숨겨진 창 모드로 떠 있던 리시버를 승인된 재시작 범위에서 종료하고 일반 창 모드로 다시 실행했습니다. Windows UI 접근성 검사에서 **PhoneWheel · Wi-Fi QR 연결**, **F1 Engineer ON**, **Telemetry Waiting**, **Wendy IDLE**, **Flag UNKNOWN**, **폰 연결 대기**를 확인했습니다. QR 스캔/실제 게임 주행은 아직 미실행입니다.
- PC DHCP 주소가 바뀌었으므로 현재 리시버 창의 새 QR로 연결해야 합니다. 2026-09-18 확인 주소는 172.30.1.3:26760입니다. 주소를 영구 고정값으로 사용하지 마세요.
- UI 상세 버튼 자동 클릭은 도구의 `coordinate input geometry is unavailable`로 확인하지 못했습니다. 메인 QR 창의 존재/상태 확인과 코드/빌드 테스트를 상세 창 클릭 시험으로 표현하지 않습니다.
- 배포 ZIP: `release/mDrive-0.4.0-win-x64.zip`, 695,869,208 bytes. SHA256 `c9599f0d2a5a997b6ca5d85699489097598529342d33f8636690742ef7ffa807`.
- 모델 평균 working set(30문장 관측 표본): 933.49 MiB. cold load를 제외한 단순 조회 평균 348ms. 부하·측정 범위는 위 제한을 따릅니다.

### 파일 목록

Core: 새 WendyIntent.cs, WendyPatterns.cs; 수정 F1RaceState.cs, WendyEngineer.cs.

Host: 새 CpuIntentModel.cs, CpuProcessJob.cs, WendyRecommendationStore.cs; 수정 WendyService.cs, PairingWindow.cs.

Android: WendyClient.kt, WendyVoice.kt, MainActivity.kt, app/build.gradle.kts.

Tests: 새 WendyNextTests.cs, WendyModelTests.cs; Program.cs, PhoneWheel.Tests.csproj.

Tools: 새 prepare-wendy-model.ps1, package-wendy-receiver.ps1; test_wendy_transport.py, test_wifi_recovery.py, test_usb_transport.py.

문서: README.md, docs/WENDY.md, docs/PROTOCOL.md, 이 기록과 검증 로그. 0.3.2 버튼 간격 변경은 이전 기록을 참조하세요.
