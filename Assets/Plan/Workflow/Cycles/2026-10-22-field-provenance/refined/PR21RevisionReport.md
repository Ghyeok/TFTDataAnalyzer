# PR21 검수 보완

상태: 진행 중

최초 head `3619a5f4c42b1d74c42359e879c5d6640f98a6d7`, [PR21](https://github.com/Ghyeok/TFTDataAnalyzer/pull/21). CodeRabbit 검토가 진행 중인 동안 UTC04:14:30/04:20:06/04:25:24/04:30:44에 직접 확인했다. 마지막 확인에서 CodeRabbit SUCCESS·별도CI0·CLEAN이었지만 미해결6건이 있어 병합하지 않았다. Hermes·외부 예약은 사용하지 않았다. 사용 한도 중단 뒤 UTC08:50:07의 재개 확인에서도 head와 미해결6건이 같았다.

## 실제 지적과 반영

| thread ID 끝부분 | 지적 | 반영·판단 |
|---|---|---|
| C9bn | PR20 계획 완료와 마지막 TODO 불일치 | 이미 병합한 PR20의 해당 TODO만 완료로 표시 |
| C9bq | FinalDocumentation 상태값 혼합 | Workflow §5 허용값 ‘승인 대기’를 별도로 명시. 사용자 최종 결과 승인 전이므로 제안된 ‘완료’를 그대로 적용하지 않음 |
| C9bs | ImplementationPlan 상태값 혼합 | PR 병합 TODO가 남아 있으므로 허용값 ‘진행 중’을 별도 명시 |
| C9bu | PlanningReview 지정 상태 누락 | 기획 검토와 명시 승인 완료를 근거로 ‘상태: 완료’ 추가 |
| C9bw | 출처 반복 출력이4096자에서 잘려 뒤쪽 pointer 소실 | 효과40개로 실제 RED1 확인 후 공식/보완·문서명·URL·SHA ValueTuple별로 묶어 출처를1회 출력. pointer와아이콘계보안내 유지, GREEN32/32 |
| C9by | Windows autocrlf 체크아웃의 골든 바이트 비교 위험 | root.gitattributes에 골든2파일만 text eol=lf. core.autocrlf=true/core.eol=crlf의 실제 checkout-index에서도 CRLF0·기존SHA 일치 |

별도 nit의 DataDragon 반복 SHA 계산도 이번 출처 추가 호출과 연관돼 반영했다. 4개 Parse 루프 밖에서 ko/en SourceStamp를 한 번 계산해 기존 record.Sources와 FieldOrigins가 공유한다. 선택한 값·순서·해시 의미는 바꾸지 않는다. 새 설정/프레임워크나 무관한 최적화는 추가하지 않았다.

Docstring coverage0%/80% 경고는 CodeRabbit의 비차단 warning으로 남긴다. 같은 결과를 모든 경고 통과로 표현하지 않으며 unsupported 함수47개가 별도 보고됐다는 사실과 구분한다.

## 보완 검증

- `pr21-ui-clipping-red.json`:1개실패. `pr21-review-green.json`:출처32/32 통과.
- `pr21-fixture-checkout.json`:원본 f005879 직렬화로 생성한 schema1/2 골든의 SHA·LF바이트 유지. 기존 다른 JSON의 줄바꿈 규칙은 바꾸지 않았다.
- `pr21-review-source-freeze.json`:20파일. 원래 동결19개에서 제품2(DataDragonAdapter/CatalogBrowserViewModel)와 UI시험1개만 바뀌고 .gitattributes가 추가됐다.
- `pr21-review-full-tests.json`:797개 중795통과·실패0·호스트symlink2제외,81.56초. 새 출처32개 전부 통과. 실제 제품 변경 때문에 전체 검증을 다시 했으며 같은 소스로 무의미하게 반복하지 않았다.
- 새940/500×출처/legacy4PNG를 root가 직접 확인했다. 정상 캡처 Passed/owned0, 기존9창/native1·포커스보존. helper·정리 코드는 불변이므로 표시전/후 실패 probe는 이전 실행 증거의 적용 범위를 유지하며 다시 실행하지 않았다. 이전 PNG/보고서는 Library의 pr21-before-grouped-ui에 보존했다.
- `pr21-review-final-evidence.json`:동결20·보호1399변경0, 현재창/native/focus는before와동일. 즉시 사후 확인은 UTC04:37의 자동 승인 검토 사용 한도로 **미실행**이었다. 안내된 복구 시각 이후 UTC08:50에 같은 Editor PID29408의 지연된 상태를 확인했으므로 즉시 post-test 샘플로 표시하지 않는다.
- 지연 확인의 Console오류2/경고0은 `pr21-console-sanitized.json`에서 Unity.Pipeline.BasePipelineServer의 GET /api/commands 전송 연결 종료·headers already sent 오류로 확인했다. TFTAnalyzer/CS compile 언급은 없다. 로그 원문·인증값을 출력하지 않고 경로·URL·긴 토큰을 Editor 내부에서 가린 요약만 기록했다. 오류를 지우거나 도구 오류의 원인이 해결됐다고 주장하지 않는다. 원래 전체 검증796/794P의 즉시 Console0 증거와 구분한다.

기존 코드검수 담당의 현재 delta 후속 응답: “현재 delta 검수 pass, 필수 수정0건.” 출처 ValueTuple 묶음·아이콘 안내·RED/GREEN32/32, 공유 스탬프의 값·ko/en 순서, 상태값·LF범위와 실제checkout, 동결20 SHA를 확인했다. 추가 전체·PNG는 root 증거와 분리했다. 최종 QA 후속·push·최신head 재검수·thread 해소·병합은 이 문서만으로 완료 판정하지 않는다.

최종 QA 후속 응답: “pass — 현재 보완분의 필수 수정0건.” 새PNG4장·RED/GREEN32/32·전체797/795P0F/2호스트제외·동결20직접대조·LF범위·스탬프값을 확인했다. 보호1399·창/native/focus는 동일Editor의지연샘플이며 즉시종료증거로보지않았다. Console2건·최초화면실패의원인은해결로판정하지않고, 변경없는helper의이전예외probe증거범위만유지했다. 쓰기·Unity·네트워크·재위임·금지파일열람0, 실제모델·추론·사용량미확인이다.
