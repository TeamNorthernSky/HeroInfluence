# BGHigh002 · BGStore004 최신 JC 제작본 채택

2026-09-29 사용자 요청 완료.

- BGHigh002 19개, BGStore004 7개, 총 26개 기존 배치를 최신 JC 메시·재질·색상 정의로 교체.
- 12개 관련 씬을 Unity 편집 모드에서 열어 메시·재질·색상 대상 연결 검증. 생성용 DecorativeBuildingPrefabCatalog_JC의 두 항목도 최신 JC 프리팹으로 변경.
- 테스트씬 z_JC_BuildingColorTestbed의 JC_SVN_Comparison 구역 삭제. 비교용 4개 건물·받침·라벨·독립 조절기가 씬에서 함께 제거됨. 저장된 비교 프리팹과 SVN 에셋은 보관.
- BGHigh002의 높이·통일된 블록색·램프 수정, BGStore004의 사방 녹황녹적 간판·볼드 12/24·3색 독립 조절이 실제 배치에 반영됨.
- 현재 공용 색상 프로필은 BG_All_JC_Approved.asset. 기존 JC/SVN 프로필 파일은 보존.

## 보존 및 검증

기존 배치 Transform 변경 0. 테스트씬은 작업 시작 시의 미저장 상태를 별도 사본으로 보관하고 그 내용을 기준으로 수정하여 기존 사용자 변경을 보존함. 다른 모델과 저장 프로필 변경 없음. Bank·HeroAssociation·VillainUnion 원본 롤백 유지. 활성 배치에 SVN 두 모델의 참조가 없고 비교 구역이 없는 것 확인. 전체 26개 색상 대상 연결 정상, Store004 sign_green/sign_yellow/sign_red 항목 정상. 컴파일 오류 없음. 테스트씬 재오픈 및 저장 상태 확인. Play 모드는 실행하지 않음.

백업과 검증: C:/Dev/_scratchpad/adopt-two-524b33c05d/의 before.zip, live-before.unity, patch-report.json, verification.tsv, preservation.json.
