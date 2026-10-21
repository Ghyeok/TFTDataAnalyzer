# GPT 기획 B — 분석 최소 표본 수

verdict: pass
필수보완 findings: 0건.
base: 310faf6d05ed4f6e5fb4323d00a444a607b2f700(PR17 미병합 head)
inputDigest: d178aea98b1749877cd19f9d4b4aed58666dbc88d070cf2c2930bd241c4dbc88
입력2개 SHA-256·합계11646bytes가 manifest와 일치한다. digest는 제공 식별자이며 결합 산식의 독립 재계산은 미확인이다. A 응답/outputs는 읽지 않았다.

근거:
- 요청값과 실제 적용값을 구분하고 effective=max(주입 정책 하한, 요청값)로 계산하므로 UI100이 주입250을 낮추지 않는다. WithMinimumSamples는 불변 metadata를 보존하고 공용 정책을 수정하지 않으며 provider 결과와 effective를 같은 요청에 결합한다. provider Missing/Invalid에서도 기존 차단과 요청 하한을 유지하는 테스트가 명시돼 있다.
- 실제 값 변경은 유효 여부와 관계없이 세대 변경·취소·결과 제거로 연결되고, 같은 값은 멱등적이다. snapshot.MinimumSamples의 실제 적용값 기록과 요청보다 낮은 응답 거부, 늦은 완료 폐기, CSV 대화상자 이후 snapshot 참조 확인이 서로 일관된다. 필터는 재집계하지 않으며 CSV는 현재 presentation만 사용한다.
- 최소표본 입력을 busy 중에도 편집 가능하게 하면서 기존 필터/갱신/CSV busy 규칙을 유지한다. 재구성 시 VM값 보존과 옛 필드/콜백 해제, 새 창 기본100·비영속 범위가 명확하다. 설정을 바꾼 뒤 자동 갱신하지 않는 동작도 완료조건과 일치한다.
- 조합의 참가자수와 쌍의 비교 가능 로비수에 각각 같은 하한을 적용하고 정책 차단 우선순위를 유지한다. 99·199/200·int.MaxValue·주입250, metadata 유지, 취소/실패/전환, 정상 presentation→CSV, 실제 입력/UI/PNG 증거가 필요한 회귀를 포함한다. 기존 interface 구현체 이관과 순수 BuildSnapshot의 실제 기준 보존도 작업에 포함돼 있다.

unverified: 동결 문서만 검토했다. 원본 코드·Unity·컴파일·테스트·PNG·실제 API·실제 분류·실제 표본은 확인하지 않았다. 실제 모델/추론/비용은 이 실행에서 독립 확인할 수 없어 추정하지 않는다. 실제 분류등록0과 완전 보드 지문의 표본 부족 한계는 그대로다. 본 판정은 사용자 승인·PR17 병합·제품 구현/검증 완료 또는 실행 허가가 아니다.
