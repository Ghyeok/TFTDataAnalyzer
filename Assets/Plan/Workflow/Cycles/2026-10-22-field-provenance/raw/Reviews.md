# 실제 검수 전달과 응답

기준 f005879. 다음은 기존 담당자에게 전달한 범위와 받은 응답이다. 신규 생성·재위임0. 경로는 C:/Unity/TFTDataAnalyzer 기준이다.

## 코드검수 최초 요청

새 승인 사이클의 기존 코드검수 담당자로 재사용합니다. 재위임/쓰기/Unity/네트워크 금지. 기준 f005879, branch codex/field-provenance. 사용자 call_d0d2d56f0c514f2eb73afc534b641d3a 명시 승인 수신. 계획 Assets/Plan/Workflow/Cycles/2026-10-22-field-provenance/refined/ImplementationPlan.md 와 현재 product diff의 동결목록 Library/TftAnalyzerValidation/field-origin-product-freeze.json(13파일)만 우선 검수. 테스트는 새 FieldOrigin*4파일 및 schema1/2-field-origin-compat.json2개, 변경범위의 기존 관련 테스트만 필요시. 사용자 Assets/DefaultVolumeProfile.asset 및 docs/riot-api-key.md는 읽지 마세요. schema3 실제 ko/en파서→병합결정→아이콘/정규화→검증된 게시/loader→UI, 구1/2바이트/해시/immutable재사용과 실제저장Catalog반환을 검토. 원본부재cost0 vs 실제cost0은 둘다거절하되 tokenorigin차이, effects0필수. /componentIds는 기존canonical명. legacy외부provider는 metadata없으면 schema2유지, 실제Network어댑터는3공급. 현재 FieldOrigin22/22, Ingestion69/69. 추가 loader/optional 테스트와 소유창 synthetic캡처는 root가 진행중이므로 아직 최종QA완료 아님. 필수 버그와 최소수정·구체재현만 반환, 원래변경과무관한전체감사금지. 실제모델/추론확인불가시미확인. 동결제품 변경 필요시 발견사항으로 알려주세요.

## 코드검수 최초 응답

**필수 수정 1건(P2)**입니다.

ParsedFieldOrigins.cs:23의 접미사 판별이 효과 키도 locale 필드로 오인합니다.

- 재현: 영어 primary 문서에 `effects: {"koKr": 0}`을 넣으면 값은 영어 문서에서 읽지만 `/effects/koKr` 출처는 한국어 문서로 기록됩니다. ko-only record의 `effects.enUs`도 반대로 잘못 기록됩니다.
- 영향: 두 문서가 record/manifest sources에 있으므로 검증을 통과하며, 잘못된 출처가 저장·표시됩니다.
- 최소 수정: `/name/koKr`, `/description/koKr`, `/name/enUs`, `/description/enUs` 네 포인터만 정확히 판별하고 나머지는 primary 출처를 사용하세요. 두 실제 어댑터의 해당 효과 키 회귀가 필요합니다.

나머지 검토 범위에서 추가 필수 결함은 발견하지 못했습니다. 동결 13개 해시는 일치했습니다. 이 지적은 정적 경로 확인이며 Unity 재현은 실행하지 않았고, 진행 중인 추가 검증·캡처·최종 QA 통과를 주장하지 않습니다.

## 코드검수 후속 요청

지적한 locale 효과키 오류를 실제 DataDragon 어댑터의 4case에서 RED(2실패/2통과)로 재현 후 정확한4pointer 비교로 수정했습니다. 같은case에 CommunityDragon 실제파서(en-primary/ko-only)를 추가했고 FieldOrigin28/28 GREEN: Library/TftAnalyzerValidation/field-origin-review-effect-{red,green}.json. 후속은 해당지적 closure와 추가helper진단 delta만 확인해주세요. 동결13중 변경2: ParsedFieldOrigins.cs 및 AgentScripts/TftAnalyzerCatalogVisualValidation.cs. 최종13해시 field-origin-final-product-freeze.json. helper첫실행은 InvalidOperationException/캡처0이었으나 기존9창/native1/focus보존·owned0; 제한상태진단 추가한 1회 재실행 pass4PNG(root직접봄), 두 예외probe모두pass/owned0. 초기원인은 확정못해 기록유지, 해결주장안함. 진단은 소유 창 가짜자료의 Schema/DetailLength/flags/rows만기록, 알려진상수만오류이유공개. 최종전체검증은지금진행예정. 원본프로젝트/키문서추가열람·쓰기·Unity·네트워크·재위임금지. 새전체감사말고현재delta 필수문제만.

