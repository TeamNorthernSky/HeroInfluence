# 전투씬 우상단 네 버튼 — 1번 단위 구현

- 사용자 승인: 우상단 네 버튼 구현. 기존 도주/배속/자동 버튼은 재사용 가능하면 리소스를 교체하고, 불가능하면 신설 후 기존 비활성. 이번에는 모두 재사용 가능했다.
- 이전 합의: C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/JC/Docs/20260913-battle-ui-plan-review-2da3cc57f2.md.
- 기준: JC, HEAD d3c0316e. 시작 시 TmpBattleScene Edit Mode, 미저장 변경 없음. 기존 UI_ReusableVisuals 미추적 상태 보존.

## 구현

- TmpBattleScene/BattleSceneCanvas/TopRightButtons 신설. 우상단 anchor/pivot, 위치 (-56,-56), 크기 160×299.
- 순서: MenuButton, Button_Auto, Button_Speed, RunButton. 각 버튼 160×65, 세로 간격 13. Scale=(1,1,1), 자동 LayoutGroup 없음. 에디터에서 직접 위치/크기 변경 가능, 런타임 배치 코드 없음.
- Button_Auto/Button_Speed/RunButton은 기존 오브젝트와 실행 컴포넌트를 보존하여 새 부모로 이동했다. 새로 복제한 옛 버튼이나 삭제 대상 없음. 기존 ButtonController는 두 스킬 버튼이 남아 있으므로 유지.
- 네 이미지는 이미 프로젝트에 있던 Assets/_Ui_Sprites/BattleScene/UI_box_button_menu/auto/speed/escape.png 사용. SVN 복사/원본 수정/import 설정 변경 없음.
- 기존 ToggleButton, AutoBattleToggleButton, BattleSpeedToggleButton, RunButton, 호버 효과를 사용한다. 초기 상태/배속 값/전투 호출 코드는 그대로이며 scene 참조를 명시적으로 연결했다.
- 원본 새 자동/배속 이미지는 활성용 별도 이미지가 없다. normal/active 양쪽에 새 이미지를 연결하고, 기존 SelectedSkillHighlight 컴포넌트를 ActiveState 자식에 배치해 켜짐 표시를 담당시켰다. 보존한 UI_ReusableVisuals/SelectionFrame.prefab을 씬에 배치해 연두 테두리로 사용한다. 자식 배치로 포인터 호버 때도 켜짐 표시가 사라지지 않는다. 두 공용 스크립트/원본 프리팹은 수정하지 않았다.
- 새 C#은 Assets/_ProtoType_Merge/JC/Scripts/UI/Battle/BattleMenuButton.cs 하나. Button 클릭 listener를 등록/해제하고 공용 SystemMenuController를 찾아 OpenMenu 호출. 한국어 Inspector Tooltip 포함. 배치/정지/메뉴 로직을 복제하지 않는다.
- SystemMenuController, BattleManager, InputHandler 및 기존 버튼 C# 파일은 변경하지 않았다. 나머지 UI 2~7도 변경하지 않았다.

## 검증

- Unity에서 새 컴포넌트 컴파일/부착 성공. 컴파일 오류 없음. 임시 PreviewScene 도구의 RenderTexture 해제 순서에서 오류 로그 2건이 발생했으며, 카메라 연결 해제 후 텍스처를 해제하도록 scratchpad 도구를 수정했다. 재렌더 후 새 오류가 추가되지 않음을 확인했다. 기존 로그를 지우지는 않았다. 전투 코드 오류와 구분한다.
- 저장한 씬을 다시 열어 4버튼 수, button/토글/이미지/켜짐 표시 참조, Scale=1, LayoutGroup 없음 검증 통과. Play=False, 씬 clean.
- 기존 세 버튼의 씬 오브젝트 ID 보존, 삭제된 YAML 오브젝트 없음 확인. 버튼·호버 및 부모 계층 외 변경은 제거했다.
- 비플레이 PreviewScene에서 실제 버튼 Image/RectTransform을 복제해 배치 렌더링 확인. 미리보기 C:/Dev/_scratchpad/battle-top-buttons-fb21da9279/buttons-preview.png. 실제 전투 Game View 스크린샷이나 플레이 동작 검증은 아니다.
- 씬 저장/재열기 도구 실행 과정에서 폰트 에셋 자동 직렬화 및 무관한 기본 필드 3개가 추가됐다. 시작 시 clean인 증거와 원본으로 대조해 폰트 2개(작업 디렉터리 CRLF 포함) 및 BattleSceneManager/EnemySpawner 블록을 원복했다. 최종 git status에서 폰트 수정 표시가 사라졌고 변경한 버튼과 신규 오브젝트만 남겼다.
- git diff --check 완료. 프로젝트 테스트 스크립트 신설 없음. 검사용 C#/백업은 커밋 폴더 밖 scratchpad에만 저장.
- 에이전트 플레이 테스트/커밋/push 없음. 후속 사용자 답변(2026-09-13): 플레이모드에서 정상 작동하며 요구 조건을 충족한다고 확인함. 우상단 네 버튼 검수 완료. 호버 효과·배속 인디케이터 품질 향상은 별도 후속 작업.

## 변경 파일 및 후속

- 수정: Assets/_ProtoType_Merge/Scenes/TmpBattleScene.unity.
- 신설: Battle/BattleMenuButton.cs 및 .meta, Battle.meta. 추가 프리팹 파일 없음.
- UI_ReusableVisuals는 이전 보존 자산이며 이번에 새 생성한 것이 아니다. ActiveFrame이 해당 SelectionFrame 프리팹을 참조한다.
- 삭제/비활성화해야 할 옛 버튼 없음(기존 3개 모두 재사용). 공용 기존 프리팹·스크립트 삭제 없음.
- Unity에서 TopRightButtons를 선택한 상태로 마쳤다. 다음 단위는 사용자 요청 후 진행하며 2~7을 자동 착수하지 않는다.
- 현재 구현 구조의 지속 갱신 문서: C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/JC/Docs/BattleSceneUI.md. 이 작업일지는 변경 기록이며, 현재 구조 파악은 해당 문서를 먼저 읽는다.
