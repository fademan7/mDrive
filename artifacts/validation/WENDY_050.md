# Wendy 0.5.0 — 2026-09-18

## 적용 결과

- Android versionCode 12 / versionName 0.5.0: Fold5 SM-F946N에 `adb install -r` Success. 기존 앱 데이터/조향/페달/레이아웃 설정 보존. `dumpsys package`로 버전 확인, 앱 실행 후 PID 16094 확인. 최근 종료 사유는 패키지 업데이트이며 새 시작 crash는 관측하지 않았습니다.
- PC `release/receiver/PhoneWheel.Receiver.exe`를 새 Release publish 결과로 교체하고 사용자 승인 후 Wi-Fi QR / Engineer ON으로 재시작했습니다. PID 132896, MainWindowTitle `PhoneWheel · Wi-Fi QR 연결`, Responding true. 172.30.1.3:26760 UDP, 172.30.1.3:26762 TCP, 127.0.0.1:20777 UDP 리스너 확인.
- 새 EXE SHA256 `99339347BE111109342FFC22818A8802ED448099103773DD671EDDAA0F3C5EDA`가 `release/receiver-050` 빌드와 일치. 기존 0.4.0 EXE는 `release/backups/PhoneWheel.Receiver-before-050.exe`에 보존했습니다. 최초 교체는 종료 직후 파일 잠금으로 실패했으나 프로세스 종료 재확인 후 복사/해시/재실행 완료.
- 모델/CPU 런타임은 0.4.0의 검증한 번들을 그대로 사용. 새 다운로드나 GPU backend 없음. 기존 0.4.0 ZIP은 새 버전 배포물이 아닙니다.

## 기능과 제한

- Android 전경 전용 Always Listening 옵션, 기본 OFF. 온디바이스 영어만, 미지원/권한 오류 시 중단하고 PTT 안내. 반복 세션 사이 1.2초 공백, TTS 중 마이크 취소, 연결/포커스/앱 종료 시 정리, 늦은 recognizer callback ticket 무효화, 3회 실패 backoff. AUTO/LISTENING 시 발화 전 경고만 허용. 실제 Fold5 온디바이스 영어 인식/장시간 오디오/배터리 시험은 미실행입니다.
- 피트 조언·계획/취소·랩 리포트·코칭 의도 추가. Box box는 리마인더만 저장하며 게임 request를 보냈다고 말하지 않습니다. 기존 실제 설정 쓰기 미지원 상태 유지.
- 공식 F1 25 UDP v3의 session track/totalLaps/피트 윈도/복귀 순위, lap distance/current time/driver status, status compound/age를 추가로 사용. Session payload 653/654/655 위치는 127+64×8+2+3×4 산식 및 문서와 대조. NaN distance, forecast count, rejoin position 검증 추가.
- 첫 완전 정상 랩을 기준으로 즉시 다음 랩부터 거리 정렬 비교, 더 빠른 정상 랩으로 갱신. 랩 시간/이전 clean lap delta/clean best 리포트. 세션 메모리에 한정. 날씨/셋업/타이어 변경 및 부적절한 랩 제외.
- 기준 브레이킹 구간 entry 속도 차이와 실제 구간 시간 손실을 함께 확인하는 제한적 코칭. 이상적 속도/라인/물리 원인 분석이 아니며 코너 번호/섹터 리포트/연료·온도 보정/선수 이름은 아직 없습니다. 첫 레이스 랩은 학습용 기준이지 최적 랩이 아닙니다.
- 순위상 같은 랩 뒤차 gap <=1.5s가 3초 지속 시 알림, 45초 쿨다운. 사각지대 spotter가 아닙니다.
- 세부 사용법/임계값: [WENDY.md](../../docs/WENDY.md).

## 실제 실행 결과

