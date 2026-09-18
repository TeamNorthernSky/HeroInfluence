# 전투 스킬 기반 연결 구현 · 2026-09-15

## 결과

매 시전 스킬 선택, 기본판·강화판 실행, 유닛·열·전체 진영·자신 발동 입력을 연결했습니다. 영속 데이터로 생성되는 실제 전투 경로를 기준으로 구현했습니다. 협회 강화는 기본~+5와 기본판·강화판의 계열 공유까지 연결했습니다.

## 확정 기준

- 기준 문서: `D:\SVN\2_Documents\강현재\H.I 캐릭터 스킬 기획서 V1.4.xlsx`, 연출 기획 V1.2.
- 강화판 습득 후 기본판 복귀를 허용하지 않음: 문서 근거가 아닌 사용자 결정.
- 매 시전 스킬 선택 후 두 번째 클릭으로 발동. 열/진영은 해당 범위에 유효 대상이 있으면 빈 칸 클릭 허용.
- TAO: 부활 미사용+죽은 아군 존재 시 시체 선택 필수. 부활 보류 불가. 시체가 없거나 부활 사용 후에는 적 진영 클릭. 시체가 없을 때는 부활 사용권 보존.
- 협회 강화: 기본 35%, +1 28%, +2 21%, +3 14%, +4 7%, +5 0%. 위력은 스킬 변형별 계수.
- 저장 단계는 기존 호환을 위해 1=기본, 6=+5. 기본/강화판에 나뉜 기존 기록은 높은 단계로 병합.
- 사용자 추가 확정: +5 비용 자금 1000·메달 10. 연구소 1에서 +1, 2에서 +2, 3에서 +3~+5 순차 강화.

## 발동 입력

| 구분 | 발동 클릭 |
|---|---|
| 단일 공격·단일 회복·체인·LFL·대쉬 | 유효 유닛 |
| 솔라 프리즘 | 대상 열의 3칸 중 한 칸 |
| 크래쉬·프리즘 폭발·광역 코어·위압 코어 | 적 진영 6칸 중 한 칸 |
| 방어 코어 | 시전자 자신 또는 점유 발판 |
| TAO: 부활 대상 필요 | 죽은 아군 1명 |
| TAO: 부활 대상 없음/이미 사용 | 적 진영 6칸 중 한 칸 |

## 검증

- Unity 2022.3.62f3 에디터 컴파일 통과, diff 공백 검사 통과.
- 원래 씬과 분리된 빈 씬의 Play 상태에서 현재 씬과 같은 SO 테이블을 로드하여 검증.
- 4명 × 레벨 1~8의 습득/교체, 16개 기본판+16개 강화판 실행 및 실제 4개 버튼 선택 검증.
- 빈 칸 열/전체 진영 클릭, 잘못된 진영·단일 빈 칸 거절, TAO 두 시체 중 직접 선택·반복 시전·부활 확정 검증.
- 실제 TAO/LFL/크래쉬/방어·위압 코어 시전, 효과 적용, 잘못된 요청의 IP 보존 검증.
- 연구소 실제 프리팹의 +5 버튼, 리소스 1000/10 차감, 연구소 3에서 5회 순차 강화 검증.
- 실제 GameSaveData JSON 왕복 및 DH RestoreLab 경로에서 +5 계열 공유 유지 검증.
- 32개 현행 스킬의 아이콘과 연출 조회, 리스크 키/확률의 시전·연출 데이터 복사 검증.
- [play-results.txt](C:/Dev/_scratchpad/battle-foundation-d523740a54/play-results.txt): 369개 통과.
- [extra-results.txt](C:/Dev/_scratchpad/battle-foundation-d523740a54/extra-results.txt): 87개 통과.
- [final-runtime-results.txt](C:/Dev/_scratchpad/battle-foundation-d523740a54/final-runtime-results.txt): 6개 통과.

- 합계 462개 확인 항목 통과. [연구소 +5 화면](C:/Dev/_scratchpad/battle-foundation-d523740a54/lab-plus5.png)도 확인.
- 검증 종료 후 DHScene_3 복구: Play=False, 씬 dirty=False, runInBackground=False. 사용자 저장 파일은 테스트에 사용하지 않음.

## 남은 연출 작업과 한계

