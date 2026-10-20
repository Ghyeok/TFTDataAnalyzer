# 기존 코드검수 담당 공개 결과

/root/pr14_revision_review 재사용, 신규 에이전트0. 최초 P3와 제한 재확인을 모두 보존한다.

# Augment policy provider 독립 코드검수

기준534cfa5. 승인 ImplementationPlan 및 지정 제품4/테스트2파일에 한정했다. 판정: 경미한 경로 계약 결함1건 수정 권고, 나머지 지정 경계에서 확정 finding 없음. 서비스 GREEN·전체 검증 완료 판정은 유보한다.

## 조치 필요

[P3] Assets/TFTAnalyzer/Editor/Compliance/ReviewedAugmentPolicyProvider.cs:64–66
조건: Windows에서 projectRoot가 C:relative 또는 \relative이면 Path.IsPathRooted는 true이며 UNC 거부도 통과한다. 이어지는 GetFullPath는 현재 디렉터리/드라이브를 기준으로 해석한다.
영향: 명시적 절대 projectRoot 계약을 위반하고 다른 위치의 Policy 경로를 사용하거나 환경에 따라 생성에 실패한다. 현재 Window는 paths.ProjectRoot를 넘겨 직접 영향은 없고 해시 검증도 유지돼 임의 정책 신뢰로 확대되는 결함은 아니다.
최소 수정: drive-relative/root-relative를 거부하는 IsPathFullyQualified 등 완전한 절대경로 검사를 사용한다. Windows 상대 두 형태 거부 및 정상 절대경로 수용 회귀를 추가한다.

## 확인된 보존 계약

- 기본 Approved는 빈 비공개 목록이며 등록값과 sources는 복사된다. registration은 전체 identity(별도 SetNumber 포함)+ContentHash로 선택하고 미등록은 파일을 열지 않는다. 경로는 승인 SHA-256 이름으로 고정하며 링크 검사는 open 전후에 수행한다. 검토일/HTTPS 출처의 형식 검사 자체가 사람 검토를 증명한다고 해석하지 않는다.
- 최대256KiB/깊이8/규칙4096, 원시 바이트 해시, strict UTF-8와 별도 JSON 문법, 중복속성/알 수 없는 필드/타입/enum/정규화 ID/카탈로그 밖 ID/identity·콘텐츠 불일치 거부를 확인했다. 메타데이터 생성은 전체 검증 뒤이며 누락 ID는 Unknown 경계를 유지한다.
- InvalidDataException 포함 예상 I/O/파싱 오류는 Invalid로 마스킹하고 취소는 전파한다. result는 실패에 Metadata를 제공하지 않는다. 새 UI/로그에 원문 예외·자료·경로를 넣지 않는다.
- 기존 생성자/policy 주입과 BuildSnapshot 호환, policy/provider 충돌 거부, LoadAsync의 소유 Task 안에서 작업 identity에 대한 provider1회 호출과 작업별 지역 policy를 확인했다. Missing/Invalid는 기본 차단이고 A→B 간 이전 policy를 저장하지 않는다. Window 조립도 기존 프로젝트 경로를 전달한다.
- 합성 자료만 검증하며 배포 등록0개·실분류자료 미확보이므로 실제 증강 비율 차단 해제 완료가 아니다.

## 증거·미실행

root 산출물: provider RED 42중41F1P→1차 GREEN 28F14P(InvalidDataException 마스킹 누락)→GREEN2 42/42. service RED 7중4F3P. 지정 compile-status 3개는 completed/failed=false/errors=[]다. 서비스 GREEN은 이 검수에서 확인하지 않았으며 root 후속 실행 대상이다. 기존 tracked diff --check 종료0.
여기서는 테스트/Unity·네트워크·제품/Git 쓰기를 수행하지 않았다. 전체 EditMode·실제 API·수동 PNG는 미실행이며 과거 증거를 새 통과로 적용하지 않는다. 새 에이전트/재위임0. 실제 모델·추론/토큰·비용은 확인 불가.

## P3 경로 수정 제한 재확인

ReviewedAugmentPolicyProvider.cs:64가 IsPathFullyQualified로 바뀌었고 생성자에서 GetFullPath 전에 drive/root-relative를 거부한다. DriveOrRootRelativeProjectRootsAreNotGuessed의 C:relative, \relative, relative 3case를 대조했다. 지정 원문 증거는 경로 RED 3중2F1P(기존 상대 두 형태 수용)→provider 최종45/45, 최종 컴파일 completed/failed=false/errors=[]이며 service GREEN도7/7이다. P3 finding은 해소됐고 이번 검수에서 남은 조치 필요 결함은0건이다.

이번 확인은 이 경로 경계와 지정 증거에 한정했으며 전체 재검수·추가 테스트·Unity 실행은 하지 않았다. 전체 EditMode/실제 API/수동 PNG는 본 검수에서 미실행이다. root 실행 증거를 확인한 것으로 전체 QA/병합 통과나 실제 분류 자료 승인 판정은 아니다. 새 에이전트·제품/Git 쓰기·네트워크0. 기존 보고서 원문은 보존했다.
