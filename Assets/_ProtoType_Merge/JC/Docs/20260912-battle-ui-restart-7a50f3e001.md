# 전투 UI 재시작 준비 — 기존 구조·기여 이력·작업 경계

- 요청: 기존 전투 UI의 Git 변경자와 영역을 확인하고, 기존 흐름/인터페이스를 유지하며 사용자가 이해할 수 있는 작은 작업으로 분할한다. 이번 단계는 읽기 분석·준비만이다.
- 기준: 2026-09-12 로컬 JC, HEAD `d3c0316e`. 원격 fetch는 하지 않았다. 로컬에 있는 이력을 기준으로 분석했다.
- 이전 개편 코드와 씬 변경은 사용자 디스카드로 제거된 상태다. Git에는 보존한 `Assets/_UI_Prefabs_Runtime/UI_ReusableVisuals`와 meta만 미추적 파일로 남았다. 이전 구현안은 재적용하지 않는다.
- HP바는 기존 것을 사용한다. 클릭할 수 없는 버튼의 툴팁은 추가하지 않는다. 새 스크립트/구조 변경은 필요성과 기존 대안을 먼저 확인받는다. 공용·타팀 파일은 사용자 사전 확인 후 변경한다.

## 결론과 이력 해석의 한계

기존 전투 UI는 KJ의 표시/버튼/결과 UI에 ASB의 전투 진행과 JC의 공용 툴팁·아이콘·연결 보완이 결합된 구조다. 폴더 이름이나 최근 커밋 작성자만으로 전담 소유권을 정할 수 없다. Git은 변경 이력을 보여주지만 현재 팀의 담당 합의나 실제 타이핑한 사람/AI를 증명하지 않는다.

기록상 계정은 KJ=chogangjin, JC=rock-oon, ASB=Bin9825로 대응한다. 사용자 설명, 영역, 커밋 내용/병합 기록을 함께 대조했다. 현재 업무상 담당권은 기존 합의를 우선한다.

`5b67e71f`(2026-05-04, rock-oon)는 Orora에서 가져온 초기 클론 커밋이다. 이때 처음 등장한 UnitHPBar/BattleUIManager/InputHandler 등은 원래 작성자를 이 저장소만으로 단정할 수 없다. 현재 줄의 작성자 비율은 원작성자/소유권 판정에 사용하지 않는다.

기존 코드가 재사용 불가능하여 전면 교체해야 한다는 근거는 없다. 이전 개편의 별도 HUD/HP바 구현은 필수적인 기술적 대응이 아니라 제가 택한 방식이었으며, 현재 재시작 기준으로 채택하지 않는다.

## 주요 영역과 변경 이력

경로는 `C:\Dev\HeroInfluence\Assets\_ProtoType_Merge` 기준이다. ‘협의 경계’는 편집 승인이 아니라 확인이 필요한 영역이다.

