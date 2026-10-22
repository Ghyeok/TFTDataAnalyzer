# 필드별 출처 기록 기획 검토 결과

2026-10-22. 기준 f0058799355cd2754f193eff38d7dd415cd700e6. 최초 입력 2개 합계 15,324bytes, inputDigest `9c42ac8193ca0a0121296f83b6f753f53450a9eaa1856ce0af624d370925315d`. [manifest](manifest.json)에 개별 SHA256을 기록했다. 원래 동결 입력은 보완 후에도 불변이다.

- 기획 A: [요청](request.md), [구조화 응답](outputs/review.json), [실행 기록](outputs/runtime.json). agy1.4.4, 실제 initModel gemini-4-argon-medium, 요청 effort medium. SUCCESS/exit0, pass, 필수 지적0. 10,712 input +1,479 output =12,191 total tokens, thinking1,366은 별도 보고 필드다. 응답20.888초/총29.934초. 도구 시도0이나 강제 접근 제한은 미검증이다.
- 기획 B: 기존 `/root/analysis_plan_technical` 재사용. [요청](request-gpt-b.md), [응답](outputs/gpt-planning-b.md). 동일 2개 입력의 개별 SHA256/합계 일치 확인, digest 결합 산식의 독립 재계산은 미확인. 출처 완전성 조건의 모호함으로 revise/P2 1건.
- root 보완: [추가 계약](inputs/completeness-contract.md), [추가 manifest](followup-manifest.json). 비용·효과0·optional 부재를 구분하는 고정 필수 pointer 규칙과 adapter→serializer→validator 시험을 정했다. 최초 A 범위나 기존 값 규칙을 확장하지 않아 A는 재호출하지 않았다.
- B 후속: [제한 요청](request-gpt-b-followup.md), [응답](outputs/gpt-planning-b-followup.md). 보완 입력 3,910bytes/SHA256 확인 후 pass/P2 해소. 새 에이전트 생성0, 재위임0. GPT의 실제 모델·추론·토큰·실행 시간은 확인하지 못했으므로 추정해 기재하지 않는다. 중요 호환 설계에 High 수준을 권고했으나 실제 적용을 확인한 기록은 아니다.

통합은 [정제 계획](../../../Assets/Plan/Workflow/Cycles/2026-10-22-field-provenance/refined/ImplementationPlan.md)과 [PlanningReview](../../../Assets/Plan/Workflow/Cycles/2026-10-22-field-provenance/refined/PlanningReview.md)에 반영했다. 기획 검토는 제품 실행·기존 팩 바이트 보존·사용자 승인을 대신하지 않는다. 제품 변경0, 실제 API0, Unity 제어0. 사용자 명시 승인 전 구현하지 않는다.
