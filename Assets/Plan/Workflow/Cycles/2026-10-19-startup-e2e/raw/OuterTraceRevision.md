# PR14 바깥 예외 진단의 경로 제외 수정

2026-10-20. 기준8f0ac09. 개발 재개 승인으로 PR14를1회 조회했다. 최초 GraphQL은 지원하지 않는 CheckRun.app 필드로 실패해 조회 데이터를 얻지 못했고, 해당 필드를 제거한 조회가 성공했다. head8f0ac09, CodeRabbit SUCCESS 및 해당 head COMMENTED review를 확인했다. 별도 CI check는 없고 미해결 thread1개(PRRT_kwDOVK_2hM6uE5xu, AgentScripts/TftAnalyzerEndToEndVisualValidation.cs:160)가 있었다. 전체 pagination은 완료다. 성공 상태를 미해결0과 혼동하지 않는다.

## 변경과 실제 실행

`cause.StackTrace`를 직접 저장하던 한 지점을 타입·메서드 프레임 문자열로 바꿨다. 예외 형식명·독립 detail/state try/catch·원래 실패값·Passed 판정·타임아웃·소유 창 정리는 유지한다. 제품/테스트 Assets는 변경하지 않는다.

- RED 원본: Library/TftAnalyzerValidation/pr14-outer-trace-red. 정본 도구의 출력 root와 capture callback만 바꿔 await Task.Yield 뒤 합성 InvalidOperationException을 발생시켰다. 원래 pipeline/import를 통과한 뒤 같은 바깥 catch에서 detail을 저장했다. ExceptionTypeRetained=true, ScenarioMethodRetained=true, AbsoluteSourcePathPresent=true, CsSourceMarkerPresent=true로 경로 제외 계약이 실패했다. Exception.Message sentinel은 기록되지 않았다.
- GREEN 원본: 같은 조건의 pr14-outer-trace-green. 동일 예외 형식·시나리오 메서드는 남고 절대 경로·.cs:·Message sentinel은 모두 없다. ExpectedPrivacyContractPassed=true다.
- 두 실행 모두 strict Passed=false/VisualValidationFailed:InvalidOperationException/captures0, 실제 실패·state·최종 보고서는 유지됐다. 별도 CleanupLastOwned.Verified=true다. 이는 원래 KI-001 실패 재현이나 정상 UI 시각 통과가 아니다.
- 전체 제품620suite는 Assets 소스가 같아 반복하지 않았다. 변경 실행 도구는 실제 run_script로 컴파일·실행했다. 과거 suite 수치는 새 실행으로 기록하지 않는다.

## 독립 검수 요청

기존 pr14_revision_review 담당을 재사용한다. scope는 AgentScripts/TftAnalyzerEndToEndVisualValidation.cs의 마지막 diff와 위 RED/GREEN 산출물이다. 읽기 전용으로 타입/메서드만 출력·null frames·독립 guard·기존 실패/정리 계약 보존을 확인하고 Library/TftAnalyzerValidation/pr14-outer-trace-review.md에800자 이내 결과를 작성한다. Unity·네트워크·Git 쓰기·추가 에이전트·제품 수정은 하지 않는다. 원래 KI-001은 미해결로 개발 진행 허용이며 전체 QA/병합 통과를 선언하지 않는다.

## 별도 QA 요청

기존 pr14_diagnostics_qa 담당을 재사용한다. scope는 동일 diff·RED/GREEN privacy-summary/실제 command·cleanup/source-manifest와 새 docs/known-issues.md의 사용자 결정 일치다. 읽기 전용으로 실제 오류 판정·예외/메서드 보존·경로/Message 제외·소유 창 정리·시험 범위를 대조한다. Library/TftAnalyzerValidation/pr14-outer-trace-qa.md에800자 이내 결과를 작성한다. Unity·네트워크·Git 쓰기·추가 에이전트·제품 수정은 하지 않는다. 미실행 전체 suite/PNG/Console을 통과로 쓰지 않는다.

기존 코드 검수와 별도 QA는 이번 한정 수정 범위를 수용했다. 모델·추론은 기존 담당자의 설정을 재사용하며 이번 런타임 실제 모델·비용·토큰별 사용량은 독립 확인할 수 없어 추정하지 않는다.

## 독립 검수 공개 응답

# PR14 바깥 trace 독립 검수

기준8f0ac09의 지정 6줄 diff와 RED/GREEN 증거만 검토했다. 확정 finding 없음.

GetFrames null은 빈 문자열로 처리하고 null GetMethod는 제외한다. 출력은 예외 형식명과 DeclaringType.FullName/Name이며 파일 경로·줄·Exception.Message를 읽지 않는다. 독립 detail/state guard와 원래 failure/Passed/finally·소유 창 정리는 보존됐다.

RED/GREEN clone hash가 manifest와 일치하고 원본 hash도 일치한다(RED는 Git LF→작업본 CRLF 정규화). clone 차이는 Root/callback뿐이다. 실제 command 양쪽 실행 성공·strict Passed=false·InvalidOperationException·captures0을 확인했다. RED detail은 경로/.cs: 포함, GREEN은 예외 형식·시나리오 메서드만 보존하고 경로/.cs:/Message sentinel이 없다. privacy-summary와 일치한다. cleanup 양쪽 Verified=true, owned 0·기존 window/container·focus 보존이다.