| 영역 / 파일 | 확인된 작업자·이력 | 협의 경계 |
|---|---|---|
| 현재 유닛 정보 `KJ/Scripts/UI/HeroInfoPanel.cs` | KJ `0e236a56` 6/17 생성. JC `ecb27a4b` 6/22 초상화 연결, `a089eb4c` 6/28 라이브러리 통합. KJ 6/25~26 수정 | KJ 기반 UI, JC 보완. 기존 컴포넌트 재사용 |
| 버튼 선택 `KJ/Scripts/UI/ToggleButton.cs`, `SkillButtonController.cs` | KJ `fa7fd44d` 6/16 도입. 컨트롤러에 ASB 병합 수정 이력. JC `b98d4a01` 6/25 재클릭 선택 표시, `0f42ac1c` 8/12 ESC 후 재선택 수정 | ToggleButton은 자동/배속 등에도 쓰인다. 버튼 공통 로직과 스킬 전용 로직을 구분 |
| 전투 호버 연결 `KJ/Scripts/UI/SkillButtonTooltip.cs` | KJ `fa7fd44d` 6/16 초기 구현. JC `ecb27a4b` 6/22 공용 SkillTooltip 연결로 변경, `04be6510` 7/3 호버 중 턴 전환 보완. KJ `5d495417` 7/30 무기 레벨 API 호출 1줄 변경 | KJ 폴더지만 현재 호버 표시 연결의 상당 부분은 JC 작업. 전투용 연결과 공용 표시를 분리해서 검토 |
| 옛 호버 `KJ/Scripts/UI/HoverTooltip.cs` | KJ `fa7fd44d` 6/16 도입, 6/19·25 보완 | 현재 대상 씬의 두 스킬 버튼에서는 직렬화 참조를 찾지 못함. 프로젝트 전체 미사용이라는 뜻은 아님 |
| 아이콘 `JC/Scripts/UI/SkillButtonIcon.cs` | JC `ecb27a4b` 6/22 생성, 6/25·28 보완. KJ `5d495417` 7/30 무기 레벨 API 호출 1줄 변경 | 기존 Sprites 조회를 유지 |
| 공용 툴팁 `JC/Scripts/UI/Lobby/SkillTooltip.cs` | JC `5e31866c` 6/17 생성, `665fd41d` 6/19 영속화·레이아웃 보완, 6/22 전투 대응 | 로비/연구소/캐릭터 정보에서도 사용. JC 작성이어도 공용 영역 |
| 공용 설명 `JC/Scripts/UI/Lobby/ClassSkillTooltipText.cs` | JC `665fd41d` 6/19 생성, `324fdb46` 8/5 템플릿 전환 및 SkillData 브리지 | 연구소/캐릭터 정보/전투가 공유. 강화/저장 개편과 섞지 않음 |
| 턴 순서 `ASB/Scripts/Battle/Core/TurnOrderUI.cs` | ASB `721950ea` 6/8 생성. KJ 6/16~18 슬롯·프레임·마스크, JC 6/22·28 초상화 연결 | ASB의 순서 조회 + KJ/JC의 표시가 결합 |
| 유닛 HP/IP `ASB/Scripts/Battle/Core/UnitHPBar.cs` | 최초는 5/4 초기 클론이라 원저자 불명. KJ 6/16 HP/IP 표시·연결 수정 | 사용자의 유지 지시. 새 관리 스크립트로 대체하지 않음 |
| 결과 생성 `ASB/Scripts/Battle/Core/BattleUIManager.cs` | 최초 5/4 클론. KJ `9cc23380` 6/23 결과 프리팹화, JC 6/22 연결, ASB `564632ba` 6/29 정리 | 기존 생성 API와 결과 수락 흐름 유지 |
| 결과 표시 `KJ/Scripts/UI/HeroInfoResult.cs`, `BattleResultPanel.cs`, `BattleResultView.cs` | KJ 6/16~23 도입/프리팹화. JC `a089eb4c` 6/28 표시순서·스킬 획득 순차 표시. KJ `5d495417` 7/30 획득창과 결과창 노출 순서 보완 | 보상 계산/확정은 전투·저장 측. 표시와 지급을 따로 다룸 |
| 입력·턴·실행 `InputHandler.cs`, `BattleFlowManager.cs`, `BattleManager.cs` | 최초 5/4 클론. 이후 ASB 중심 개선, JC UI/타겟팅/ESC 관련 보완, 일부 KJ UI 연결. ASB `babab0a8` 8/3 턴 소유권 정리, `1315fd5d` 9/11 튜토리얼 제한 추가 | ASB 전투 로직 영역. UI 개편을 이유로 함께 재작성하지 않음 |
| 씬 `Scenes/TmpBattleScene.unity` | 세 작업자 모두 수정. 최근은 ASB `f90c44a2` 9/8 보스/소환물, JC 9/2 라이팅, KJ `e564a3ce` 7/30 결과창 겹침 | 씬 전체의 단독 소유자로 보지 않는다. 수정할 오브젝트와 컴포넌트를 특정 |

