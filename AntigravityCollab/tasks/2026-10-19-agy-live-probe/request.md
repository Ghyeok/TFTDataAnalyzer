# agy 자동 호출 연결 시험

2026-10-19. 사용자 요청: “agy 에이전트를 켰는데 확인해볼래?”

Codex가 공식 CLI의 별도 단발 세션을 실행해 자동 요청·응답이 가능한지 확인한다. 사용자가 열어둔 agy 세션을 재사용하거나 종료하지 않는다. 실제 기획 자료·프로젝트 코드·키·seed는 보내지 않는다.

확인한 실행 파일은 `C:/Users/gulww/AppData/Local/agy/bin/agy.exe`, 버전은1.4.2다. 제한된 환경의 조회는 네트워크·로그 캐시 접근에 실패했고, 승인된 환경의 모델 조회는 정상 종료했다. 모델 목록에 있는 `gemini-4-argon-medium`을 이 시험에 명시한다. 실제 응답은 성공 후 별도 기록한다.

## 실제 전송할 시험 요청

Return only the JSON object matching the provided schema, with taskId "2026-10-19-agy-live-probe" and ack "TFT_AGY_OK". This is a connectivity check only. Do not use tools, access files, run commands, or change anything.

요청은 stream-json 입력 한 건으로 전달한다. 시작 이벤트에서 agent·모델·활성 도구를 먼저 확인하며, 설정을 검증하지 못하면 전송하지 않고 실패로 기록한다.

## 준비 점검 후 시험 범위 분리

첫 두 초기화에서 CLI는 요청한 agent 이름과 모델을 보고했지만 전체 도구 목록을 계속 노출했다. 자료를 전송하지 않고 종료했으며, 작업 폴더 격리와 명시적인 완료 도구 지정으로도 같은 결과였다. 원인이 확인되지 않았으므로 도구 제한 성공으로 기록하지 않는다.

별도 **연결만 확인하는 시험**에서는 프로젝트 밖 고유 임시 폴더와 plan 모드에서 위 상수 응답 요청 한 건만 보낸다. 실제 자료는 계속 보내지 않으며 도구 사용이 관측되면 실패 처리한다. 이 시험의 성공은 CLI 요청·응답만 증명하며, 접근 차단·기획 workflow 활성화를 증명하지 않는다. 런타임 기록도 초기화 점검과 분리한다.
