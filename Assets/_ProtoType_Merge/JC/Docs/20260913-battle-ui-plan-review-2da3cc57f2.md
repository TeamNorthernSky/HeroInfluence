# 전투씬 UI 개편 합의 수정 및 정리 후보

- 기준: 2026-09-13, HeroInfluence/JC, HEAD d3c0316e. 프로젝트 변경 없음. 분석·기록만 수행.
- 사용자: 이전 1~7 계획 중 이번에 지적하지 않은 항목은 동의. 이번 메시지는 답변 검토 요청이며 일괄 구현 착수 요청으로 해석하지 않음.
- 이 기록은 이전 20260912-battle-ui-restart-7a50f3e001.md와 20260913 계획 응답의 상충 부분을 대체한다. 이전 폐기된 전체 HUD 구현은 재사용하지 않는다.

## 현재 합의

1. 고정 UI 1~7을 작은 단위로 진행한다. 기존 제안의 신규 파일 BattleMenuButton/BattleSkillDescriptionPanel/BattleEnemyInfoTooltip 및 기존 컴포넌트 중심 수정 방향은 동의 상태. 후속 사용자 확정: 메뉴 버튼은 일시정지+메뉴 호출만 필요하며 ESC의 다른 분기를 가져오지 않는다. 기존 OpenMenu를 그대로 사용한다. SystemMenuController.cs는 수정 대상에서 제외한다.
2. 씬에 UI를 미리 배치한다. 위치·크기·내부 배치는 Inspector RectTransform으로 조절 가능해야 한다. 후속 사용자 확정: 개편 대상 UI의 Scale 크기 조정 및 HorizontalLayoutGroup 자동 배치는 모두 없앤다. localScale=(1,1,1)로 정리하고 필요한 크기는 Width/Height, 앵커/오프셋에 반영한다. 고정 배치를 런타임 코드/레이아웃 그룹으로 덮어쓰지 않는다. 즉시 삭제 보류 합의에 따라 자동 배치 컴포넌트는 우선 비활성화한다. 비활성 보존하는 옛 비교용 UI는 원형을 보존하고, 개편된 실사용 UI에 이 기준을 적용한다. 유지하기로 한 월드 HP바, Canvas 자체의 엔진 관리 스케일, 타 화면 공용 UI를 일괄 수정하는 권한으로 확대하지 않는다.
3. 좌하단 기준은 현재 행동 유닛. 랭크는 이름 우측의 임시 고정 위치. 이름·HP바·IP 표시는 신규 기획안으로 구성. 기존 이름·HP·IP 오브젝트는 씬에 비활성으로 보존한다. 유닛 위 HP바 유지 지시는 계속 유효하다.
4. 우하단: 사용 가능한 스킬 호버 → 선택 스킬 → 스킬 선택 안내. 안내 제목 '스킬 선택', 본문 '사용할 스킬 버튼 또는 코어 스킬을 클릭하세요.'
5. 적 턴은 '적 행동 중'만 표시. BattleManager.cs를 편집 대상에서 제외한다. 적 스킬 데이터·시전 시작 알림 추가는 ASB 협의 이후 별도 작업이다.
6. 아군 초상화 호버는 보류. 적 셀/적 턴 초상화 툴팁은 전투 Canvas에 임시 패널만 구현하는 이전 범위 유지. 공용 SkillTooltip/DDOL은 유지한다.
7. 매 시전 수동 선택, 코어스킬은 기존 무기 실행 경로, 미결선 슬롯은 새 실행 구현 제외, 강화 단계 불일치는 기록만 한다. InputHandler 최소 수정 제안은 이번 이의 제기에 포함되지 않았다.
8. 모든 삭제 후보는 기록만 하고 삭제하지 않는다. 교체된 옛 UI는 비활성으로 남긴다. 작동하는 새/옛 컨트롤러가 동시에 같은 표시를 갱신하지 않게 배치/연결한다. 정리는 전투 UI 완료 후 사용 씬 확인 → 사용 씬 개편/미사용 씬 삭제 → 참조 검사 → 사용자 검증 후 일괄 진행한다.
9. 원본 Excel은 읽기 전용. Bar/Stat은 이번 사용자 메시지에서 복사 사용을 지시했으나 이번 검토에서는 복사하지 않았다. 실제 구현의 복사 대상/목적지는 해당 단위에서 명확히 남긴다. 그 밖의 과거 SVN 리소스 복사 제한을 포괄 해제한 것으로 해석하지 않는다.

## OpenMenu 조사 결과