원시 이력 조사: `C:\Dev\_scratchpad\battle-ui-restart-7a50f3e001\history.json`. 18개 주요 파일의 이름 변경 추적 이력과 현재 줄의 커밋 작성자를 대조했다. 주요 호버 교체 및 최근 API 수정은 실제 diff도 확인했다.

## 현재 실제 연결 — 씬 직렬화와 코드 기준

- `BattleSceneManager` 오브젝트: BattleFlowManager, BattleManager, SkillButtonController가 켜져 있다. 중앙에 새 HUD 관리자 하나를 추가해야 하는 구조는 아니다.
- `BattleSceneCanvas/HeroInfoWindow`: HeroInfoPanel이 켜져 있다. 그 아래 `HeroInfo`의 다른 HeroInfoPanel은 꺼져 있다. 두 컴포넌트를 구분해야 한다.
- `BattleSceneCanvas/ButtonController/Button_Skill`, `Button_Skill_2`: ToggleButton·SkillButtonTooltip이 켜져 있고, 자식 SkillIcon에는 SkillButtonIcon이 있다. 컨트롤러가 두 ToggleButton을 참조한다.
- `BattleSceneCanvas/TurnOrderPanel`: TurnOrderUI가 기존 TurnSlotUI 프리팹을 최대 5개 생성한다. 순서는 BattleFlowManager.GetPredictedTurnOrder에서 받는다.
- `UIManager`: BattleUIManager가 `Resources/UI_Prefab/Battle_Scene/Result_VictoryPanel.prefab`·`Result_DefeatPanel.prefab`을 생성한다. 기존 ResultPanel은 시작 시 비활성이나 결과 생성 부모로 참조되므로, 꺼져 있다고 삭제하면 안 된다.
- `SkillTooltip` 프리팹 원본은 `Assets/_UI_Prefabs_Runtime/UI_Common/SkillTooltip.prefab`이다. 씬에 인스턴스가 있으나 실행 시 SkillTooltip.Awake → EnsurePromoted가 `PersistentTooltipCanvas`를 생성하고 DDOL로 옮긴다. 먼저 존재한 싱글턴이 있으면 뒤의 인스턴스는 제거한다. 따라서 플레이 전 씬 배치와 실제 표시 인스턴스가 달라질 수 있다.
- HP/IP는 기존 유닛 쪽 UnitHPBar가 BattleCharactor의 HP/IP 이벤트를 받아 표시하고 위치를 갱신한다.

호출 흐름:

```text
턴 시작: BattleFlowManager
  → HeroInfoPanel: 현재 캐릭터 정보
  → TurnOrderUI: 턴 순서
  → SkillButtonController: 버튼 선택 상태
  → SkillButtonIcon / SkillButtonTooltip: 해당 턴의 스킬 정보

클릭: Button → ToggleButton → SkillButtonController
  → InputHandler.BeginPendingAction → 유효 대상 클릭 → 기존 BattleManager 실행
  (행동/대상 허용 여부와 턴 진행은 기존 BattleFlowManager)

호버: SkillButtonTooltip
  → 현재 유닛의 기존 스킬/무기 데이터 + 공용 설명/아이콘 조회
  → SkillTooltip.ShowInfo → PersistentTooltipCanvas에서 표시
  → 포인터 이탈 시 SkillTooltip.Hide

전투 종료: 기존 전투 종료 흐름
  → BattleUIManager.ShowBattleResultUI → BattleResultPanel
  → 기존 획득 화면/확인 흐름
```

## 개선 후보 — 아직 수정하지 않음

