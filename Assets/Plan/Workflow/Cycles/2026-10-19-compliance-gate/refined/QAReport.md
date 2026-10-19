# Task 5 별도 QA

판정: **PASS(검증 범위 내)**. 승인된 refined/ImplementationPlan.md의 구현·검증 조건을 아래 증거와 대조했다. 독립 코드 검수와 별도인 증거·화면 QA이며 제품 코드는 변경하지 않았다.

- TODO 1 설정·판단: red 44건(18통과·25실패·1skip) → green 45건(44통과·0실패·1skip). full의 관련 시험에서 필수 메타데이터·미래 날짜·상태 거부, 4필드 계약, 상태별 저장/재읽기, 무생성 읽기, 잠금 오류, 교체 실패 보존·임시 정리, junction 거부가 통과했다. 직접 파일 symlink 시험은 호스트 권한으로 skip이므로 실행 검증 완료로 보지 않는다.
- TODO 2 빌드 차단: full에서 미확인·철회·알 수 없는 상태, missing/unavailable 차단과 직전 파일 재조회·기본 콜백 시험 통과. 유효 확인 상태의 공개 허용 판단 시험도 통과. 실제 Player 빌드 및 공개 내보내기는 실행한 증거가 아니다.
- TODO 3 UI: 저장 실패 시 기존 상태 유지, 방문 시 무저장, 명시적 확인, 철회 시 시각 제거, 실제 탭 재진입·재생성·구독 해제 시험 통과. visual-final의 PNG 6장을 직접 열었다. 940px에서 세 정책 링크·두 영문 고지·출처/익명화/지표/제외 기능, 500px에서 상하단 스크롤과 과거 관측 고지, Settings 미확인→확인 기록·저장 성공·UTC 표시를 확인했다. 키·등록 비밀값 입력은 없다. 500px 중간 스크롤 위치의 별도 PNG는 없다.
- TODO 4 회귀·환경: 세 등록 상태의 개인 연구 CSV 저장·비율 억제 시험 통과. compile-status 내부 문자열 JSON은 up_to_date, compilationFailed=false, errors=[]. full은 546/545통과/0실패/1skip(58.68초). protected-before/after는 1,386파일·변경 0. 전체시험 전후 창 9개·native 프레임 1개·포커스 동일. 정상·표시 전/후 예외·fallback 모두 소유 잔류 0, 기존 ID 보존·포커스 복원이다.

Console은 동일 세션 cursor 2→22, 기존 buffer error 2/warn 0 유지, 테스트 후 Editor 실시간 error/warn 0이다. 과거 오류 전체가 0이라는 의미는 아니다. 독립 code-review의 spec/품질 PASS를 확인했다. 별도 Unity 재실행·네트워크·Git 쓰기는 하지 않았다.

한계/후속: synthetic/offline 검증이며 사용자 설정 미저장. 온라인 Riot 승인, 실제 Player 빌드, 취소·연결 끊김은 미실증이다. 문서 갱신·커밋·PR은 root 후속 단계다. 실제 모델·추론·사용량은 별도 확인하지 않았다.
