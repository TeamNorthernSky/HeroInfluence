탐사 외형 보강 — JC_Testbed 전용 1차 실험

관리 위치: JC_WorldDressing > JC_ExploreDetail. 기존 WorldDressing 프리팹에 자식으로 저장되어 있습니다. 부모와 자식이 활성일 때 DHScene_3에 적용됩니다.
메뉴 JC > 환경 > 탐사 외형 보강 설정에서 JC_ExploreDetail_Default 에셋을 선택합니다.
Enable Details OFF 또는 보강 컴포넌트 비활성화: 원본 렌더링 복원.
도로 표면 / 보도 라운딩 / 보도 연석 / 야지 연석을 개별 조절할 수 있습니다.
플레이 중 변경을 유지하려면 설정 저장 버튼을 누릅니다. 씬 저장은 필요하지 않습니다.

DH 원본 TileChunk 생성 결과를 읽어 표시용 복사 메시와 재질만 생성합니다.
원본 MeshFilter, MeshCollider, 지면 높이·통행·이벤트 데이터는 변경하지 않습니다.
원본 차선·횡단보도·보도 텍스처 유지, 도로 입자/노멀 추가, 보도 외곽만 최대 기본0.025 라운딩.
보도 및 잔디(tilemap_04)와 Road_ 타일의 경계에 연속 연석을 배치합니다. 물(tilemap_02)은 제외합니다.
기존 RoadDetailKit의 연석 메시/재질과 노멀 텍스처를 재사용합니다. 의존 에셋을 함께 유지하세요.
생성 결과는 저장하지 않는 임시 외형이며 컴포넌트 종료/설정OFF/어셈블리 재로드 때 원복합니다.
기존 DH 메시 생성기가 청크를 교체하면 변경을 감지하여 다시 구성합니다.

1차 지원 범위: 현재 DH 축 정렬 사각 타일 메시 형식, Road_ 텍스처, tilemap_01 RaisedCube 보도.
도로 폭, 차선 배치, 횡단보도 위치, 산·건물·식생은 원본 그대로입니다.
콜라이더는 원본이므로 작은 시각적 라운딩/연석은 이동 지형을 새로 만들지 않습니다.
검증: ON/OFF3회 및 컴포넌트OFF 복원, 원본282청크/73충돌체 참조 보존, 정적 원경·근경ON/OFF.
플레이 성능·탐사 동선 검수는 사용자 확인이 필요합니다.

수정: 보도와 연석은 같은 평면 외곽선에서 서로 겹치지 않는 영역으로 생성합니다. 도로와 만나는 볼록 코너는 기본 반경0.28의 평면상 라운딩으로 처리하며 잘린 부분은 도로 높이로 채웁니다. Corner Radius와 Curb Tint로 조절합니다. 연석 색상은 탐사 전용 복사 재질에만 적용됩니다.

줄눈: Joint Width(기본0.008), Curb Block Length(기본0.5), Joint Tint로 조절합니다. 보도-연석 경계와 블록 사이 면을 분할하여 중첩 없이 표현합니다. 자식 Inspector에 프로필 편집기를 표시합니다. WorldDressing OFF 상태에서는 수동 갱신도 원본을 복원합니다.

평탄한 풀밭: 도로와 높이 차이가 Flat Boundary Tolerance(기본0.03) 이하이면 Flat Grass Curb Height(기본0.012)의 낮은 연석을 사용합니다. 높은 보도와 낮은 풀밭이 맞닿으면 보도 쪽에만 연석 띠를 만듭니다. 줄눈 기본색은 회색0.46입니다.

도로-풀밭 자연 경계: Natural Road Edges를 켜면 낮은 연석 뒤 얇은 흙띠와 드문 풀을 표시합니다. Soil Edge Width/Soil Edge Tint/Edge Grass Tint로 조절합니다. 흙길 입구는 원본 ParkRoad 이미지의 흙길 부분만 연석을 생략하고 Path Apron Length만큼 연석의 풀밭 쪽 안쪽에서 정리합니다. 연결 면은 도로로 확장하지 않습니다.
DH LevelTileRegistry의 ParkRoad 텍스처 17종에서 가장자리 흙색 구간을 분석해 프로필에 저장했습니다. 현재 축 정렬 DH 메시 UV 기준이며, 새로운 이미지/아틀라스/회전 형식은 재검토해야 합니다. 여러 분리된 입구가 한 면에 있는 이미지에는 분석 오류로 알립니다. 미분석 흙길은 추측 배치하지 않습니다. 원본 이미지 변경 시 '흙길 타일 경계 다시 분석' 후 설정을 저장합니다.
JC_ExploreDetail Inspector의 '도로-풀밭 변경 위치 (월드 X/Z)' 목록에서 '보기'를 누르면 Scene 뷰가 해당 경계로 이동합니다. 생성 자식 '흙길 입구 01~15'는 위치 확인용 임시 표시이며 게임 오브젝트나 충돌체를 추가하지 않습니다.
2026-10-10 검증 맵: 경계293개(타일 한 변 단위), 흙길 입구15개, 미분석0/높이차 제외0. 전체좌표는 C:/Dev/_scratchpad/explore-18f3572ef3/road-boundaries.tsv. 원본284청크 메시/재질 및73충돌체 참조 보존, ON/OFF3회 원복 통과. 물가/수면은 변경하지 않습니다.

연석 모따기: Curb Bevel 기본0.012. 높은 보도의 바깥 윗모서리는 한 단계 경사면, 낮은 풀밭 연석은 상단 네 모서리를 한 단계로 처리합니다. 낮은 연석은 높이의40% 이하로 자동 제한합니다. 공용 원본 연석 메시 에셋은 수정하지 않습니다.


