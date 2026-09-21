# 0.2.1 선형 입력 검증 — 2026-09-13

- 사용자 요청: 조작과 입력값이 선형이어야 한다. 기본 비선형 곡선 0.7을 1.0으로 변경하고 추가 데드존 0.5°→0°, 추가 평활 8ms→0ms로 변경. 한쪽 180°는 유지. 페달 위치 1:1 및 기존 요청인 95%→100% 예외는 변경하지 않음.
- 장치의 앱 설정을 읽어 저장된 조향 override가 없고 gameRumble=false만 있음을 확인. 새 기본값이 실제 기기에 적용됨. 레이아웃과 진동 설정은 보존.
- 기본 생성자를 사용하는 합성 회전 시험: 0.1/30/45/90/135/179/180° 및 좌우 왕복/중앙 복귀에서 각도÷180과 매 표본 일치. 필터 지연이나 추가 데드존 없이 검증. 실제 물리 각도 측정을 대체하지 않음.
- Android `gradlew.bat --offline testDebugUnitTest assembleDebug lintDebug`: BUILD SUCCESSFUL (34초). Kotlin 19 tests, C# 11/11 groups PASS. 기존 ZXing deprecation/lint warnings 유지.
- USB ADB 재연결 확인: SM-F946N device. `adb install -r ...app-debug.apk` Success; versionCode=3, versionName=0.2.1, lastUpdateTime=2026-09-13 11:10:26.
- computer-use로 이전 USB 세션을 정상 종료하고 기존 PC EXE를 재실행. 실제 폰 UI에 `USB 연결됨 · 중앙 보정 필요` 확인. PC 코드 변경/재게시 불필요.
- 사용자에게 편한 자세에서 중앙 보정 후 실제 약 45° 회전 시 앱의 각도/% 확인을 요청함. 앱의 계산 선형화와 게임 내 핸들 애니메이션/차량 조향비는 별개. 실제 조향이 미미한 원인을 해결했다고 단정하지 않음.
