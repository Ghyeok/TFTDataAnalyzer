# 비동기 진단 경로 노출 수정 공개 기록

2026-10-20. 수정 기준 bd2079f. root는 Unity 실행·통합, 구현자는 요청 Sol/Medium, 독립 검수는 Sol/High, 별도 QA는 Sol/Medium으로 배정했다. 실제 runtime 모델·추론·토큰·비용 총계는 독립 확인 불가로 추정하지 않는다.

## 새 실행 증거와 이전 판단 정정

원래 시각 검증 C#을 Library에 복사해 증거 Root와 캡처 callback만 바꿨다. callback은 임시 Assets/TftAnalyzerE2E_<GUID> 루트 형식·경계·기존 RejectReparsePoints guard·대상 미존재를 확인한 후16.18.2 디렉터리 자리에 고정 내용의 파일 하나를 만들었다. 서비스/publisher/VM/실패 JSON/catch/finally/Passed 판정은 바꾸지 않았다. PNG 생성은 하지 않았다.

이 controlled failure는 실제 CatalogAssetWriter.Publish의 IOException을 발생시켰다. JSON에서 ImportFailed·IOException과 게시 함수는 확인됐지만 bd2079f의 StackTrace(error,false).ToString 출력에는 절대 .cs 경로가 남았다. 앞선 '파일 경로 제외' 판단을 정정한다. 이는 원래 간헐 오류의 원인 증거가 아니다.

- 임시 probe 첫 시도는 internal guard 직접 호출의 컴파일 오류로 실행되지 않았다. 반사로 같은 guard를 호출하도록 도구만 수정했다. compile-error-*.json을 보존하며 제품 오류로 세지 않는다.
- probe SHA는 처음 LF 정규화 텍스트를 계산한 값이었다. CRLF 원시 파일 byte SHA와 LF 텍스트 SHA를 별도 필드로 명시했다. 내용 변경/위조로 판단하지 않는다.
- pr14-async-trace-red.json은 compile 상태 compiling에서 구 assembly2개만 실행해 회귀 증거에서 제외했다. completed/오류없음을 확인한 뒤 pr14-async-trace-red-current.json의 현재3개 중 async1실패를 유효 RED로 사용했다.
- 구현 후 GREEN3/3과 같은 actual publisher probe에서 .cs/절대 경로 없음·게시 함수 정보 보존·실패 JSON export·owned cleanup을 확인했다. 정상 시각 판정 Passed=false를 그대로 유지한 의도된 실패 시험이다.
- 최종 전체620개 중619통과/실패0/호스트 링크1제외,81.73초. 창9/native1/focus0 동일, 보호1,386개 변경0/임시 자산0. 원래 간헐 오류의 전체 QA·병합 보류는 유지했다.

## 구현자에게 보낸 GREEN 요청

> 확정 RED: pr14-async-trace-red-current.json Total3 Passed2 Failed1. async True만 .cs/C:\경로가남아실패. 이전 pr14-async-trace-red.json은compilation완료전구assembly2개만실행되어증거제외; root이후 completed확인후 current3개실행. GREEN진행하세요. 내부renderer는 new StackTrace(error,false).GetFrames()에서GetMethod metadata만출력(typeFullName+methodName), raw.ToString/StackTrace string/GetFileName/Message/Data금지; formatter예외삼키기/clear/원래ErrorCode+finally유지. 실제forcedprobe에서Publish발생frame보존도 root가확인예정. scope2파일한정, Unity/tests/Git/network/추가agent없음. GREEN_READY에서편집중단.

## 독립 검수에 보낸 요청

> 최종metadata renderer검수요청. 구현agent GREEN_READY후 root completed확인→실제pr14-async-trace-green.json3/3pass; REDcurrent3중1asyncfail. VMdiff8lines GetFrames→GetMethod.DeclaringType.FullName+Name만string.Join; 원래formattercatch/clear/type/state/finally불변. Testsync기존+asyncawaitthrowrethrowcase추가. 지정2source파일diff검수, raw.ToString나StackTracegetter없는지/실제발생frame보존/privacy계약검토. 원인미확정/병합보류유지. 지금root동일ownedfilecollisionprobe한번재실행중. 결과전달후확정해주세요. 결과 Library/TftAnalyzerValidation/pr14-trace-sanitization-review.md, product/Unity/Git/network쓰기금지. 원probereview경로 Library/pr14-owned-probe-review.md도최종doc에서공개보존예정.

## 별도 QA에 보낸 요청

> 별도QA후속: 앞선bd2079f diagnosticprivacy가 실제asyncfault에서 깨짐을발견. 원래harnessclone(probeRoot/PNGcallback만교체)에서ownedGUIDAssetroot검증+링크거부후source16.18.2부모에filecollision강제→actualpipeline IOException Publish→VMgenericcatch→원본exportfailureJSON, 여기에PDB절대.cs경로. 증거 Library/TftAnalyzerValidation/pr14-owned-import-fault-probe/before-fix/e2e-visual-failure-state.json. 원래간헐rootcause아님. source새diff2files DataManagerViewModelTests.cs asyncawaitthrowrethrowcase기존test확장, VMrenderer GetFrames.GetMethod metadata타입/메서드이름만구성. raw ToString/getStackTrace/PDBfilegetter안씀. 실제RED pr14-async-trace-red-current.json3중2P1F(asyncprivacy)→GREEN pr14-async-trace-green.json3/3. 이전red.json은컴파일중구assembly2개로실행돼유효증거제외. root강제probe현재다시실행중, fullsuite도뒤이어예정. 읽기전용QA새보고 Library/TftAnalyzerValidation/pr14-trace-sanitization-qa.md초안, source/evidence필요부분만. noUnity/network/Git/Assets쓰기/추가agent. 새진단privacy결함수정한정QA/원래간헐issue전체QA보류분리. 실제모델usage미확인추정금지. 최종증거대기.

