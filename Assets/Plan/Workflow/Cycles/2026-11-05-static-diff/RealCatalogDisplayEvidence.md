# 포함 카탈로그의 비교 표시 경로 확인

기준 main ba50db8, 읽기 전용 검증. `Library/TftAnalyzerValidation/StaticDiffCatalogEvidenceProbe.cs`를 Unity에서 실행해 프로젝트에 포함된 18.2 카탈로그를 제품 serializer로 읽었다. 실제 태그 포함 아이템 이름에 가상의 변경을 메모리에서만 적용했다.

제품 `CatalogDiffer`는 변경 아이템 1개를 찾았고, 실제 `DataManagerTab.AddEntityDiff`가 생성한 Label에 기존 이름이 포함됐지만 `enableRichText=true`였다. 실행 전후 카탈로그 파일 SHA-256이 같았다. EditorWindow 생성, 외부 API 호출, 카탈로그 게시·수정은 하지 않았다.

이 결과는 실제 포함 데이터가 현재 비교 표시 코드에 전달될 수 있고 서식 해석이 활성화돼 있음을 보여준다. 가상의 변경을 비교했으므로 실제 다른 패치의 변경은 확인하지 않았다. 실제 화면 PNG와 글자의 최종 렌더링은 검증하지 않았다.
