# PhoneWheel 구현 및 검증 요약

실행일: 2026-09-12.

## 구현 결과

- C# .NET 10 Windows Core/Host/Probe/ViGEm 출력 및 test runner를 추가했습니다.
- PWR1 Hello/Control/Status/Haptic의 little-endian layout, truncated HMAC-SHA256, reserved/range/finite 검사를 C#과 Kotlin에 구현했습니다.
- Host에 인증 Hello endpoint lock, receiver-clock ACK freshness, 150ms 독립 watchdog, 300ms 중립+ARM 상승 재활성화, single-reader gamepad worker를 구현했습니다.
- null/CSV/ViGEm Xbox 360 출력 백엔드와 폰 없는 Probe를 구현했습니다.
- Android 앱에 sensor worker, 상대 quaternion 조향, pointer ID 독립 페달/버튼, foreground/cancel/stale 해제, UDP client, haptic lease/priority/no-queue scheduler를 구현했습니다.
- 모바일 기본 배치를 왼쪽 브레이크/오른쪽 가속/상단 F1 보조 키로 개편했고, 끌어서 위치 이동·선택 확대/축소·기본값 복원·기기 저장을 구현했습니다.
- 휴대폰 조향 반경(한쪽 45–180°, 기본 180°), 데드존, 0–30ms 평활, 중앙 응답 곡선, 페달 민감도를 옵션에서 조정할 수 있습니다.
- F1 2025 Motion Ex 273-byte의 공식 문서 대조 일부 reader를 C#에 이식했습니다. 2026 parser와 AC adapter는 의도적으로 활성화하지 않았습니다.

## 실제 PASS

- Python 참조 모델: 37/37 tests.
- C#: 8/8 test groups.
- Kotlin: 10/10 tests.
- Kotlin↔C#: 인증 Control datagram을 파일로 교환해 양쪽 decoder 통과.
- Windows 11 x64에서 서명된 ViGEm driver 1.21.442.0과 client 1.21.256 native load 성공.
- XInput slot 0 실제 관찰: LX -16384/+16384, LT 128, RT 128, LT+RT 255/255, 종료 중립.
- localhost 실제 UDP: Hello/Status/Control, 300ms neutral dwell, ARM 상승, 동시 페달, 완전 침묵 처리. 마지막 Control→중립 163.968ms, Host exit 0.
- Android debug APK build 성공. SHA-256 `3525684276622509BE6F36536A0C68B586304C0A657F730BECAA47E90298AD47`.
- Android Lint: 0 errors, 10 warnings(버전/현지화/아이콘/대화면 회전 정책 등 비차단 항목).

## NOT RUN / DISABLED

- Fold5 SM-F946N(Android 16)에 최신 APK 설치와 화면 실행은 2026-09-12에 확인했습니다. 센서율·각도·동시 터치·진동 실측은 NOT RUN입니다.
- F1 25 2026 Season Pack과 원작 Assetto Corsa의 게임 입력 인식은 NOT RUN입니다.
- F1 실캡처, 2026 전용 포맷, AC Kunos 공유메모리 원문 대조는 NOT RUN입니다.
- slip 의미가 검증되지 않아 telemetry detector와 게임 이벤트 진동 송신은 DISABLED입니다.
- Wi-Fi/USB 비교, motion-to-photon, 30분 주행, 진동 구별 시험은 NOT RUN입니다.

정확한 명령·환경·증거 파일은 [Gate 검증 기록](artifacts/validation/GATE_RESULTS.md)을 참조하십시오. Python 보고서는 [verification/REPORT.md](verification/REPORT.md), 사용자 실행 절차는 [README.md](README.md)에 있습니다.

## 2026-09-12 설치 및 단일 실행 파일

- `release/receiver/PhoneWheel.Receiver.exe`: self-contained Windows x64 단일 EXE 게시 성공. .NET 런타임 및 native ViGEm client 포함, ViGEmBus 드라이버는 별도입니다.
- `START_RECEIVER.cmd` 더블클릭 진입점과 `tools/publish-receiver.ps1` 재빌드 스크립트를 추가했습니다.
- 기본 실행은 ViGEm 출력, LAN IP 자동 선택, 연결 정보 표시 및 4Hz 입력 모니터입니다. 모니터는 수신/출력 watchdog과 별도 작업입니다.
- 단일 EXE에서 실제 ViGEm 초기화 후 0.5초 종료 시험 exit 0, C# 기존 8/8 groups PASS. 사용자용 리시버 창을 실행해 두었습니다.
- Fold5 APK 설치 `Success`, Android 16, 앱 화면 확인. 증거: `artifacts/validation/phonewheel-device-screen.png`.
- PC Wi-Fi 172.30.1.59 / 폰 Wi-Fi 172.30.1.71을 확인했으나 자동 입력 중 ADB 연결이 끊겨 end-to-end 입력 확인은 미완료입니다.
- 공용 네트워크에 휴대폰 IP 한정 영구 방화벽 규칙을 추가하려던 작업은 자동 승인 검토가 명시적 승인 부족으로 거부했습니다. 방화벽 변경은 수행하지 않았습니다.

## 전체화면 페달 및 180° 조향 후속

- 양끝 25% 폭, 전체 높이 페달. 폭은 편집에서 20–25%로 조절하며 중앙 버튼과 겹치지 않도록 제한합니다.
- 가로 고정, 시스템바 숨김, cutout 영역까지 채우고 이전 상하 inset 여백을 제거했습니다. 연결/민감도/배치 편집은 옵션 메뉴로 이동했습니다.
- 페달 기본 이동 거리는 높이의 55%, 상단 15%는 최대 입력 유지 구간입니다. 시작점에서 0%로 시작하며, 중간에서 시작해도 위쪽 마진에 닿으면 100%가 되도록 유효 이동 거리를 계산합니다.
- 조향은 연속 각도 변화 누적으로 ±180° 경계를 넘나들어도 좌우가 뒤집히지 않습니다. ±181° 왕복, quaternion 부호 반전, 중앙 복귀, 센서 공백 후 재보정 요구를 단위 테스트했습니다. 실제 물리 회전 정확도 실측은 아직 아닙니다.
- 첫 실기에서 발견한 화면 생성 전 insets 접근 오류를 수정했습니다. USB 재연결 후 최종 APK 설치 `Success`, 정상 시작 및 2316×904 전체화면을 확인했습니다. 최종 APK: 6,756,273 bytes.
- Fold5에 ADB 터치 입력: 가속 x=2000, y=800에서 시작 → y=550에서 50% → y=300에서 100% → UP 후 0%. 화면과 캡처로 확인했습니다. 실제 사람의 손가락 사용성·PC 전달과는 구분합니다.
- 옵션 메뉴에서 조향 설정을 열고 아래쪽 페달 민감도 입력까지 스크롤됨을 확인했습니다. 값을 바꾸지 않고 취소해 주행 화면으로 돌아왔습니다.
