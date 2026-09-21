# mDrive 0.3.2 — 중앙 버튼 간격 / Wi-Fi 진단

## 변경

- 방향키/ABXY를 각각 바깥쪽으로 최대 화면 너비의 2.5% 이동. 크기와 수직 위치 유지.
- 기존 페달 폭을 사용하여 0.2% 너비의 간격을 남김. 25% 폭 페달에서는 각 묶음을 0.8% 이동하며, 20.25% 폭에서는 2.5% 이동.
- 기본 스틱 반경(화면 너비의 최대 4.3%)과의 수평 간격은 각각 최소 1% / 2.7% 너비가 됨. 2316px 화면에서는 약 23px / 63px.
- `look_spacing_v6` 한 번만 실행. 원본 JSON은 `layout_before_spacing_v6`에 보존. 레이아웃 초기화에도 새 간격 적용.
- 레이아웃 편집 경계를 실제 페달 폭에 맞춰 계산. 페달을 넓힐 때도 버튼이 겹치지 않도록 경계 적용.
- 조향, 페달 응답, 스틱 입력, 프로토콜, Receiver, Wendy 음성 로직 변경 없음.

파일: `ButtonGroupSpacing.kt`(새 파일), `PedalTouchView.kt`, `ButtonGroupSpacingTest.kt`(새 파일), `app/build.gradle.kts`(0.3.2 / code 10), README, 이 기록.

## Wi-Fi

읽기 전용 검사에서 실행 중 Receiver의 UDP 172.30.1.90:26760, Wendy TCP 172.30.1.90:26762, Telemetry UDP 127.0.0.1:20777 리스너 확인. Wi-Fi 프로필은 Public, PhoneWheel.Receiver 인바운드 Allow/Public 규칙 2개 확인. 이 검사는 폰에서 오는 실제 패킷의 통과까지 증명하지는 않음.

사용자가 재연결 후 Wi-Fi 정상 연결을 확인함. 일시 실패의 정확한 원인은 미확정. 연결 코드/방화벽 변경이나 Receiver 재시작은 하지 않음.

## 검증

명령: `android/gradlew.bat --offline testDebugUnitTest assembleDebug lintDebug` (프로젝트 JDK/SDK/Gradle 캐시).

결과: BUILD SUCCESSFUL (1m 27s). Kotlin 39 tests, 0 failures/errors. APK metadata 0.3.2 / versionCode 10 확인. `lintDebug` 통과.

새 순수 기하 테스트: 좁은 페달에서 대칭 이동, 넓은 페달과 비겹침, 이미 이동한 위치/페달 경계에서 추가 이동 없음.

휴대폰 설치·터치 실기는 미실행. 빌드 산출물: `android/app/build/outputs/apk/debug/app-debug.apk`.