- 리스크 효과 자체(자기 피해·빗나감 등)의 실행부는 기존 미구현이며 이번 작업에도 포함하지 않았습니다. 이번에는 리스크 종류와 협회 강화 확률을 시전 데이터까지 전달했습니다. 확률 수치 전달을 실제 리스크 효과 실행 완료로 보아서는 안 됩니다.
- 강화판 전용 연출이 있는 바인딩은 유지하고, 없는 강화판은 기본판 연출을 재사용합니다. 신규 VFX 제작·모델 애니메이션 타이밍의 시각 검수는 다음 연출 작업 범위입니다.
- 강화 비용의 +5와 연구소 단계 조건은 갱신 기획서 수령 후 재대조할 사용자 결정값입니다.
- V3.1의 현재 사용 중인 SO를 보정했습니다. 구형 밸런스 파일 재임포트 시 그 파일의 값으로 덮일 수 있으므로 재임포트 기준도 V1.4와 대조해야 합니다.

## 변경 파일

기존 28개 파일, 신규 스크립트 2개와 그 .meta 2개. 기존 미추적 Docs.meta는 보존했습니다. 커밋하지 않았습니다.

| 영역 | 파일 | 변경 |
|---|---|---|
| ASB | [JcVfxPresentationAdapter.cs](<C:/Dev/HeroInfluence/Assets/ASB_Work/Effect/JcVfxPresentationAdapter.cs>) | LFL의 확정된 추가 회복 대상을 SetTargets로 전달. 주 대상은 중복 제외하고 시전자도 실제 추가 대상이면 유지. |
| ASB | [ClassSkillDataTable.asset](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Data/Tables/H.I 알파 전투 밸런스 데이터 테이블 V3.1/ClassSkillDataTable.asset>) | 현재 참조되는 V3.1 테이블: 루미나 습득 순서, 펀치·대쉬 대상 유형 보정. 협회 강화의 구형 위력 증가 열을 변형별 고정 계수로 통일. |
| ASB | [InputHandler.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/InputHandler.cs>) | 클릭한 스킬 ID와 시전 데이터를 보관. 빈 칸을 포함한 열/진영 클릭 해석, 선택 취소, 실패 시 행동 권한 해제. 가까운 빈 발판 뒤로 클릭이 뚫리는 문제 차단. |
| ASB | [TargetingHelper.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/TargetingHelper.cs>) | 유닛/열/진영/자신 입력과 TAO 상황별 유효 대상 일치. 버프의 아군 판정 수정. |
| ASB | [BattleFlowManager.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleFlowManager.cs>) | 실행 실패에 한해 이번 턴 행동 권한을 반환하는 메서드 추가. |
| ASB | [BattleManager.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/BattleManager.cs>) | 강화판 습득 후 기본판 실행 우회 차단, 목표 재검증 뒤 IP 소모. TAO 부활 확정 시 사용권 소비. LFL 강화판 연출 대상 전달. 상태 효과만 있는 코어도 시전 연출 후 효과 적용. |
| ASB | [CombatCalculator.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/CombatCalculator.cs>) | 방어 코어의 최종 피해 감소 배율 적용. |
| ASB | [StatusEffectContext.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Battle/Core/StatusEffectContext.cs>) | 상태 효과 수치 전달 필드 추가. |
| ASB | [SkillPresentationDirector.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Battle/Presentation/SkillPresentationDirector.cs>) | 연출용 데이터 복사에서 리스크 키·협회 강화 단계를 보존. |
| ASB | [BaseSkillHandler.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/BaseSkillHandler.cs>) | 대쉬의 앞/뒤 2칸, 솔라 프리즘의 고정 3칸 열, 전체 코어의 영향 범위 해석. |
| ASB | [DamageSkillHandler.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DamageSkillHandler.cs>) | 대쉬 주/서브 계수, 프리즘 폭발 50% 미만 보너스, 저체력 추가 피해의 강화판 계수, 체인 라이트닝 추가 대상 계수 연결. |
| ASB | [DebuffSkillHandler.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/DebuffSkillHandler.cs>) | 크래쉬 전체 피해+도발, 방어 코어 자신 피해 감소, 위압 코어 전체 방어력 감소 수치 연결. |
| ASB | [HealSkillHandler.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/HealSkillHandler.cs>) | 네코밍 최대 HP 기준 회복, 신성탄 계열 무작위 아군 실제 피해량 회복, LFL 주/서브 회복, TAO 클릭 시체·20/30% 부활·전체 공격 분기. |
| ASB | [SkillAreaPreviewHelper.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillAreaPreviewHelper.cs>) | 실제 대쉬/열/전체/TAO 결과와 범위 미리보기 일치. |
| ASB | [SkillEffectHelper.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillEffectHelper.cs>) | 상태 수치와 방어 코어 효과 유형을 실제 상태 인스턴스에 전달. |
| ASB | [SkillExecutionRegistry.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillExecutionRegistry.cs>) | 16개 강화판을 기본 계열 실행부로 연결하고 잘못 연결된 기본 핸들러 수정. |
| ASB | [SkillExecutionResult.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Battle/Skill/SkillExecution/SkillExecutionResult.cs>) | 상태 결과에 수치를 함께 기록하는 선택 인자 추가. |
| ASB | [BattleCharactor.PersistenceEquipment.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Data/Core/BattleCharactor.PersistenceEquipment.cs>) | 영속 유닛의 현재 습득 4개 계열을 전투에 주입. 선호 스킬의 강화판 선택, 계열별 협회 강화 단계 복사. |
| ASB | [BattleCharactor.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Data/Core/BattleCharactor.cs>) | 이번 시전 선택을 영속 선호 스킬과 분리. 해제 시 원래 선호 계열 복원, 기본판·강화판 계열 해석 및 피해 감소 조회. |
| ASB | [StatusEffectManager.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Data/Core/StatusEffectManager.cs>) | 방어 코어를 시전 턴 종료 이후 유지하고 다음 자기 턴 시작에 만료. 최종 피해 감소 배율 조회. |
| ASB | [StatusEffectType.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Data/Core/StatusEffectType.cs>) | 기존 enum 숫자를 유지하며 방어 코어용 효과 유형 추가. |
| ASB | [SkillData.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Data/Model/SkillData.cs>) | 리스크 식별값, 이번 시전의 협회 강화 단계와 리스크 확률 조회 추가. |
| ASB | [SkillPresentationCatalog.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Data/Presentation/SkillPresentationCatalog.cs>) | 강화판 전용 바인딩 우선, 없으면 같은 계열 기본 연출 재사용. |
| DH | [DHCsvTemplateCatalog.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/DH/Scripts/Manager/Data/DHCsvTemplateCatalog.cs>) | 레벨별 현재 사용 변형 4개 조회. 협회 단계와 변형 위력 분리, 원본 리스크 키를 실행 데이터에 전달. |
| JC | [LabManager.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/JC/Scripts/Manager/LabManager.cs>) | 기본~+5(저장 1~6), 기본/강화판 공유, 기존 기록의 최댓값 병합. +5 비용 1000/10, 연구소 3에서 +3~+5 허용. 교체된 기본판 재장착 차단. |
| JC | [ClassSkillTooltipText.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/JC/Scripts/UI/Lobby/ClassSkillTooltipText.cs>) | 변형 위력과 협회 강화 단계·리스크 확률을 구분하여 표시. |
| JC | [LabModalController.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/JC/Scripts/UI/Lobby/LabModalController.cs>) | 실제 프리팹의 기존 카드 안에 다섯 번째 강화 버튼/게이지 추가 및 입력 연결. 모든 현행 계열의 기본~+5 표시. |
| KJ | [SkillButtonController.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/KJ/Scripts/UI/SkillButtonController.cs>) | 4개 버튼 각각의 스킬 ID 전달, 잠금·자동 교체 표시 및 실제 시전 선택값에 따른 토글/설명 동기화. |
| ASB | [SkillActivationRules.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Battle/BinputHandler/SkillActivationRules.cs>) | 신규: Unit/Column/Side/Self 발동 입력과 TAO 부활 필수 여부를 공용 판정. 열/진영 클릭을 실행 앵커 유닛으로 변환. |
| ASB | [HeroSkillRules.cs](<C:/Dev/HeroInfluence/Assets/_ProtoType_Merge/ASB/Scripts/Data/Model/HeroSkillRules.cs>) | 신규: V1.4 16개 계열의 기본/강화 ID 해석 및 기본~+5 리스크 확률 공용 계산. |
