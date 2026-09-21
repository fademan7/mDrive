"""Run portable reference tests and write an honest, reproducible report."""
import datetime
import io
import json
import platform
from pathlib import Path
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))


class RecordingResult(unittest.TextTestResult):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.passed_ids = []

    def addSuccess(self, test):
        super().addSuccess(test)
        self.passed_ids.append(test.id())


def main():
    suite = unittest.defaultTestLoader.discover(str(ROOT / "reference"), pattern="test_*.py")
    stream = io.StringIO()
    result = unittest.TextTestRunner(stream=stream, verbosity=2, resultclass=RecordingResult).run(suite)
    success = result.wasSuccessful() and result.testsRun > 0
    report = {
        "generated_at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "environment": {"os": platform.platform(), "python": platform.python_version()},
        "scope": "portable reference model and synthetic data only",
        "status": "PASS" if success else "FAIL", "tests_run": result.testsRun,
        "passed": result.passed_ids,
        "failures": [{"test": str(t), "detail": detail} for t, detail in result.failures],
        "errors": [{"test": str(t), "detail": detail} for t, detail in result.errors],
        "skipped": [{"test": str(t), "reason": reason} for t, reason in result.skipped],
        "not_run": ["Galaxy Z Fold5 sensor/touch/haptics",
                    "F1 2026 Season Pack gameplay", "Assetto Corsa shared memory",
                    "physical end-to-end latency", "human haptic discrimination",
                    "EA 2026 native packet parser", "real telemetry replay"]
    }
    out = ROOT / "verification"
    (out / "results.json").write_text(json.dumps(report, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    (out / "unit-test-output.txt").write_text(stream.getvalue(), encoding="utf-8")
    markdown = f"""# 검증 결과

생성 시각: {report['generated_at_utc']}

**{report['status']} — Python 참조 모델 {result.testsRun}개 테스트 실행, 실패 {len(result.failures)}개, 오류 {len(result.errors)}개, 건너뜀 {len(result.skipped)}개.**

실행 환경: {platform.platform()}, Python {platform.python_version()}.

## 실제 실행한 검증

- 쿼터니언 상대 회전, 좌우 부호, 좌표계 공통 회전 불변성 300개 합성 자세.
- 가속·브레이크 동시 입력 및 4,097개 입력에서 트리거 256단계와 양자화 오차 한계 확인.
- 수동으로 명시한 기대 바이트와 통신 레이아웃 대조. 68바이트의 544개 단일 비트 변조와 모든 잘린 길이를 거부하는지 확인.
- 중복·역순·순번 순환, 인증되었지만 잘못된 값, 잘못된 세션, 오래된 challenge 확인.
- 입력 단절 150ms 모델 경계, 300ms 중립 대기와 재활성화, 앱 상태·보정 변경 시 중립 복귀.
- 진동 우선순위, 오래된 이벤트 폐기, 만료, 반복 통지가 패턴을 재시작하지 않는지 확인.
- EA F1 25 v3 문서의 Motion Ex 일부 필드 위치를 합성 패킷으로 검증. 2026 형식을 이 파서가 거부하는지 확인.

참조 코드는 OS 시간 예약, 실제 UDP 소켓, Android 센서와 진동, 가상 패드 출력을 구현하지 않습니다. 테스트 시간은 지정한 모델 시간이며, 실행 속도를 실제 입력 지연으로 해석할 수 없습니다. 통과는 Kotlin/C# 이식 후 같은 테스트의 통과를 대신하지 않습니다.

## 실행하지 못한 검증

{chr(10).join('- '+x for x in report['not_run'])}

이 목록은 현재 프로젝트에서 아직 실기 실행하지 못한 항목입니다. Windows/.NET/Android 빌드, ViGEm→XInput 관찰, localhost UDP watchdog 및 Kotlin↔C# 결과는 별도의 `artifacts/validation/GATE_RESULTS.md`에 기록했습니다. 이 Python 스크립트 자체는 그 외부 경로를 실행하거나 재검증하지 않습니다.

## 문헌 확인의 범위

ViGEmBus의 무료 공개 배포와 유지보수 종료, XInput 축 범위와 독립 트리거, Android 센서·진동·로컬 네트워크 권한, EA의 2026 시즌 상품 및 UDP 2025 선택 안내를 공식 자료로 확인했습니다. F1 25 v3 PDF 본문에서 참조 파서의 레이아웃을 대조했습니다.

2026 전용 구조체 첨부는 게시 위치를 확인했지만 본문 재조회가 429/403 등으로 제한되어 완전 대조하지 못했습니다. AC 원본 공식 공유메모리 문서도 조회하지 못했습니다. 따라서 2026 전용 파서와 AC 구조체는 검증 완료로 취급하지 않습니다. 상세 URL은 `docs/SOURCES.md`에 있습니다.

## 재현

패키지 루트에서 `python verification/run_verification.py`를 실행합니다. 외부 Python 패키지는 필요하지 않습니다. 이번 결과의 개별 테스트는 `unit-test-output.txt`, 기계 판독용 결과는 `results.json`에 있습니다.

실기 검증 절차와 합격 기준은 `docs/HARDWARE_TEST_PLAN.md`를 따릅니다. 아직 측정하지 않은 지연·정확도·게임 인식에 PASS를 부여하지 마십시오.
"""
    (out / "REPORT.md").write_text(markdown, encoding="utf-8")
    print(stream.getvalue(), end="")
    print(f"Report: {out / 'REPORT.md'}")
    return 0 if success else 1


if __name__ == "__main__":
    raise SystemExit(main())
