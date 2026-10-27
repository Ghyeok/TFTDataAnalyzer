# root의 독립 A/B 통합과 코드 대조

초기 공통 snapshot은 변경하지 않는다. A는 revise(P1 하나/P2 세 개), B는 revise(P2 하나)다. B의 null key/출처 지적은 별도 delta에서 pass를 받았다. 아래는 나머지 A 제안의 수용/판정이며 최초 응답을 pass로 바꿔 쓰지 않는다.

## A-P1: Sources pruning 제안은 불채택, 기존 의미 보존을 명문화

기준 f997472의 실제 코드와 다르다. DataDragonAdapter.cs:431 PairSources:

```csharp
return new[] { ToSourceStamp(pair.KoKr), ToSourceStamp(pair.EnUs) };
```

같은 파일의 Parse는 snapshot.AllResponses의 stamps를 manifest에 넣는다. 기존 record는 PairSources 전체를 보존한다. CatalogMerger.cs:374 MergeSources:

```csharp
return official.Concat(supplement)
    .GroupBy(source => $"{source.SourceName}\u001f{source.Url}\u001f{source.Sha256}", StringComparer.Ordinal)
    .Select(group => group.First())
    .OrderBy(source => source.SourceName, StringComparer.Ordinal)
    .ThenBy(source => source.Url, StringComparer.Ordinal)
    .ToArray();
```

CatalogAssetWriter.cs:510 AsSchema는 source.Sources를 그대로 전달하며, NormalizeForContentSnapshot은 source URL/SHA/종류를 보존하고 시간/ETag만 정규화한다. 따라서 같은 profile/같은 문서 원본에 새 구조의 **읽기만 추가**하는 이번 변경에서 Sources를 fieldOrigins로 재산출하면 오히려 구 팩 해시 계약을 깨뜨린다. 원본 SHA가 실제 달라지면 충돌이 정상이며 이를 삭제해 성공시키지 않는다.

판정: A의 ‘구 projection 출처 경계 검증 필요’ 우려는 수용하되 ‘실제 선택 leaf만으로 Sources pruning’ 처방은 코드 근거로 불채택한다. 계획에 same docs/new structure only 구schema3 재사용 성공·문서SHA변경 충돌·구schema3기존leaf출처변경 충돌을 명시했다. 새 source endpoint 종류는 추가하지 않는다.

## A-P2 세 건

- null 구간: B와 동일 우려이며 key필수/null허용/항상출처필수로 해결한다. exact minUnits/maxUnits/style/effects key pointer를 적었다. effect null entry도 출처필수다.
- BaseStats 결측: 공식/보완 각각 absent/null/number의9조합 표를 계획에 추가했다. 0은 number, false는 Unique HasValue로 유지한다. official null→supp number는 SupplementFill, absent→supp null도 명시 결측 보존의 SupplementFill이다. non-null 충돌만 OfficialWins다.
- known/unknown: ko/en 양 문서의 known source pointer를 값 채택 여부와 무관하게 Extensions에서 제외한다. 부분 소비 컨테이너는 unknown subtree만 보존한다. 잘못된 known 타입은 게시 차단 오류이며 extension으로 우회하지 않는다. 경계 tests를 명시했다.

정제 계획의 마지막 두 통합 절이 승인 대상이다. 이 문서와 정제 계획 수정은 제품 코드 변경이 아니다. A의 동일 전체 문서를 반복 전송하지 않고 기존 B에 이 bounded delta와 코드 근거만 재검토 요청한다. 사용자 구현 승인·제품 실행 증거는 별도다.