## 코드검수 후속 응답

**후속 검수 pass, 기존 P2 해소, 추가 필수 수정 0건**입니다.

- 정확한 네 locale 포인터 비교로 효과 키 오인을 제거했습니다. 실제 두 어댑터의 en-primary/ko-only 회귀와 **28/28 GREEN**을 확인했습니다.
- helper 진단은 합성 자료의 상태값과 허용된 오류 상수로 제한됩니다.
- 최종 13개 해시가 일치하며, 이전 동결 대비 변경은 지정된 2개 파일뿐입니다.
- 정상 캡처·두 예외 probe JSON 모두 통과: 기존 창 9개·프레임 1개 보존, 소유 창 0, 포커스 복원.

최초 캡처 실패 원인은 여전히 미확정입니다. 전체 검증은 별도 진행 단계이며, PNG 육안 확인은 root 수행 결과로 구분합니다.

## 최종 QA 요청

기존 QA 담당자를 새 승인 field-provenance 사이클에 재사용합니다. 읽기전용, 쓰기/Unity실행/네트워크/재위임금지. 사용자 키문서 docs/riot-api-key.md 및 DefaultVolumeProfile.asset 내용은 읽지 마세요. 승인계획 Assets/Plan/Workflow/Cycles/2026-10-22-field-provenance/refined/ImplementationPlan.md Task1–3 대비 실제diff/evidence 확인. 기준f005879, 현재19파일 freeze Library/TftAnalyzerValidation/field-origin-full-source-freeze.json과 final-evidence.json. 전체 field-origin-full-tests.json=796중794통과/실패0/호스트symlink2제외, 신규FieldOrigin31통과. protected1399변경0, source19변경0, 전후9EditorWindow/native1/focus동일, Console오류0경고0. 정상·표시전/후probe3보고서도passed/owned0. 실제4PNG catalog-window-{940,500}-field-origins{,-legacy}.png를 직접 view_image로 확인해주세요. full 직전 추가한 테스트는 mixed전체pipeline→loader/해시재계산출처누락차단, 구schema2보완재사용, metadata만변경불변충돌을 검증합니다. 기존code-review P2 locale-effect키는 실제RED2/4→두어댑터GREEN28/28후속pass. helper첫실행 InvalidOperationException 캡처0, 정리는pass; 제한진단추가후1회pass4PNG. 초기원인미확정은 raw/Execution.md와최종증거에그대로남김, root해결주장안함. API실행0. 최종pass/revise와정확한필수결함/미실행한계만 반환, 새전체감사금지. 실제모델/추론/사용량확인불가시미확인.

## 최종 QA 응답

**pass — 필수 결함 0건.**

승인 Task1–3과 실제 변경·시험을 대조했습니다.

- 동결19파일 SHA256 직접 재계산: 변경0.
- 전체796건: 794통과·실패0·호스트 symlink 제외2건, 83.32초. 신규 FieldOrigin31건 모두 통과.
- 실제 어댑터의 locale/effect 오기록은 2건 실패 재현 후 28/28 통과로 해소됐습니다.
- mixed pipeline→loader, 출처 누락 차단, 구 schema1/2 재사용, 출처만 변경한 불변 충돌 시험을 확인했습니다.
- PNG4장을 직접 확인했습니다. 신규 출처의 포인터·공식/보완·URL·SHA와 구 자료의 정보 부재가 구분되며 작은 폭에서도 읽힙니다.
- 정상·표시 전후 예외는 소유 창0과 기존 창·프레임·포커스 보존입니다. 보호1399변경0·Console 오류/경고0은 최종 증거와 일치합니다.

최초 캡처 실패는 정리 성공과 구분했으며, 후속 성공을 원인 해결 증거로 확대하지 않았습니다. 최초 원인은 여전히 미확정입니다.

쓰기·Unity·네트워크·재위임·금지 파일 열람은 하지 않았습니다. 실제 모델·추론·사용량은 미확인입니다. 실제 API·분류 등록·n100·PR 병합·서비스 전체 완료는 이 판정 밖입니다.
