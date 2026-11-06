# 구현 경계와 검증

DataManagerTab.RenderDiff/AddEntityDiff는 데이터 모델을 Foldout/Label로 렌더한다. field.Before/After와 field.FieldName, StableId, added/removed ID, 카탈로그 DisplayIdentity가 사용자 표시 텍스트다. 외부 데이터에 rich text가 들어갈 수 있으므로 소스 값을 변경하지 않고 해당 동적 시각 요소 내부 Label의 rich text 동작을 차단해야 한다. Foldout의 내장 Toggle/Label도 함께 검증한다. Label.text만 비교하면 실제 화면 서식 해석을 검출하지 못한다.

합성 두 카탈로그를 사용해 이전/이후가 태그·일반 텍스트인 경우, 외부 ID, 비교 방향 전환, 재비교와 다중 항목을 검증한다. 실제 940/500px 화면 및 실행 전후 EditorWindow/native frame/포커스 보존과 소유창 finally 정리를 확인한다. 정상 카탈로그는 불변이며 실제 API 호출·카탈로그 변경·개인정보 처리는 없다. 성공 화면과 자동 test 결과, 최종 이미지 직접 확인은 구분한다.