- C# 호출은 전체 Assets 검색 기준 두 곳: JC/Scripts/UI/HQLobbyMenuController.cs:146, TopBarButtons.cs:21. 선언은 SystemMenuController.cs:71. UnityEvent의 OpenMenu 직렬화 호출은 Assets의 씬/프리팹/asset 검색에서 찾지 못했다.
- HQLobbyMenuController는 UI_HQLobbyScene/UI_Shell.prefab에, TopBarButtons는 UI_Common/TopBar.prefab 및 TopBar 1.prefab에 연결된다. 현재 런타임 활성 인스턴스 개수는 플레이로 확인하지 않았다.
- OpenMenu 신설: 01ac5ec0, 2026-06-17, rock-oon(JC 계정), '본부 로비씬 UI 프로토타입 빌드용 커밋'. 동일 커밋에 로비 옵션 버튼 호출 추가. 주석도 '외부(로비 옵션 버튼 등)에서 시스템 메뉴 모달을 연다'이다. 계정이 실제 타이핑 주체를 증명하지는 않는다.
- TopBarButtons 호출 도입: f7782830, 2026-06-30, rock-oon. 로비/탐사 공유 상단 바 모듈화. 이 커밋에는 Claude 공동 작성 표기가 있다.
- 앞선 분기: c981d9b4, 2026-05-06, rock-oon에서 로비 OpenModal / 다른 씬 TogglePause 분리. 로비에서 일시정지가 없어야 하는 구체적 기획 근거는 확인한 주석·커밋 설명에 없다.
- 현재 SystemMenuModal 원본 프리팹의 Modal.pausesGame=0이나 Resources/CommonUIManager.prefab 내 메뉴 인스턴스가 pausesGame=1로 덮어쓴다(326행 부근). 이 설정은 90f48d85, 2026-06-29 분리 커밋에 이미 있다. 메뉴 프리팹 GUID 직접 사용처는 CommonUIManager.prefab 하나를 찾았다.
- 실제 공용 구성: OpenMenu → OpenModal → _modal.SetActive(true) → Modal.OnEnable → ModalPauseGate.Refresh → Time.timeScale=0. 따라서 'OpenMenu는 일시정지 없이 메뉴만 연다'는 앞선 설명은 부정확하며 정정한다.
- OpenMenu를 비정지용 API라서 폐기해야 한다는 근거는 없다. 후속 사용자 답변으로 유효한 API임을 확인하고 그대로 사용하기로 확정했다.
- 종전 'ESC와 완전 동일' 요구는 후속 답변에서 일시정지+메뉴 호출로 한정됐다. 따라서 ESC의 타이틀/엔딩 가드, 대화 처리, 최상위 모달 닫기, 빌드모드 종료를 버튼에 이관하지 않는다. 기존 OpenMenu 호출로 충족하며 SystemMenuController 수정 불필요.

## RectTransform 조사

- 조사 범위: TmpBattleScene 저장본, Battle_Scene UI 프리팹, TurnSlotUI, 공용 메뉴·호스트 및 SkillTooltip. 정적 YAML/관련 C# 조사이며 현재 에디터의 미저장 상태·실행 후 transform 값은 조사하지 않았다.
- 원시 목록: C:/Dev/_scratchpad/battle-ui-review-2da3cc57f2/ui-transform-audit.json. 목록은 직렬화 원본값과 override를 나눠 기록한다. 프리팹 변형의 최종 상속값과 동일시하지 않는다. 씬 override에는 조사 출력상 3D 배경도 섞여 있으나 UI 문제로 분류하지 않는다.
- TmpBattleScene 주요 고정 UI의 직접 배치된 자식 RectTransform은 (1,1,1). 씬 안 HPCanvas Variant의 해당 scale override도 1이다.
- BattleSceneCanvas.prefab(미사용 후보)의 Portrait=1.8, HeroInfo=2.4, RunButton≈0.71123. Auto/Speed=(1.2347999,0.71717006,1)로 비균일이다. 현재 씬 Canvas는 이 프리팹 인스턴스가 아니므로 현 전투씬 값으로 보고하지 않는다.
- HeroInfo.prefab 원본: Portrait≈(1.3104,0.6552,0.6552), IP/EXP Value≈(1.00885,0.51713,1), IP/EXP 라벨에도 0.75 스케일. 결과용 변형 프리팹은 일부를 1로 덮어쓴다.
- ResultPanel.prefab: HeroIndex≈(0.5,1,1), Victory/Defeat Image=(2,1,1). 현재 BattleUIManager의 직접 생성 대상인 Result_VictoryPanel/Result_DefeatPanel과 구분한다.
- HeroInfoResult.cs 및 SkillSelectionPanel.cs가 생성 초상화에 localScale=0.75를 적용한다. 결과/습득 화면은 이번 고정 UI 범위 밖이므로 기록만 한다.
- TurnArrow RectTransform=(0.03,0.03,0.012), HPCanvas 원본=(0.02,0.02,0.02)는 월드 표시 용도와 함께 판단해야 하므로 단순 실수로 판정하지 않는다.
- Canvas 루트 여러 개에 저장 scale=(0,0,0)이 있다. 자식 UI의 수동 크기 조정과 구분하며 저장값만으로 고장/수동실수라고 판정하거나 일괄 1로 바꾸지 않는다.
- TurnOrderPanel/SlotParent에 활성 HorizontalLayoutGroup이 있다. 개편 시 슬롯별 수동 위치 편집을 보장하려면 자동 정렬을 해제한다. 컴포넌트 삭제는 보류 규칙에 따라 후속 정리 후보로 둔다.
- 신규 고정 UI는 매 프레임 위치/크기 재설정 금지. 커서 추적 툴팁은 동적 위치와 별개로 크기·내부 배치·커서 오프셋을 편집 가능하게 한다. 전역 공용 툴팁 자동 레이아웃을 이번에 무단 개편하지 않는다.