1. 씬의 활성/비활성 중복과 런타임 생성 위치를 우선 식별한다. 삭제나 DDOL 통합부터 하지 않는다.
2. 자동 선택은 InputHandler의 턴 시작 처리와 SkillButtonController의 Start/턴 시작 표시 양쪽에 있다. 매 시전 선택으로 바꿀 때 한쪽만 수정하면 입력과 표시가 달라질 수 있으므로 한 행동 변경으로 함께 검토한다. 둘이 동일한 실행을 중복 호출한다고 단정하지 않는다.
3. SkillButtonTooltip은 턴 이벤트로 currentUnit을 받는다. SkillButtonIcon은 활성화 즉시 CurrentUnit도 읽는다. 턴 중 활성화 차이를 재현해야 하는 검토 후보이며, 현재 버그가 발생했다고 확정한 것은 아니다.
4. 공용 SkillTooltip은 싱글턴/공용 Hide를 사용한다. 전투 표시 배치만 바꾸려면 우선 전투 호출부의 위치 설정을 사용하고, 공용 내부 변경이 필요할 때만 다른 화면 영향까지 확인한다.
5. 보완 이력과 임시 브리지가 쌓여 있으나 전체 재작성 근거는 아니다. 해당 작업에서 관찰된 중복/불일치만 원인과 영향 범위를 설명한 뒤 고친다.

## 다음 작업 분할 제안

| 단위 | 사용자에게 먼저 보여줄 범위 | 유지할 연결 |
|---|---|---|
| 1. 스킬 호버 툴팁 | 두 버튼의 SkillButtonTooltip, 공용 SkillTooltip, 설명 생성 부분. 위치/내용/생성 위치를 따라가고 한 가지 개선만 결정 | 기존 ShowInfo/Hide, 스킬 데이터/아이콘 조회 |
| 2. 현재 캐릭터 정보 출력 | 활성 HeroInfoPanel과 연결된 텍스트/이미지. 비활성 중복의 출처부터 확인 | 기존 HP/IP·턴 이벤트 |
| 3. 매 시전 수동 선택 | SkillButtonController와 InputHandler에서 자동 선택 제거 및 표시/입력 초기화 관계만 검토 | 기존 BeginPendingAction·대상 검사·실행 경로, ASB 튜토리얼 제한 |
| 4. 4개 슬롯 표시 | 기존 버튼 구조에서 필요한 표시 변경을 산정. 신규 파일 필요 여부를 먼저 확인 | 기본 스킬 사용 유지, 미결선 기능 추가 금지 |
| 5. 턴 순서·범위 표시 | 각각 별도 작은 작업으로 기존 표시 컴포넌트 활용 | 기존 예측 순서·대상/범위 계산 |
| 6. 결과창 | 표시 배치와 기존 데이터 연결만 먼저 검토 | 보상 지급·확인·스킬 획득 순서 |

각 단위에서 ‘현재 흐름 → 변경 이유 → 대상 파일/오브젝트 → 신설 여부 → 확인 방법’을 짧게 설명하고 합의 후 구현한다. 현재 준비를 전체 구현 승인으로 해석하지 않는다. HP바·공용 프레임워크·DDOL 구조·강화 저장/실행 결선은 일괄 개편하지 않는다.

## 검증 및 미확정

- Git 이력/현재 코드/씬·프리팹 참조만 읽었다. 프로젝트 파일 수정·새 C#·리소스 복사·Unity 조작·플레이·테스트 실행·커밋 없음.
- 원본 스프레드시트는 재열람하지 않았다. 이번 요청인 기존 구조·기여 이력 확인에 범위를 한정했다.
- 런타임에 최종 살아남는 DDOL 인스턴스와 실제 조작감은 이번 정적 분석만으로 확정하지 않았다. 전체 프로젝트의 미사용 코드 판정이나 전체 DDOL 개수 조사도 하지 않았다.
- 현재 담당자 확정은 Git 이력만으로 대신할 수 없다. 기존 사용자 지시에 따라 UI는 KJ와 공유하는 영역, 전투 내부는 ASB 영역으로 취급하고 필요한 편집 전에 확인한다.