- `.tools/dotnet/dotnet.exe run --project windows/tests/PhoneWheel.Tests/PhoneWheel.Tests.csproj --no-restore -c Release`: **22/22 groups PASS**.
- 새 시험: 피트 의미 구분/부정/애매한 문장/낮은 신뢰도/미실행 명시/공식 offset/오래된 데이터, 첫 랩 reference/두 번째 랩 구간 코멘트/소비 후 재생 금지/랩 delta/날씨 reset/partial·invalid·yellow·pit·pause·gap 제외/session reset, 뒤차 지속·반복 억제.
- 실제 CPU classifier: **36/36 대표 문장 PASS**. 초기 34/36에서 피트 조언과 상태 혼동, brake bias query UNKNOWN이 드러났습니다. 피트 의미 정의를 분리하고 명확한 brake bias readback을 결정적 규칙으로 처리한 후 통과. 대표 문장 시험이며 전체 영어 정확도/실발화 STT 성능이 아닙니다.
- `.tools/dotnet/dotnet.exe publish windows/src/PhoneWheel.Host/PhoneWheel.Host.csproj --no-restore -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o release/receiver-050`: 성공.
- Android `gradlew.bat --offline testDebugUnitTest assembleDebug lintDebug`: **BUILD SUCCESSFUL**, **41 tests**, lint **0 errors / 25 warnings**. AlwaysListenPolicy의 기본 OFF/lifecycle/중복 시작 금지/무음 간격/backoff/fatal 중단 시험 포함. 초기 SDK analytics 경로 경고는 빌드 실패가 아니며 실제 APK 생성/설치 확인.
- `tools/test_usb_transport.py --receiver release/receiver-050/PhoneWheel.Receiver.exe`: PASS, partial-frame watchdog **152.659ms**, 독립 양페달/분할/잘못된 길이/held input/중립 rearm/재접속.
- `tools/test_wifi_recovery.py --receiver release/receiver-050/PhoneWheel.Receiver.exe`: PASS, active endpoint 보호/stale peer 재인증/held throttle 차단/중립 rearm.
- `tools/test_wendy_transport.py --receiver release/receiver-050/PhoneWheel.Receiver.exe`: PASS, 10개 조회+피트 계획/취소/조언, 설정 쓰기 제외, PTT 발화 중 경고 금지/AUTO idle-listening 경고 허용, HMAC/새 nonce/신선도/실제 CPU 추론 중 controller active.

## 성능 관측 / 미검증

6초 합성 loopback 측정 (한 코어 = 100%):

| 조건 | Receiver CPU | Receiver working set | Controller ACK p95 |
|---|---:|---:|---:|
| Engineer OFF | 3.90% | 37.29 MiB | 8.92ms |
| Engineer ON, 모델 로드 전 | 5.10% | 44.65 MiB | 9.10ms |
| CPU 의도 추론 포함 | 별도 native process | 아래 참조 | 9.10ms |

모델 연속 시험 CPU 196.1% of one core (16 logical CPU 전체 대비 약 12.26%), idle 3초 7.29% of one core (전체 약 .46%), 종료 직전 working set 955.8MiB. 이전과 동일한 2스레드 CPU 모델입니다. 랩 분석은 10Hz/20m 고정 상한 1501 표본씩 2랩이며 모델 추론 없이 규칙 연산만 수행합니다.

위 수치는 실제 F1 주행 FPS/물리 입력 지연/무선 라디오 안정성/코칭 정확도 측정이 아닙니다. 합성 통신 부하 데이터는 정상 풀랩 코칭 부하를 대표하지 않습니다. Fold5 Always Listening 서비스 지원·배터리·오인식·TTS echo 및 실제 첫 랩 → 두 번째 랩 코칭은 사용자의 게임 테스트가 필요합니다. 컨트롤러 코덱/송신 루프/조향·페달·스틱 계산은 변경하지 않았습니다.

## 변경 파일

Core: `F1RaceState.cs`, `WendyIntent.cs`, `WendyEngineer.cs`, 새 `WendyCoaching.cs`.
Host: `CpuIntentModel.cs`, `WendyService.cs`.
Android: `WendyVoice.kt`, `WendyClient.kt`, `MainActivity.kt`, 새 `AlwaysListenPolicy.kt`, `app/build.gradle.kts`.
Tests: `WendyTests.cs`, `WendyModelTests.cs`, `Program.cs`, 새 `WendyCoachingTests.cs`, 새 `AlwaysListenPolicyTest.kt`; `tools/test_wendy_transport.py`.
Docs: `README.md`, `docs/WENDY.md`, `docs/PROTOCOL.md`, 이 보고서.

## 테스트 순서

새 PC QR로 연결 → 폰 Wendy ON → PTT로 `Do I need pit in?`, `Box box`, `Stay out`, `How was my last lap?` → Always Listening을 ON하고 손대지 않고 질문 → TTS가 자기 말에 재응답하지 않는지 확인 → 같은 조건으로 정상 한 랩 완료 후 다음 랩 비교 → 피트/황기/일시정지 시 잘못된 코칭 없는지 확인. 권한 최초 허용과 옵션 변경은 정차 상태에서 수행하세요.
