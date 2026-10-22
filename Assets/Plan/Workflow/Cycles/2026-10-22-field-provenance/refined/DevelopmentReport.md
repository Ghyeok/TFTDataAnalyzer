# 승인·구현·검수 실행 기록

상태: 진행 중

기존 담당자에게 전달한 실제 요청·응답은 [검수 원문](../raw/Reviews.md)에 보존한다. 최종 QA는 동결19해시·전체796개·신규31개·PNG4장·창정리·보호1399를 독립 확인해 pass/필수0으로 판정했다. 실제 API·분류 등록·n100·병합·서비스 전체 완료는 이 판정 밖이다.

2026-10-22. 기준 f0058799355cd2754f193eff38d7dd415cd700e6, branch codex/field-provenance.

## 승인과 방식

사용자는 비동기 질문 call_d0d2d56f0c514f2eb73afc534b641d3a의 ‘필드별 출처 기록’ 제안에 “이 계획대로 구현 진행 (권장)”으로 명시 승인했다. 이전 blocked는 승인 대기 이력이며 승인 후 구현으로 재개했다. root inline 구현, 기존 `/root/analysis_code_review`와 `/root/analysis_final_qa`를 재사용한다. 새 에이전트 생성·재위임0. 기획 A/B 원문·사용량은 [협업 결과](../../../../../../AntigravityCollab/tasks/2026-10-22-field-provenance-planning/result.md)에 있다.

개발 작업은 다중 모듈·예외/호환 경계라 High 수준이 적합하다고 판단했다. root·기존 GPT 담당의 실제 모델·추론·토큰은 런타임에서 확인하지 못했으므로 모델 전환·정확한 사용량을 주장하지 않는다. Antigravity를 같은 기획으로 재호출하지 않았다.

## 주요 실행 증거

- `Library/TftAnalyzerValidation/field-origin-task1-red.json`: 실제 테스트3개 모두 실패. 출처 소실·필수 출처 누락 허용·구 스키마 주입 허용을 재현했다. 첫 Editor 미연결과 잘못된 recompile_scripts 명령은 기능 RED가 아니다.
- `field-origin-task1-green.json`: 최초3/3 통과. `field-origin-task2-red.json`: 모델/골든12통과와 absent cost1통과, 실제 adapter origin 미공급3실패. 이후 `field-origin-ingestion-green.json`16/16.
- `field-origin-legacy-golden-capture.json`: git f005879의 원래 serializer를 Library 일회성 namespace에서 실행했다. 기존 팩은 읽거나 재작성하지 않고 창작 fixture2개만 생성했다. SHA256 schema1=`daf4a7a6484a18e92a96a06c8b466c4b8064517e6e72a834d5df6f5a8297827d`, schema2=`310e505051591e680d8cef43fcb3a2c492dd6f74a11e2e1bebcf7915c4ab7825`.
- `field-origin-publication-red.json`: 게시 시3→2로 내려가는 손실을 확인했다. legacy fixture의 비공식 host 때문에 거절된 별도 시험은 fixture를 공식 host로 수정했으며 네트워크 요청은0이다. `field-origin-publication-green.json`19/19.
- `field-origin-case-red.json`: 대소문자 변형 metadata 주입1실패; 대소문자와 무관하게 구 스키마 주입을 거절하도록 수정했다. `field-origin-browser-red.json`은 출처 UI 부재1실패. `field-origin-browser-green.json`22/22, `field-origin-ingestion-regression.json`69/69.
- 검수 P2: suffix만으로 locale을 판별해 `/effects/koKr`를 한국어 출처로 잘못 기록했다. `field-origin-review-effect-red.json`의 실제 DataDragon4case 중2실패로 재현, 정확한 네 locale pointer만 분기하도록 수정했다. 두 실제 어댑터의 en-primary/ko-only 회귀를 포함한 `field-origin-review-effect-green.json`28/28.
- `field-origin-catalog-visual-final.json`과 정상940/500×신규/legacy4PNG는 root가 직접 확인했다. 표시 전·후 실패 probe도 모두Passed, 기존EditorWindow9/nativeContainer1 ID 보존, 소유 창0, 포커스복원. 초기 실패의 보고서는 `field-origin-visual-first-failure.json`에 남겼고 원인은 미확정이다. 합성 상태만 기록하는 진단을 추가한 1회 재실행을 원인 해결 주장으로 바꾸지 않는다.

## 기존 코드검수 담당의 응답

최초 필수 수정1건(P2): “ParsedFieldOrigins.cs의 접미사 판별이 효과 키도 locale 필드로 오인합니다.” 영어 primary의 효과 koKr·ko-only 효과 enUs에 잘못된 문서를 기록하며 일반 참조 검증도 통과한다. 실제 두 어댑터 회귀와 정확한4pointer 비교를 요구했다. 나머지 범위에서 추가 필수 결함은 발견하지 못했고 당시 동결13개 해시가 일치했다.

후속 응답: “후속 검수 pass, 기존 P2 해소, 추가 필수 수정0건.” 두 어댑터 회귀28/28, helper의 제한 진단·허용 상수, 최종13해시와 변경2개(ParsedFieldOrigins/helper), 정상·두 예외probe JSON의 정리를 확인했다. PNG 육안은 root 수행과 구분하고 최초 실패 원인은 미확정으로 유지했다. 최종 전체 검증은 별도 단계다.

## 판단과 보존

- 기존 정규 JSON의 관계 필드는 `/componentIds`이므로 초안의 `/components`를 그 이름으로 구체화했다. 새로운 필드가 아니다.
- 기본 NetworkStaticCatalogSource의 실제 어댑터는 schema3을 공급한다. 메타데이터 없는 기존 사용자 지정 provider는 schema2로 유지하고 출처를 만들지 않는다. 기존 호환 호출을 보존하면서 실제 기본 공급 경로를 바꾸는 판단이다.
- 프로세스 상태 조사에서 명령줄의 Unity 인증값이 도구 출력에 포함됐다. 값을 이 문서·코드·협업 자료로 복사하지 않았고 이후 조회는 PID·실행 경로로 제한했다. 원문 프로세스 명령줄/Editor.log를 수집하지 않는다.
- 보호 파일1399개는 `field-origin-protected-before.json`에 현재 SHA를 기록했다. 사용자 `Assets/DefaultVolumeProfile.asset`·`docs/riot-api-key.md`는 수정/스테이징 대상에서 제외하며 키 문서 내용은 읽지 않았다. Chrome 종료·조작0.

## 최종 전체 실행

`field-origin-full-tests.json`:796개 중794통과·실패0·호스트symlink2제외,83.32초. 새 FieldOrigin31개 전부 통과. 마지막 추가한 mixed pipeline→loader·구schema2보완재사용·출처만 변경한 불변 충돌도 여기에 포함된다. `field-origin-final-evidence.json`:동결 소스19개 변경0·보호1399개 변경0·창9/native1/포커스 전후동일·Console오류0경고0. 정상·표시전후 probe3개 모두정리통과/owned0. QA·PR 최신 head 검수·병합은 별도 단계다.

PR21 CodeRabbit 보완과 최종797/795P0F/호스트2제외, 새PNG·LF체크아웃·지연Console2도구오류의 증거는 [PR21 보완 기록](PR21RevisionReport.md)을 따른다. 최초796/794P 기록을 새797 실행의 Console0 증거로 확대하지 않는다.
