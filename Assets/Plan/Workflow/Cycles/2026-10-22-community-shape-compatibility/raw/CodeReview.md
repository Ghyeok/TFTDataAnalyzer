# 코드검수 요청·공개 응답

## 최초 요청

승인된 다음 cycle의 읽기 전용 코드검수를 재사용 요청합니다. 신규 agent/재위임/쓰기/Unity/네트워크/금지파일 docs/riot-api-key.md 본문/프로세스명령줄 열람 금지. 기준main5a67afb679bd9c2e91cf71f3bc83b2ce80622bcc(PR21 merged), 현재branch codex/community-shape-compatibility. 계획 Assets/Plan/Workflow/Cycles/2026-10-22-community-shape-compatibility/refined/ImplementationPlan.md (사용자 명시 승인됨). 변경제품3파일 CommunityDragonAdapter, 새 CommunityDragonParseResult, StaticCatalogPipeline; 새Tests2/창작fixture2. 정확7개 경로+SHA는 Library/TftAnalyzerValidation/community-shape-review-source-freeze.json에 있습니다. 해당범위만 현재diff/필요기존계약과 대조. TDD증거 community-shape-task1-red.json 4/4fail→task1-green4/4, task2-red8중4fail→task2-green8/8, boundary-tests22중20pass2fail(SetName손실,손상composition 비구조예외)→community-shape-ingestion-green.json 전체관련97/97. 전체suite는 아직미실행. 글로벌 root.items는 같은요청 officialcatalog 전체patch/profile/validator를 확인한 종류별 ID교집합만; selected-set명시적소속으로prefix추측제거; 같은locale유일이름만stableID정규화; cost범위제외/손상오류구분; source진단을최종ValidationReport에연결; immutable팩/구schema/증강분류차단유지. 필수 실제결함 최대3건과 pass/revise, 검증한hash/한계/실제확인가능runtime만 간결히. 선호·무관한최적화는 필수로 만들지 마세요. 전체재검증/새 PNG 강요는 실제변경·요구 증거가 있을때만. 계획·동결·실행근거를 검토하되 사용자파일은열지말아주세요.

## 최초 공개 응답

**Revise — 필수 수정 2건(P2)**입니다.

1. [CommunityDragonAdapter.cs:288](C:/Unity/TFTDataAnalyzer/Assets/TFTAnalyzer/Editor/Ingestion/CommunityDragon/CommunityDragonAdapter.cs:288): `CatalogIdentity.Equals`는 `SetNumber`를 비교하지 않습니다. 공식 fixture의 SetNumber만 17로 변경해도 요청 18의 글로벌 범위를 승인합니다. **명시적 SetNumber 비교와 거절 회귀**가 필요합니다.

2. [CommunityDragonAdapter.cs:241](C:/Unity/TFTDataAnalyzer/Assets/TFTAnalyzer/Editor/Ingestion/CommunityDragon/CommunityDragonAdapter.cs:241): 영어 trait의 이름을 제거하고 영어 unit의 `traits=[""]`로 설정하면, 빈 참조가 해당 trait ID로 정규화됩니다. 한국어 이름이 남아 있어 최종 validator도 통과할 수 있습니다. **이름 조회 전 공백 참조 거절과 회귀**가 필요합니다.

7개 동결 해시 일치와 관련 **97/97 GREEN**을 확인했습니다. 위 두 지적은 정적 경로 확인이며 직접 실행하지 않았습니다. root가 조사 중인 정수 overflow는 별도로 유지합니다. 전체 suite·실제 모델/추론은 미확인입니다.

## 후속 요청

같은 cycle 지적 한정 후속 검수 요청. 검수P2 두건 + root number overflow를 실제 회귀로 재현: community-shape-review-red.json 27중24pass/3fail(빈참조정규화,official SetNumber17승인,OverflowException). code에 manifest.Patch.SetNumber 명시비교, whitespace trait ref 사전거절, number int.TryParse를 추가. community-shape-review-green.json 27/27pass. 추가 profile불일치와 빈지원배열 차단도 통과. 기존7경로 final SHA는 Library/TftAnalyzerValidation/community-shape-review-source-freeze.json에갱신했고 이전값은 community-shape-initial-review-freeze.json. 변경은 adapter/test2개뿐; 나머지5같음. 원래 권한제한/읽기전용/금지파일보존/추가위임금지 유지. 지적해소와관련새결함만 판단해 pass/revise 간결응답. 전체suite는 수정확정후1회 예정이며 아직미실행.

## 후속 공개 응답

**후속 검수 pass, 지적 3건 해소, 추가 필수 수정 0건**입니다.

- SetNumber 명시 비교, 공백 trait 참조 거절, 정수 범위 사전 검사를 확인했습니다.
- 실제 회귀 **RED 24통과·3실패 → GREEN 27/27**을 확인했습니다.
- 최종 7개 해시가 일치하며, 변경은 adapter와 해당 테스트 2개 파일뿐입니다.

전체 suite는 아직 미실행이며 이번 통과 판정에 포함하지 않습니다.


## ID 경고 계약 delta 요청

같은 cycle의 마지막 잔여 계약 한정 delta 검수 요청입니다. 새agent/재위임/쓰기/Unity/네트워크/금지파일열람금지 유지. 승인검토 사용량 오류 후 같은 require_escalated 경로의 1회 확인이 오늘 정상 통과했고 현재 EditorPID32384/ready라 실제 검증을 재개했습니다. 계획 계약2의 비용제외 기물ID경고가 개수만 남겨지던 누락을 root가 찾았습니다. community-shape-warning-id-red.json 1/1fail, bounded표본추가시험과함께 community-shape-warning-id-bounds-red.json2/2fail를 확인했습니다. adapter에서 cost제외ID 표본 최대5개, 각64UTF16자(경계high-surrogate절단방지·control문자공백), 전체 제외count와 생략건수, 진단Issue.Message로 전달합니다. 현재 community-shape-final-ingestion-green.json104/104pass, 그중새29전부pass. final SHA7개는 Library/TftAnalyzerValidation/community-shape-final-source-freeze.json. 기존27검수동결과 다른파일은adapter/CommunityDragonShapeTests2개뿐. 같은해당delta의 실제계약해소·ID목록유실/길이/개수/원본보존만 확인해 pass/revise 필수지적 간결응답 바랍니다. 전체suite는이후1회예정이라아직미실행입니다. 새로운전체조사역할을추가하지말아주세요.

## ID 경고 delta 공개 응답

**해당 delta 검수 pass, 필수 수정 0건**입니다.

- 제외 ID를 최대 5개 보존하고, 전체 제외 수와 표본 외 생략 건수를 진단에 전달합니다.
- ID는 원문 최대 64 UTF-16자와 생략 표시로 제한하며, surrogate 경계와 제어문자를 처리합니다. 원본 ID·카탈로그 값은 변경하지 않습니다.
- RED 실패와 관련 **104/104 GREEN**을 확인했습니다.
- 최종 7개 해시가 일치하며, 이전 동결 대비 adapter·테스트 2개만 변경됐습니다.

전체 suite는 아직 미실행으로 구분합니다.
