# BGHigh002 · BGStore004 SVN 교체 및 비교 구역

2026-09-29 사용자 요청 적용 완료.

- SVN 원본: `D:/SVN/4_Resources/★ Alpha 02/3D/Building`의 두 FBX와 Texture 폴더의 같은 이름 JPG.
- 추가 에셋: `Assets/_ProtoType_Merge/DH/AlphaAsset/AIAsset/Building/JC_Mod/SVN_20260929/`.
- 기존 배치 BGHigh002 19개, BGStore004 7개(총 26개)를 SVN 버전으로 교체. 실행 중 배치에 사용하는 DecorativeBuildingPrefabCatalog_JC의 두 항목도 교체.
- 12개 관련 씬을 Unity 편집 모드에서 열어 연결과 색상 조절 대상 수를 검증. 변경된 씬은 11개(공용 색상 프로필 연결 변경 포함).
- z_JC_BuildingColorTestbed의 `JC_SVN_Comparison`에 비교용 4개 추가. 기존 갤러리 오른쪽, 루트 위치 (45, 0, 3). 왼쪽 JC, 오른쪽 SVN이며 앞줄 BGHigh002, 뒷줄 BGStore004.
- 비교용 네 건물은 별도 재질·정의·프로필을 사용하므로 색상 조절이 서로 또는 실제 배치에 영향을 주지 않음.

## 보존과 배치 기준

SVN FBX/JPG는 원본과 바이트 일치. 배치용 파생 메시에서 원점과 바닥 높이만 평행 이동했으며, 토폴로지·UV·크기는 원본과 동일. 기존 씬의 Transform 위치·회전·스케일 변경 없음. 바닥은 로컬 높이 0으로 맞춤.

JC 제작본의 FBX, Blender 저장본, 재질, 색상 정의 및 기존 저장 프로필은 보존. 현재 SVN 기본색용 프로필은 별도 `BG_All_SVN_Current.asset`으로 저장. Bank·HeroAssociation·VillainUnion의 원본 롤백 상태도 유지.

## 색상 기능과 검증

SVN 메시의 면과 기존 분류의 위치·UV를 대응시켜 색상 마스크를 이식. 4,156개 / 8,976개 삼각형 모두 대응, 불일치·모호한 대응 0. 이는 모든 장식을 별도 분류했다는 의미는 아님. 기존 분류에서 보호된 간판·장식 등은 납품 텍스처 색을 유지(High002 103면, Store004 6,810면).

색상 영역별 GPU 출력, 다른 영역의 변화 0, 기존 프로필의 파츠 ID 불러오기, 비교 구역 네 조절기의 대상 각각 1개를 확인. SVN 기본색과 원본 비교 전환은 기존 조절기에서 사용 가능.

원본/JC 에셋 및 프로필 보존, 기존 씬 레코드 삭제 0, 기존 Transform 변경 0, 생성 카탈로그 두 항목, 컴파일 오류 없음, 열린 테스트씬 저장 상태 확인. Play 모드는 실행하지 않음.

검증 자료: `C:/Dev/_scratchpad/svn-comparison-538d030710/`의 scene-verification.tsv, color-verification.txt, preservation.json, comparison.png. 작업 직전 파일은 before.zip에 보관.
