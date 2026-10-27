# 기획 요청과 승인 경계

사용자의 활성 목표는 미구현 계획을 지침대로 구현하고 PR최신head를5분확인/병합후다음작업하는것이다. PR23은7922fa6에병합했고같은전체tree를확인했다. 다음후보는상위§6.6과nested LocalizedText저장의표현차이다. root가기획초안을작성했으며제품코드/새시험/Unity실행은하지않았다. A/B독립검토후통합계획의사용자명시승인을기다린다.

공통입력과A실제request는 AntigravityCollab/tasks/2026-10-27-localization-planning/ 에동결한다. 기획 준비 당시에는 전송/검수 결과가 없었다. GPT B도같은두문서만받고A응답은보지않는다. 기존담당재사용·신규생성/재위임0,모델실행값은실제반환후기록한다.


실제 A요청/응답/runtime과 실제 B요청/응답은 [전달결과](../../../../../../AntigravityCollab/tasks/2026-10-27-localization-planning/result.md)에연결한다. 두요청문서는에이전트에전달한공개prompt그대로이며숨은추론/비밀값은저장하지않는다. A외부전송의거절→사용자구체승인→동일경로성공과B독립pass를[통합검토](../refined/PlanningReview.md)에구분했다. 구현승인은아직받지않았다.

## 실제 A 지시문

```text
# 기획 A — Antigravity

제공된 inputs 두 문서만 읽는 독립 기획 검수다. 제품 범위·사용자 가치·상위/상세 계약 정합성·기존팩 보존과 실패 영향·승인범위를 검토한다. 새로운 기능을 임의로 추가하거나 현지화 동작이 전부 미구현이라고 표현하지 않는다. 원본저장소/네트워크/Unity/비밀파일열람·쓰기·도구호출·재위임금지, 상대B의견은보지않는다. 계약·검증누락 중 승인전필수항목만 최대6건(reference/impact/action/inScope)으로보고한다. 과거pass를새구현증거로쓰지않고실제모델/추론/usage미확인은미확인으로남긴다. 한국어로응답하고제공schema의taskId/inputDigest를그대로사용한다.
```

실제A입력은 위 지시문, taskId/inputDigest JSON, manifest순서의 두 동결 inputs를 기존helper가 합친 것이다. [manifest](../../../../../../AntigravityCollab/tasks/2026-10-27-localization-planning/manifest.json), [기획입력](../../../../../../AntigravityCollab/tasks/2026-10-27-localization-planning/inputs/implementation-plan.md), [소스입력](../../../../../../AntigravityCollab/tasks/2026-10-27-localization-planning/inputs/source-contract.md)으로 원문을 보존한다.

## 실제 B 지시문

```text
기존 담당을 기획 B(GPT)로 재사용합니다. 새 현지화 문자열 분리 저장 계획의 독립 기술 검수입니다. 읽을 입력은 AntigravityCollab/tasks/2026-10-27-localization-planning/inputs/implementation-plan.md 및 source-contract.md 두 파일로 제한합니다. 기준 main7922fa61cc0d5fe99460ffe9deea5752c7d6089f, taskId=2026-10-27-localization-planning, inputDigest=eadd8516c28e796367d36944565fcc7f5e227caf38854779f9a7f408af732012. A 응답/원본repo/Unity/네트워크/환경변수/키문서/사용자Volume 열람·쓰기·재위임 금지. 기존 한영 표시가 이미 있다는 점과 저장표만 분리한다는 선택, schema5 strict validation·legacy1~4 byte/hash/source/origin 보존·old4 구조화 projection·actual adapter/writer/loader 연결·fallback flag·객관적TDD 조건을 검토하세요. 승인 전에 고쳐야 할 계약 결함만 최대6개, severity(P1/P2/P3),reference,impact,action,inScope 필드로 구체적으로 보고하세요. 이 문서만으로 정할 수 없는 점은 unverified에 적고 실제 모델·추론·usage는 확인 불가면 미확인으로 기록합니다. JSON {taskId,inputDigest,verdict(pass|revise|blocked),findings:[],unverified:[]}만 반환하세요.
```
