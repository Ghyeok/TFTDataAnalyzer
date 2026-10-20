# 수집 준비 안내 수정 개발 보고

2026-10-20. base main fa0df4d, branch codex/collection-readiness. 사용자 계획 승인 후 root가 직접 TDD·구현·Unity 실행을 맡았다. 기존 코드 검수 담당을 재사용했고 신규 에이전트는 생성하지 않았다. 현재 세션의 실제 모델·추론·개별 토큰/비용은 독립 확인하지 못해 추정하지 않는다.

## 결과

선택한 카탈로그가 있는데도 하단에 “카탈로그를 선택해 주세요”가 남던 최초 수집 전 안내를 수정했다. Catalog/Identity→입력→키·시드 모두 부족→키 부족→시드 부족→입력 조건 준비 순서로 안내한다. 기존 Label/Changed/Render를 사용하고 Notify 횟수를 늘리지 않는다. 설정 존재는 API 인증·네트워크 성공을 뜻하지 않는다.

제품 변경은 CollectionViewModel.cs 하나다. ValidateLatestInput과 RefreshConfiguration에서 순수 helper를 호출하고 !disposed/!busy/Idle일 때만 StatusText를 바꾼다. 실제 수집·취소·승인·실행 snapshot·상한·CanStart·오류 코드·진행률·터미널 상태는 변경하지 않는다. 완료 후 입력/카탈로그/설정을 바꿔도 이전 작업 결과를 자동 초기화하지 않는다. 비밀값은 읽거나 표시하지 않고 기존 bool만 사용한다.

## 실제 실행

- 테스트 먼저: CollectionViewModelTests에 준비 조합/우선순위·입력 복구/설정 예외/실행·종료 보존10개, CollectionTabTests에 실제 환경 설정 버튼→Label/시작 버튼 갱신1개를 추가했다.
- RED:11개 중7실패·4통과,5.08초. 준비 문구가 이전 카탈로그 선택 안내로 남아 실패했다. 상태 보존4개는 변경 전부터 통과했다. raw JSON의 한국어는 정상이며 초기 stdout 인코딩 표시만 cp949였고 이후 -X utf8로 출력했다.
- GREEN: 최소 제품 수정 뒤 같은11/11,5.05초. 관련 수집 UI/VM 전체65/65,12.91초.
- 최종 전체 EditMode:631개 중630통과/0실패/1제외,75.48초. 제외는 ComplianceSettingsStoreTests.ExistingFileLinkIsRejectedWithoutReadingOrOverwritingDestination의 Host does not permit unprivileged symlinks다.
- 컴파일은 RED/GREEN 모두 completed/failed=false/compilationFailed=false. 최종 전체 실행 전 소스3파일 해시를 동결했고 실행 뒤 같은 해시를 확인했다.
- 기존 창9개 `[50722,50724,50726,50728,50730,50732,50734,50736,50738]`, native1개 `[50698]`, focus0이 전후 정확히 같았다. 보호1,386개 변경0, E2E 임시 자산 폴더0. 이 ID는 이번 비교용이며 다음 정리 대상으로 사용하지 않는다.
- Console 같은 세션0cfbfa8f25714274abce8e1a2a9449ce의 cursor20→32에서 새 오류2건(seq26,27). 두 stack 모두 Unity.Pipeline/BasePipelineServer의 /api/commands transport disconnect·headers already sent다. 제품 컴파일 실패는 false, Console 현재 오류2/경고0이다. 오류0 또는 수정 완료로 쓰지 않고 별도 도구 한계로 기록한다.

원본: Library/TftAnalyzerValidation/collection-readiness-{red,green,focused,full-suite}.json, *-compile-status.json, *-windows-before/after.json, *-console-before/after.json, *-source-freeze.json, *-final-evidence.json. 원본은 Git 제외이며 공개 결과는 본 보고와 raw에 보존한다.

## 범위와 한계

실제 Label/binding과 버튼 상태는 실행했다. UXML/USS·레이아웃을 바꾸지 않아 수동 PNG 캡처는 수행하지 않았고 시각 검수 완료로 주장하지 않는다. 실제 키/seed/API 호출은 하지 않았다. KI-001은 미해결 기록 후 개발 계속이며 이 수정으로 해결됐다고 주장하지 않는다. 증강 신뢰 분류 공급·표본 입도 개선도 추가하지 않았다.

코드 검수 finding0, 별도 QA 조치 필요 결함0. [QA 보고](QAReport.md)와 [최종 문서](FinalDocumentation.md)에 결과를 반영했다. 커밋/PR·최신 head 검수와 사용자 최종 승인은 별도다.
