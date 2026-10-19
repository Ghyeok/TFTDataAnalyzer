# 현재 Task6에 기획 A/B 적용 결과

2026-10-19. 기획 A는 실제 Antigravity, 기획 B는 GPT로 독립 검수했다. 동일 기준 commit `ab63a3c`와 입력 digest `c7704c8a4437428da3f63a36fcc2bef69b58302d47c6616c77eb0bb2fc7e1a83`를 사용했다. root가 결과를 통합했다. 제품 완료·QA 통과는 아니다.

## 실제 실행과 권한 한계

- A: agy1.4.2, 요청/시작 이벤트 `gemini-4-argon-medium`, effort medium. 상수 시험이 아니라 복사한 기획4개(22,496bytes)를 실제 전달했다. SUCCESS·exit0, 도구 시도0, CLI 보고 입력17,775/출력3,627/합계21,402 tokens, 응답50.421초·전체57.353초. 별도 서버의 모델 식별은 미확인으로 두며 모델의 자기소개를 증거로 삼지 않았다.
- A 원문은 [review.json](outputs/review.json), 실행은 [runtime.json](outputs/runtime.json), 입력 원본 경로/해시는 [manifest](manifest.json)에 있다. 실제 A 판정은 revise다.
- B: 실행 metadata는 `gpt-6.1-sol` / `high`다. 같은 manifest와 inputs4개만 검수했다. 공개 응답은 [GPT B 기록](outputs/gpt-planning-b.md), 실행값·사용량은 [runtime 기록](outputs/gpt-b-runtime.json)에 보존한다. 입력211,857(캐시188,544 포함)/출력2,297/합계214,154 tokens이며 reasoning701은 출력에 포함되어 중복 합산하지 않는다. 에이전트의 자기소개를 모델 실행 증거로 삼지 않았다.
- SDK 도구 목록·정책의 오프라인 검사는 통과했으나 별도 API 키가 필요해 요청 전 중단했다. CLI hook 시험은 Windows 실행/UTF-8 오류와 hook 미확인으로 차단을 증명하지 못했다. 두 실패를 숨기거나 SDK가 실제 검수했다고 기록하지 않는다.
- 사용자 요청에 따라 비밀값 없는 기획 문서만 CLI plan 모드의 고유 임시 세션에 전달했다. 실제 접근 차단은 미검증이다. 도구 시도0과 보호 파일 보존은 관측 증거이며 강제 격리를 증명하지 않는다. 일반 자동화·예약은 활성화하지 않았다. 기존 사용자 agy 세션과 Hermes 중단 상태를 보존한다.

## 통합 판단

1. **공통 P1 수용:** 기본 미분류 증강 차단, 정책 통과 행의 표본 부족, 신뢰 분류를 주입한 합성 비율·Wilson 표시를 분리한다. 실제 분류 공급자를 새로 구현하거나 Unknown을 Standard로 취급하지 않는다. 기술 완료와 실데이터 유용성을 구분해 문서화한다.
2. **A P2 수용:** 24시간 내 HTTP 생략 시 저장한 lastSeenVersion으로 안내를 복원하고, 수동 가져오기 성공 뒤 추가 HTTP 없이 안내를 갱신·해제하는 검증을 명시한다. 기존 시작 확인/수동 안내 범위의 보완이다.
3. **A P2 수용·이번 재개 점검:** 보호 baseline1,386개 변경0, `Assets/TftAnalyzerE2E_*` 폴더0을 확인했다. 이후 JsonLoadSettings namespace 오류를 수정하고 실제 컴파일 완료·실패 없음까지 확인했다. 첫 집중 실행은52개 중44통과/8실패로 제품 GREEN은 아직 아니다. 오래된 네이티브/창 ID는 정리 대상으로 재사용하지 않는다.
4. **후속 위험 기록:** 실데이터 증강 분류 공급과 집계 입도 개선은 향후 별도 기획이다. n>=100 달성 확률을 측정하지 않았으므로 단정하지 않는다. 429 수동 재시도, Editor 연구/CSV 허용과 Player build 차단, 외부 등록 승인 미확인을 유지한다.

보완은 [Task6 정제 계획](../../../Assets/Plan/Workflow/Cycles/2026-10-19-startup-e2e/refined/ImplementationPlan.md)에 반영했다. 이후 구현자에게 현재 컴파일 오류와 위 완료 조건을 전달하고 root의 실제 GREEN→독립 검수→전체/수동 검증→QA를 이어간다. 기존 전체 목표 요청에서 승인된 Task6을 수행하며 제품 새 범위는 추가하지 않는다.

## 후속 적용 증거

이후 주입 아이콘 경로 검증·캐시 복원 알림·CLI 응답 형식/로컬 경로 경계를 보완하고 독립 코드 재검수에서 지적 사항을 종결했다. 실제 파이프라인 E2E와 수동 캡처, 전체 EditMode615개 중614통과/0실패/호스트 링크 권한1제외, 공개 버전 명시1통과까지 확인했다. 별도 QA에서 실제 새 EditorWindow 재생성 검증 누락을 찾아 회귀를 보완 중이다. 최종 실행과 QA 상태는 [개발 보고](../../../Assets/Plan/Workflow/Cycles/2026-10-19-startup-e2e/refined/DevelopmentReport.md)와 체크포인트를 따른다.

일반 CLI 진입점의 추가 검증은 [오프라인 기록](outputs/entry-offline-validation.json)에 있다. 실제 A 재호출 없이 가짜 CLI 이벤트로 결과 형식과 로컬 control/output 경계를 확인했다. 초기 임시 폴더 권한 실패 뒤 승인 검토가 복구되어, 정확한 소유 디렉터리5개가 링크가 아니고 비어 있을 때만 삭제했다. [정리 결과](outputs/offline-cleanup.json)의 잔류는0개다. 초기 실패를 실제 A 실패나 Unity 창 잔류로 바꾸어 설명하지 않는다.
