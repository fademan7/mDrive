# Wendy HUD 0.3.1 — 2026-09-16

## 요청 및 변경

- 기존 상단 왼쪽 중앙 보정 버튼 자리에 PTT 이동. 오른쪽에 중앙 보정/옵션 유지.
- PTT 바로 옆에 OFF 또는 IDLE/LISTENING/PROCESSING/SPEAKING/ERROR 표시. 상태 영역 탭은 Wendy 설정/최근 응답.
- 가운데 44dp 정사각형 FlagBadgeView. 양쪽 그룹을 같은 weight로 배치해 좌우 콘텐츠 길이와 무관하게 화면 정중앙 유지.
- GREEN/YELLOW/RED/BLUE는 단색. SC/VSC는 황색+검정 문자. CHECKERED는 4×4 흑백 체크. UNKNOWN/미지원 값은 회색+대시.
- 색만으로 구분하는 플래그도 accessibility 설명 제공. 실제 값이 달라질 때만 badge 갱신.
- 조향·페달·Right Stick·PTT 음성 처리와 전송·Receiver 코드 변경 없음.

파일: MainActivity.kt, 새 FlagBadgeView.kt, 새 FlagStyleTest.kt, app/build.gradle.kts(0.3.1 / code 9), README.md, docs/WENDY.md, 이 기록.

## 검증 범위

빌드 명령: workspace JDK/SDK/Gradle 캐시로 `android/gradlew.bat --offline testDebugUnitTest assembleDebug lintDebug`.

결과: BUILD SUCCESSFUL. Kotlin 36 tests, 0 failures/errors. Android lint 0 errors / 24 warnings. APK metadata 0.3.1 / versionCode 9 확인.

일반 색상 구분, SC/VSC 문자, Checkered, UNKNOWN이 GREEN으로 바뀌지 않는 동작을 순수 스타일 테스트로 확인합니다. Android 캔버스 렌더링/물리 터치 실기는 별도 확인이 필요합니다.

휴대폰 설치 및 F1 실기 시험은 이번 UI 수정에서 하지 않았습니다. APK는 `android/app/build/outputs/apk/debug/app-debug.apk`이며 기존 앱 위에 업데이트해야 반영됩니다.
