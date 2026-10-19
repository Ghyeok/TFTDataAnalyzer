# 과거 집계 분석 체크포인트

갱신일: 2026-10-19. 상태: PR12의 저장 작업 경합 의견을 검증해 수정, 독립 코드 검수·실행 검증·후속 독립 QA 통과. 수정 커밋을 통합하며 최신 head 재검수는 별도다. 최초 Task4 QA 이력과 구분한다.

- 승인 범위: 기존 Phase3 Task4 분석·필터·로컬 CSV와 사용자 재개 요청. Task5/6과 네트워크 호출은 제외한다. 독립 기획2건 통합 완료.
- 기준 HEAD: 9d87f5d3d0c9d181ceab587f915d496e79fd3d0e, PR11 병합 상태. 브랜치 codex/historical-analysis-tab.
- 사용자 변경 Assets/DefaultVolumeProfile.asset과 생성 데이터1,386개를 보존하고 stage하지 않는다. 보호 기준은 Library/TftAnalyzerValidation/collection-protected-baseline.json이다.
- Hermes5분 모니터 c5f808787506은2026-10-19 사용자 요청으로 paused, 활성 예약0을 확인했다. 대화형 Hermes 모델은 호출하지 않았으며 명시 재개 전까지 다시 켜지 않는다. [운영 지침](../../../../../docs/coderabbit-monitoring.md)을 따른다.
- 최신 실행: 저장 작업 조정 수정 뒤 컴파일 completed/failed=false/compilationFailed=false/errors[], 신규 집중6/6, 전체 EditMode501/501(62.71초, skipped0). 최초 Task4의 UI7/7·CSV5/5·전체495/495(54.24초)는 이전 이력이다. 중간 분석27/28을 통과로 사용하지 않는다.
- 전체 실행 전후 기존 EditorWindow9개·native ContainerWindow1개 ID 동일, 포커스 동일. 최종 수동 캡처6장(940/500px 상단·조합·쌍)을 직접 열어 대상 UI·99 표본 억제·100 비율/구간·작은 창 스크롤·고지를 확인했다. 현행 도구의 표시 전/후 실패 probe와 fallback도 소유 창0·기존 창/프레임 보존이다.
- 실패 이력: 특수 Windows 경로 테스트가180/60초 시간 초과됐고 Editor가 응답하지 않아 사용자가 두 차례 종료·재시작했다. Mono 부모 경로가 같은 루트를 반환하는 반복을 별도 bounded 조사로 확인하고 IO 전 차단·실제 경로 대조로 수정했다. 타임아웃 취소/창 정리에 성공한 것으로 기록하지 않는다. 저장 modal의 지연된 필터 이벤트도 실제 RED 후 수정했다.
- 캡처 이력: 최소 ScrollTo 때문에 쌍 행이 화면 밖에 있었고 다른 실행에서는 다른 프로그램 픽셀이 찍혔다. 성공 JSON만으로 통과 처리하지 않았다. 상단 정렬과 PID/실행 전 HWND 차이로 확인한 새 소유 프레임 하나의 가시성 확보 후 최신6장을 직접 검수했다. 모든 외부 가림/연결 끊김 경로의 해결을 주장하지 않는다.
- 보호: Volume·Generated 기준1,386개 SHA256 변경0. 이번 후속 검증의 Console 기준 이후 새 경고·오류0이고 기존 Pipeline GET /api/commands 통신 오류4건은 보존했다. 최초 Task4의 기준2건/cursor32와 구분한다.
- 역할: 독립 기획2건, 서비스/VM/CSV 및 UI 구현 분담, root가 후반 통합·수정·유일한 Unity 실행, 별도 Astra High 요청 코드 검수, 별도 Sol High 요청 QA. 구현·검수 중 사용량 제한으로 중단된 응답은 완료로 취급하지 않았다. 실제 runtime 모델·추론·토큰·총 활성 시간은 미확인이다.
- 미실행: 실제 Riot API/키/seed, 실데이터 증강 분류 생성, 네이티브 저장 대화상자 실제 클릭, OS 파일 교체 실패 주입, Task5/6 기능. 최신 문서와 검증은 [개발 보고](refined/DevelopmentReport.md), [사용 안내](refined/FinalDocumentation.md), [실행 증거](raw/ValidationEvidence.md)를 따른다.
- 독립 QA: [QA 보고](refined/QAReport.md)와 [공개 원문](raw/QAAgent.md)은 코드·실행·최신6장·현행 실패 probe·보호·Console·문서의 일치를 PASS로 확인했다. 문서 링크 검사도 깨진 링크0이다.
- PR12 재개: 최신 head0dacaae의 CodeRabbit SUCCESS라도 미해결 의견1건이 있어 병합하지 않았다. 수집과 분석 전체를 공유 조정자로 보호하는 [후속 수정](refined/StoreCoordinationReview.md)을 반영했다. 관측 읽기만 보호하지 않고 집계 저장까지 포함하며 대기 취소·실제 종료 전 소유권 유지도 확인했다. 이번 전체501개 전후 기존9창/1프레임 ID·포커스 동일이다.
- 다음: 사용자 Volume·Generated를 제외한 수정 커밋 식별자는 이 브랜치 Git 로그를 따른다. PR12 수정 커밋 push 뒤 링크·검증 결과를 알리고 반복 조회 없이 사용자 재개를 기다린다. 재개 시 최신 head CodeRabbit·미해결 thread·CI를 한 번 확인한다. 사용자 최종 사이클 완료 승인은 별도이며 Task5/6은 착수하지 않는다.