## probe 출처

Library 원본은 삭제될 수 있다. 원본/복제 C#의 CRLF byte SHA와 LF 정규화 SHA를 섞지 않는다. 아래 값은 실행에 사용한 파일 byte 기준이다.

```json
{
  "source": "AgentScripts/TftAnalyzerEndToEndVisualValidation.cs",
  "sourceFileSha256": "e167208baa758eb9afc1069fa05c65efc53fafa579e4481f2084d26f4e6a86d1",
  "probeFileSha256": "9562fd19a9ed4b14828d122928434d87a9afde387222ded670d162d58d480467"
}
```

[최종 개발 검수](TraceSanitizationReview.md), [최종 별도 QA](TraceSanitizationQA.md).

## probe 독립 검수 공개 응답

# PR14 소유 경로 충돌 probe 독립 검수

판정: 지정 probe의 통제된 실패 주입은 진단 export 검증에 타당하며 probe 자체에서 조치가 필요한 코드 문제는 찾지 못했다. 기존 간헐 ImportFailed의 원인은 여전히 미확정이다. 앞선 pr14-diagnostics-code-review.md의 “새 진단에 PDB 파일 경로를 보관하지 않는다”는 판단은 아래 실제 증거로 철회한다.

- 검토 범위: 지정 source manifest, probe C#, failure-state JSON, fallback-cleanup JSON만 읽었다. 제품 수정·Unity 실행·Git·네트워크·추가 에이전트 실행은 하지 않았다. 결과 문서만 작성했다.
- 출처: manifest의 기준 head는 bd2079f0b2ca62d5e260293ef72f5402d2964e22다. probe의 UTF-8/LF 정규화 SHA256은 d05825ac8c3f85a1974b34646ea810c2e9368193f78f0f29692c8b956b8a03a4로 manifest와 일치한다. 실제 CRLF 파일의 byte hash는 9562fd19a9ed4b14828d122928434d87a9afde387222ded670d162d58d480467이므로 정규화 기준을 구분해야 한다. 원본 파일의 sourceSha256은 이번 제한 범위에서 독립 재계산하지 않았다.
- 주입 경계: scenario의 실제 _assetRoot를 읽어 Assets/TftAnalyzerE2E_ 접두어와 정확한 N형 GUID를 요구한다. blocker의 정규화 절대 경로가 ownedRoot 하위인지 검사하고, 기존 RejectReparsePoints를 정확한 타입에서 반사 호출하며, 기존 파일·디렉터리가 있으면 거부한다. 그 뒤 고정된 Generated/TFTSet18/18.2/16.18.2 위치에 충돌용 파일 하나를 생성한다. 사용자 Generated·기존 카탈로그를 대상으로 쓰는 경로는 없다.
- 실제 경로: 구현체나 VM에 예외를 주입하지 않고 기존 RunAsync를 호출한다. 실패 JSON의 System.IO.IOException과 FileSystem.CreateDirectory → CatalogAssetWriter.Publish → StaticCatalogPipeline.RunAsync → DataManagerViewModel.ImportAsync frame은 실제 게시 경로에서 예외가 발생하고 generic catch 진단을 기존 exporter가 읽었음을 뒷받침한다. 전송 구현은 기존에 검수한 fabricated scenario를 그대로 사용하며, probe는 transport·service·publisher를 교체하지 않는다.
- 기존 계약: 이전 검수 원본과 비교해 catch의 고정 failure 분류·ReadImportDiagnostic 기반 JSON export·finally의 deadline 취소/ReleaseOwned/scenario.Dispose/focus 복원·Passed 조건 구조가 유지됐다. 캡처 콜백은 의도적으로 PNG 생성 대신 충돌만 주입하므로 Captures=0이며, 기존 Passed의 6장 조건을 만족하지 않는다. 이 실행을 일반 시각 QA 통과로 해석할 수 없다.
- 경로 유출의 확정 증거: ImportFailureStack에는 CatalogAssetWriter.cs:240과 StaticCatalogPipeline.cs:216의 C:\Unity\TFTDataAnalyzer\Assets 하위 절대 경로가 실제로 남았다. 따라서 StackTrace(error, false).ToString()만으로 async 경로의 파일 경로 제거를 보장한다는 주장은 틀렸다. async 이력 문자열이 원인이라는 세부 기전은 이 자료만으로 확정하지 않는다.
- 창 정리 증거: fallback-cleanup은 Verified=true, OwnedRemaining=0, Failure 빈 문자열, 창 ID 9개와 Container ID 1개의 전후 집합 동일, 추가·누락 배열 모두 빈 값, OriginalFocusRestored=true다. probe finally는 fixture.Dispose를 호출하고 blocker도 fixture 소유 루트 안에 있지만, 지정 JSON만으로 파일·디렉터리 실제 삭제까지 독립 확인한 것은 아니다.
- 최초 컴파일 실패 구분: manifest는 internal guard 접근의 최초 시도가 컴파일 오류로 끝나 창을 만들지 않았다고 기록한다. 현재 probe는 반사 조회로 guard를 호출한다. compile-error 산출물은 지정된 읽기 범위에 포함되지 않아 독립 내용을 확인하지 않았으며, 최초 오류를 본 실패 주입 실행이나 창 정리 성공 증거에 섞지 않았다.

원본 간헐 오류 재현·원인 해결·QA 통과·병합 승인을 주장하지 않는다. 실제 모델·토큰 사용량도 독립 확인할 수 없어 추정하지 않았다.