git diff --check 종료0. 실행은 root 증거를 검토했으며 새 Unity 실행은 하지 않았다. KI-001 원인은 미해결이며 사용자 승인대로 개발 계속 가능하다. 원인 해결·전체 QA·병합 통과 판정은 하지 않는다. 런타임 모델/토큰/비용은 확인 불가.

## 별도 QA 공개 응답

# 바깥 예외 진단 한정 QA

판정: 이번 경로 제외 수정 수용. HEAD 8f0ac09의 단일 diff와 RED/GREEN command·cleanup·privacy-summary·source-manifest·실제 detail/state를 대조했다. GREEN 정본/clone 해시는 manifest와 일치하며 clone 차이는 root와 비동기 실패 callback뿐이다.

RED는 절대 소스 경로/.cs: 노출, GREEN은 두 값과 Message sentinel 없이 InvalidOperationException 및 시나리오 메서드를 보존한다. GetFrames/GetMethod 타입·메서드만 출력하고 null frames는 빈 값이다. 독립 guard·원래 실패값·finally는 유지된다. 두 실제 실행 모두 컴파일 진단 없음, Passed=false/InvalidOperationException/captures0와 state·최종 보고서를 보존했다. 별도 cleanup Verified=true, 소유 창 0, 기존 창/native ID 보존·focus 복원을 확인했다.

KI-001 미해결 기록과 개발 선행 차단 해제는 사용자 결정에 부합한다. 원래 간헐 오류 재현/해결이나 전체 QA·병합 판단은 하지 않는다. 전체 suite·PNG·Console은 이번 미실행이다. QA는 읽기 전용이며 추가 에이전트·Unity·네트워크·Git/Assets 쓰기는 없다. 실제 모델·사용량은 확인되지 않아 추정하지 않는다.

## root 최종 확인

pr14-outer-trace-final-summary.json에서 RED/GREEN의 strict 실패/최종 보고·각 소유 기록 대비 기존 창9/native1/focus 보존, 보호1,386개 변경0·E2E 자산 폴더0을 재확인했다. 실행 C#은 한 파일뿐이다. 최신 제품 전체 suite/PNG/Console은 이번 변경에서 반복하지 않았다. 이전 전체620/619P/0F/1제외는 기존 제품 코드 증거다.

이번 수정과 KI-001 개발 계속 승인·서브에이전트 재사용 지침을 함께 커밋/PR 반영한다. push 후 CodeRabbit/CI를 반복 조회하지 않는다. 수정한 기존 thread만 해결 표시하며 새 head의 검수 통과·미해결0·전체 완료를 주장하지 않는다.

## 실제 발송한 공개 후속 요청

대상: /root/pr14_revision_review

> 기존 코드 검수 담당을 재사용하는 한정 요청입니다. C:/Unity/TFTDataAnalyzer/Assets/Plan/Workflow/Cycles/2026-10-19-startup-e2e/raw/OuterTraceRevision.md의 '독립 검수 요청'을 수행하세요. 현재 기준 HEAD 8f0ac09, 실제 코드 diff는 AgentScripts/TftAnalyzerEndToEndVisualValidation.cs의 원시 cause.StackTrace→GetFrames/GetMethod 타입·메서드 출력 6줄뿐입니다. Library/TftAnalyzerValidation/pr14-outer-trace-red 및 green의 실제 command/cleanup/privacy-summary/source-manifest를 읽어 일치 여부를 검수하고 Library/TftAnalyzerValidation/pr14-outer-trace-review.md에 800자 이내 결과를 작성하세요. root가 구현/실행했습니다. Assets 제품 코드·Unity·네트워크·Git 쓰기·추가 에이전트·기타 수정 금지. 원래 KI-001은 사용자 승인으로 미해결 기록 후 개발 계속이며 원인 해결/전체 QA/병합 통과 선언은 하지 마세요. 요청 범위 외 과거 전체 검수 반복 금지. 완료 후 멈추세요.

대상: /root/pr14_diagnostics_qa

> 기존 QA 담당 재사용 한정 요청입니다. C:/Unity/TFTDataAnalyzer/Assets/Plan/Workflow/Cycles/2026-10-19-startup-e2e/raw/OuterTraceRevision.md의 '별도 QA 요청'을 수행하세요. HEAD 8f0ac09, 코드 diff는 AgentScripts/TftAnalyzerEndToEndVisualValidation.cs의 원시 cause.StackTrace→GetFrames/GetMethod 타입·메서드 출력뿐입니다. Library/TftAnalyzerValidation/pr14-outer-trace-red 및 green의 actual command/cleanup/privacy-summary/source-manifest를 대조해 RED 경로 노출→GREEN 경로 제외, 예외/메서드 보존, negative Passed=false, 독립 guard/최종 보고/소유 창 정리를 확인하세요. docs/known-issues.md는 사용자 원문 '그럼 일단 실패 기록만 남기고 개발을 이어서 진행해보자'에 따라 KI-001을 미해결 기록하고 개발 선행 차단만 해제했습니다. 결과를 Library/TftAnalyzerValidation/pr14-outer-trace-qa.md에 800자 이내 작성하세요. root 구현/실행, 추가 에이전트0. Assets/Unity/네트워크/Git 쓰기/기타 수정 금지. 전체 suite/PNG/Console 미실행을 통과로 쓰지 말고 전체 병합 판단도 하지 마세요. 완료 후 멈추세요.