곡면 음영: 90도 코너16분할과 동일 위치의 유사 방향 노멀 연결로 면별 명암 끊김을 줄입니다. 모따기 경계는 유지합니다. 진입로는 Entrance Paving Tint(기본0.59) 회색 포장 띠에서 야지로 페이드하며, Path Apron Length는 안쪽 전이 길이입니다.

진입로 최신 구성: 앞쪽 Entrance Road Fraction(기본0.5)은 Road_01 도로 재질·색상과 URP 조명으로 연결합니다. 남은 연석 폭 안에서만 흙길로 페이드합니다. 양끝에 모따기 표시 블록을 배치하며 Entrance Marker Width/Entrance Block Tint로 조절합니다. 과거 회색 포장띠/연석 안쪽까지 확장하던 전이 방식은 이 구성으로 대체했습니다.

진입로 표시블록 마감: Entrance Marker Width 기본0.03, Entrance Marker Height 기본0.024. 낮은 기본연석보다 최소0.008 높게 적용합니다. 기존 연석 끝은 표시블록과 만나는 쪽 모따기를 제거하고 접합간격0.0002를 두어 겹침 없이 밀착합니다. 일반 연석 사이 줄눈 설정은 유지합니다.

입구블록 축선: 흙길과 풀밭 경계선에 블록 중심을 정렬합니다. 기존 연석은 표시블록 반폭만큼 더 짧게 생성하여 접합 간격0.0002를 유지합니다.


수로 보강 (2026-10-10)
JC_WorldDressing > JC_ExploreDetail의 프로필에서 Canal Banks / Cartoon Water를 독립 제어합니다. Enable Details OFF는 DH 원본으로 복원합니다.
수면 tilemap_02를 식별하고 실제 육지와 접한 변에만 호안을 만듭니다. 상단 마감, 콘크리트 경사, 젖은 하단부를 단일 분할 메시로 생성하며 가려지는 원본 육지 측면은 시각 복제본에서 제외합니다. 원본 메시/충돌체/레지스트리는 수정하지 않습니다.
Bank Width는 수면 쪽 폭이며 0.45셀 이하, 기본0.4입니다. Bank Cap Width, Bank Tint, Bank Panel Length, Bank Joint Strength, Bank Wet Strength로 외형을 조절합니다. Bank Excluded Land Cells는 월드 X/Z 육지 셀 중심 좌표입니다.
Water Flow Direction/Speed, Water Wave Size/Strength, Water Tint/Edge Tint/Shine/Edge Width로 물을 조절합니다. 불투명 수면이며 실제 수심/유체 시뮬레이션은 없습니다. 월드 좌표 물결이라 타일 무늬가 이어집니다. 물가색은 지형 경계까지의 거리 기반 연출입니다.
프로필 인스펙터의 설정 저장으로 에셋에 보존합니다. 수로 적용 위치 목록의 보기 버튼으로 경계를 찾습니다. 씬 자동 저장 및 플레이 조작은 하지 않습니다.
재사용: CalmCanalWater.shader / CanalBank.shader는 다른 씬의 머티리얼에도 사용 가능합니다. 수면 TEXCOORD1.x는 물가까지 거리, 호안 TEXCOORD1.y는 상단0~하단1입니다. Waterfront.cs는 기존 탐사 보강 시스템의 부분 클래스로 타일 연결·생성을 담당합니다. 원본 DH 타일 레지스트리를 대체하지 않습니다.
검증: 현재15수면청크/220호안변. 네 가지 ON/OFF 조합, 전체 복원3회, 원본284청크/73콜라이더 참조 보존 통과. 에디트 모드 렌더에서4초 흐름 차이 확인. 실제 Play 및 성능 프로파일링은 별도입니다.
현재 진입로 표시 블록의 높이/색상은 일반 낮은 연석 설정과 공유하며 과거 별도 높이/색상 설정은 비활성 호환 필드입니다.

교량 하부 수로: Bridge Water Passage를 켜면 실제 Bridge 오브젝트 범위 안에서 양쪽 수면을 잇는 도로 하부에만 연결 수면을 생성합니다. 도로 상판 및 원본 이동/충돌 판정 유지. Bridge Deck Thickness는 시각적 바닥 두께(.1)입니다. 현재18셀 연결, 호안206변. 물색은 녹색을 소폭 낮춘 푸른 청록색으로 조정했습니다.

물결 추가 설정: Water Ripple Size/Strength/Speed는 잔물결 간격/강도/일렁임 속도입니다. Water Flow Streak Strength는 흐름 방향의 긴 무늬 강도입니다. 두 강도를0으로 설정하면 기존 큰 물결만 남습니다. 원거리에서는 잔무늬를 줄여 깜빡임을 억제합니다.

수면 최신표현: 이전 흰선/흰능선 패턴은 제거했습니다. 월드좌표의 불규칙한 기울기와 환경반사로 움직임을 표현합니다. Water Flow Variation은 큰 저대비 색변화이며 과거 Flow Streak Strength의 표시명 변경입니다. 모든수면16렌더러(교량하부포함)는 동일머티리얼1개/시간/월드좌표를 공유하며 타일별무늬초기화가 없습니다. 흐름방향은 현재 프로필의 공통방향입니다. 참고원리: https://halisavakis.com/my-take-on-shaders-stylized-water-shader/ , https://catlikecoding.com/unity/tutorials/flow/texture-distortion/ .

[2026-10-10 수로 연결 흐름]
Water Follow Canal: 연결 수역의 하류까지 셀 거리에서 방향 지도를 생성합니다. 공통 월드 좌표 + RGHalf 지도 + 두 위상 혼합으로 타일 경계와 누적 왜곡을 줄입니다. 교량 하부 포함. 세부 기술문서: ../../Docs/JC_CanalWater_Technical.md