## 리소스 확인

- 제공 경로 D:/SVN/4/_Resources/...는 현재 존재하지 않는다. 실제 경로 D:/SVN/4_Resources/★ Alpha 02/2D/UI/Icon/Bar 및 Stat을 발견했다.
- Bar: UI_bar_fill(HP).png, UI_bar_fill(Exp).png, UI_bar_frame.png.
- Stat: UI_icon_IP.png, UI_icon_hp.png 및 atk/critical/def/reduceDamage/revenge/speed.
- HP fill과 IP 아이콘을 이미지로 읽어 확인했다. 원본 수정·복사·Unity import 없음.

## 삭제/이관 후보 대장 — 전부 미실행

| 대상 | 최종 처리 전 조건 | 현재 처리 |
|---|---|---|
| TmpBattleScene/BattleSceneCanvas/ButtonController | 버튼 이동 후 남는 내용과 외부 참조 확인 | 유지, 향후 빈 부모 정리 후보 |
| HeroInfoWindow/HeroInfo 등 기존 하단 정보 묶음 | 새 표시 사용자 검증, 오브젝트/컴포넌트 참조 이관 | 기존 이름/HP/IP 포함 비활성 보존 예정 |
| HeroInfo의 비활성 HeroInfoPanel 컴포넌트 | 새 정보 컨트롤러 단일 동작 확인 | 삭제 보류 |
| Button_Skill 및 Button_Skill_2의 비활성 ToggleSkill | 새 선택 동작과 타 씬 참조 확인 | 삭제 보류 |
| TurnOrderPanel/SlotParent의 HorizontalLayoutGroup | 미리 배치한 5슬롯의 수동 편집 검증 | 개편 시 비활성화 후 삭제 보류 |
| Resources/UI_Prefab/Battle_Scene/BattleSceneCanvas.prefab | GUID·동적 로딩 미사용 확정, 사용자 검증 | 미사용 후보, 삭제 보류 |
| BattleSimulationScene_Legacy.unity | 사용자/팀의 실제 사용 여부 판단 | 사용이면 개편 이관, 미사용이면 최종 삭제 후보 |
| ToggleSkill.cs/기타 옛 전투 UI 스크립트 | 사용 씬·프리팹 이관 후 참조 0 확인 | 현 시점 파일 삭제 확정 없음 |
| HoverTooltip.cs | 직렬화 참조뿐 아니라 GetComponent 등 코드 참조 정리 | 현 시점 파일 삭제 확정 없음 |
| OpenMenu() | 후속 사용자 검토 완료 | 유지 확정, 삭제 후보에서 제외 |

- 삭제 대상이 아닌 유지 자산: 공용 SkillTooltip/DDOL, UnitHPBar, TurnSlotUI 원본, 보존한 UI_ReusableVisuals, 현재 결과/스킬 습득 화면. SkillDescriptionBuilder는 스킬 습득 UI가 사용하므로 미사용 코드로 분류하지 않는다.
- 코드·씬 변경/컴파일/플레이 검증 없음. Git 미추적 UI_ReusableVisuals 상태는 이전과 동일.

## 마지막 계획 점검

- 1~7 구현 착수에 앞서 새로 필요한 기획 결정은 현재 확인되지 않았다. 메뉴·배치 정책 미결정은 해소됐다. 구현은 한 단위씩 사용자가 추적하는 기존 방식으로 진행한다.
- 구현 검증에 포함: 메뉴 열기/닫기 정지·재개와 배속 설정 유지, 사용자 수동 배치가 실행 후 유지되는지, 새/옛 정보 컨트롤러 중복 갱신 방지, 시전/취소/턴 전환 시 입력 선택·버튼·설명 동기화, UI 클릭의 전투 대상 관통 방지.
- 보류는 결정 누락이 아니다: 적 실제 스킬 설명/ASB 협의, 미결선 스킬·강화 불일치, 아군 초상화 호버, 단축키 개편, 결과·턴 팝업, 최종 미사용 정리. 과거 합의한 타일 색/범위 표시 및 스킬명 말풍선도 고정 UI 1~7과 분리한 후속 항목으로 남긴다.
