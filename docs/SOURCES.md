# 출처와 확인 수준

조회 기준일: **2026-09-07**. 링크의 존재, 문서의 기술적 설명, 실제 장치 호환성을 서로 구분합니다. 아래의 “확인”은 문헌 확인입니다. 이 패키지의 실기 측정 결과는 아직 없습니다. 제3자 요약을 드라이버·게임·API의 보증으로 사용하지 않았습니다.

| ID | 공식 자료 | 확인한 범위와 제한 |
|---|---|---|
| S1 | [ViGEmBus 저장소](https://github.com/nefarius/ViGEmBus), [1.22.0 릴리스](https://github.com/nefarius/ViGEmBus/releases/tag/v1.22.0) | 무료 공개 프로젝트, BSD-3-Clause, 보관·종료 상태와 updater 제거 릴리스. 설치·로드는 미실행 |
| S2 | [ViGEm.NET](https://github.com/nefarius/ViGEm.NET), [Nefarius.ViGEm.Client 1.21.256](https://www.nuget.org/packages/Nefarius.ViGEm.Client/1.21.256) | 공식 .NET binding, 패키지 버전과 netstandard2.0. 패키지 복원과 .NET 10 native 로드는 미실행 |
| S3 | [ViGEmClient SDK](https://github.com/nefarius/ViGEmClient) | MIT 공개 SDK, thread-safety 제약, 생성·갱신·해제·rumble 콜백의 구조 |
| S4 | [Microsoft XINPUT_GAMEPAD](https://learn.microsoft.com/en-us/windows/win32/api/xinput/ns-xinput-xinput_gamepad) | 스틱·트리거 크기와 범위, 버튼 비트. 코드의 명시적 구조체와 매핑 근거 |
| S5 | [Microsoft XInput과 DirectInput 비교](https://learn.microsoft.com/en-us/windows/win32/xinput/xinput-and-directinput) | 독립 트리거 시험에 XInput을 사용해야 하는 이유 |
| S6 | [EA 2026 Season Pack 공식 발표](https://www.ea.com/en/games/f1/f1-25/news/f1-25-2026-season-pack-official-reveal) | 2026 시즌 대상 상품·PC 출시. 게임 구매/실행은 하지 않음 |
| S7 | [EA F1 25: 2026 Season Pack UDP 게시물](https://forums.ea.com/blog/f1-games-game-info-hub-en/ea-sports%E2%84%A2-f1%C2%AE25-2026-season-pack-udp-specification/12187347) | 2025/2026 UDP 선택 안내와 공식 첨부 위치. 2026 구조체 본문은 재조회 제한으로 완전 대조 못 함 |
| S8 | [EA Data Output from F1 25 v3 PDF](https://forums.ea.com/t5/s/tghpe58374/attachments/tghpe58374/f1-games-game-info-hub-en/61/4/Data%20Output%20from%20F1%2025%20v3.pdf) | 실제 본문 확인: 헤더, Motion Ex 최소 필드, assist 설정의 의미. 참조 파서는 전체 게임 어댑터가 아님 |
| S9 | [EA F1 25 지원 컨트롤러](https://help.ea.com/en/articles/f1/f1-25/controllers-wheels-and-vr-support/) | Xbox 패드 지원과 Javelin 연결 문제 안내. 가상 패드 인증/호환 보증은 아님 |
| S10 | [Android Sensors Overview](https://developer.android.com/develop/sensors-and-location/sensors/sensors_overview) | 요청 주기·실측, 좌표계, 센서 제한·foreground 제약 |
| S11 | [Android Position Sensors](https://developer.android.com/develop/sensors-and-location/sensors/sensors_position) | 게임 회전 벡터의 자력계 비사용과 드리프트 가능성 |
| S12 | [Android Custom Haptics](https://developer.android.com/develop/ui/views/haptics/custom-haptic-effects) | 진폭·primitive 지원 검사와 fallback. 본 패키지의 패턴·우선순위는 자체 설계 |
| S13 | [Microsoft Virtual HID Framework](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/virtual-hid-framework--vhf-) | HID 소스 드라이버 개발 프레임워크의 성격. ViGEm 호환 대체품이라는 근거는 없음 |
| S14 | [.NET 공식 지원 정책](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) | .NET 10 LTS 선택 근거. 정확한 설치 SDK는 구현 PC에서 고정 |
| S15 | [Android AGP 9.3 릴리스 노트](https://developer.android.com/build/releases/agp-9-3-0-release-notes?hl=en), [AGP 호환표](https://developer.android.com/build/releases/about-agp) | API 37, Gradle 9.5.0, JDK 17 지원 표와 9.3.2 항목. 실제 빌드는 미실행 |
| S16 | [AGP 내장 Kotlin 안내](https://developer.android.com/build/migrate-to-built-in-kotlin) | AGP 내장 Kotlin 경로 확인. native 프로젝트의 실제 해결 버전은 후속 기록 |
| S17 | [Android Local Network Permission](https://developer.android.com/privacy-and-security/local-network-permission) | Android 17/target 37의 로컬 네트워크 권한과 거부 처리 필요성 |
| S18 | [Kunos 원작 AC Shared Memory Reference](https://www.assettocorsa.net/forum/index.php?threads/shared-memory-reference.3352/) | 원본 URL 조회 실패. 현행 필드/packing/단위 확인 근거로 사용하지 않음. 구현 시 원문 또는 설치 SDK 확보 필요 |

## 문헌으로 검증하지 않은 자체 설계값

PWR1 포맷, 120Hz 송신, 150ms 입력 timeout, 100ms freshness/진동 lease, 300ms 중립 대기, ±90도 조향, 필터 8ms, 페달 72dp, 진동 패턴·우선순위, latency/accuracy 합격 목표는 **개발 시작값**입니다. Fold5·게임이 그 성능을 제공한다고 설명하는 수치가 아닙니다.

슬립 검출 임계값은 아직 정하지 않았습니다. `profiles/defaults.json`의 null은 “0”이 아니라 “검증 전 비활성”입니다. 실제 게임의 필드 의미와 캡처를 확인한 뒤 근거와 함께 프로필을 버전화합니다.

## 조회 제한을 처리한 방식

EA 2026 첨부는 공식 게시 위치를 확인했으나 본문 재조회에서 429/403 등 제한이 발생했습니다. 접근 제한을 우회하지 않고, 읽을 수 있었던 공식 2025 PDF와 공식 형식 선택 안내를 이용해 MVP 범위를 정했습니다. 2026 전용 파서의 전체 레이아웃은 검증 완료로 표시하지 않습니다.

원작 AC의 공식 원문도 조회하지 못했으므로 공유메모리 어댑터는 후속 검증 대상으로 남겼습니다. 커뮤니티의 유사 구조체나 ACC/EVO 레이아웃을 원작의 확정 명세로 대신하지 않습니다.
