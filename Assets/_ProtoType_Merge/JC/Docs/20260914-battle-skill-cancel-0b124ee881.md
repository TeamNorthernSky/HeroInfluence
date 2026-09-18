# 전투 스킬 선택 취소 구현

- 작업 ID: 20260914-0b124ee881
- 저장소: C:\Dev\HeroInfluence, 브랜치 JC.
- 승인: 사용자 「구현 시작해주세요」. ASB InputHandler, KJ SkillButtonController, 전투씬 버튼 이벤트, 구조 문서에 한정.
- 상태: 구현 및 비플레이 검증 완료, 사용자 플레이 검수 대기.

## 최종 동작

- 같은 스킬 재클릭은 기존 재선택 유지.
- 다른 활성 스킬 버튼은 기존 선택을 먼저 비운 뒤 기존 검사로 새 선택. 거절되면 미선택.
- 잠금·미결선 버튼은 왼쪽 클릭으로 선택 취소만 수행. 4스킬 실행 결선은 여전히 제외.
- 우클릭은 기존 대상 선택 취소 경로 사용. 공격 실행 중 및 자동전투에서는 사용자 취소 무시.
- 하단 다른 영역 클릭은 선택 유지. 턴 경계/완료 강제 초기화는 실행 중 제한 없이 기존대로 수행.
- 공격 실행·피해량·장착·턴 진행·단축키·메뉴 로직 변경 없음.

## 변경 파일

1. Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs: 사용자 입력용 TryCancelSkillSelection() 추가, 기존 우클릭을 이 함수로 연결. 프로토타입 취소 제거 검토 주석은 확정 사양에 맞게 수정.
2. Assets/_ProtoType_Merge/KJ/Scripts/UI/SkillButtonController.cs: 다른 스킬 선택 전 초기화 및 거절 시 중단, OnUnavailableSkillClicked(BaseEventData) 추가. 이 콜백은 PointerEventData.pointerClick의 비활성 Button에 대한 왼쪽 클릭만 처리.
3. Assets/_ProtoType_Merge/Scenes/TmpBattleScene.unity: 히어로 4버튼 및 코어 버튼에 EventTrigger.PointerClick -> 위 콜백 연결.
4. BattleSceneUI.md: 실제 연결 구조·입력 조건·검증 상태 갱신. 기존 배치 수치가 최초 구현 기준임을 명시.

새 프로젝트 C# 파일 없음. BattleManager/BattleFlowManager/ToggleButton/공용 툴팁 및 리소스 수정 없음.

## 검증

- 작업 전 Unity: Edit, DHScene_3, dirty=false. Play 진입·조작 없음.
- Unity 리프레시 후 새 함수를 사용하는 에디터 실행 코드 컴파일 성공. 최근 Error 로그 없음.
- 저장 후 다시 연 씬과 격리 PreviewScene 검증 43항목 PASS: 5개 직렬화 이벤트 연결, 비활성 버튼 실제 PointerClick 전달, 활성/오른쪽 클릭 중복 방지, 코어 데이터 부재·IP 부족 전환의 기존 선택 제거, 실행 중/자동전투 취소 거절 및 토글 표시 유지, 턴 경계 강제 초기화, 이후 취소 복구, IP/장착/행동유닛 유지.
- 씬 블록 비교: 기존 5개 GameObject에 컴포넌트 참조 한 줄씩 추가, 신규 MonoBehaviour 5개, 삭제 0. 모든 기존 Transform/RectTransform과 그 외 오브젝트 블록 동일. 사용자 배치 유지.
- 임시 검증은 실제 시전이나 전투 진행을 실행하지 않음. 사용자의 플레이 검수 필요.

## 재현 자료

C:\Dev\_scratchpad\battle-skill-cancel-0b124ee881에 이번 4개 대상 파일의 시작본과 edit.py, wire.cs, verify.cs, diff.py, document.py, scene-diff.txt 보관. 시작본은 이전 UI 개편을 포함한 당시 작업 파일이며 이번 작업으로 다른 변경을 롤백하지 않았다.

검증 실행: 기존 로컬 Unity MCP RPC helper C:\Dev\_scratchpad\battle-ui-d932143567\rpc.py script에 verify.cs 경로 전달. Unity 담당권 및 Edit 상태 확인 후만 실행한다. 플레이 테스트나 리소스 복사·Git 커밋·push를 수행하지 않았다.
