using System.Collections.Generic;

/// <summary>
/// 적 스폰 계획. 키(EnemyGroupKey/BattleKey)+레벨로 복원한 "스폰 입력"을 담는 순수 데이터.
/// 조회·레벨 계산은 공용 계층(EnemySpawnPlanBuilder)에서, 셀 해석/Instantiate는 전투씬(EnemySpawner)에서 수행한다.
/// 씬 의존이 없어 DH 소비처(스킵 전력/프롬프트/보상)도 동일 계획을 재사용할 수 있다.
/// </summary>
public sealed class EnemySpawnPlan
{
    public IReadOnlyList<EnemySpawnEntry> Entries { get; }
    // 이벤트 전용 시나리오(인질 등). 일반 전투는 null.
    public BattleScenarioConfig Scenario { get; }

    public EnemySpawnPlan(IReadOnlyList<EnemySpawnEntry> entries, BattleScenarioConfig scenario = null)
    {
        Entries = entries ?? new List<EnemySpawnEntry>();
        Scenario = scenario;
    }

    public int Count => Entries.Count;
}

/// <summary>
/// 스폰할 적 유닛 하나의 확정 입력.
/// Data는 레벨 스케일이 적용된 "복제본"이어야 한다(카탈로그가 공유하는 템플릿을 직접 변형하면 안 됨).
/// </summary>
public sealed class EnemySpawnEntry
{
    public EnemyData Data { get; }
    // 1~6 논리 슬롯. 전투씬에서 BattleLogicalSlotMap으로 GridCell에 매핑한다.
    public int CombatSlot { get; }
    // Resources 프리팹 식별(일반=EnemyData.Index, 이벤트=PrefabResourcePath 우선).
    public string PrefabKey { get; }
    // 이벤트=명시 스킬 목록, 일반=null/빈값(EnemyData.Index 규칙으로 기본 스킬 유도).
    public IReadOnlyList<SkillData> ExplicitSkills { get; }

    public EnemySpawnEntry(EnemyData data, int combatSlot, string prefabKey, IReadOnlyList<SkillData> explicitSkills = null)
    {
        Data = data;
        CombatSlot = combatSlot;
        PrefabKey = string.IsNullOrWhiteSpace(prefabKey) ? string.Empty : prefabKey.Trim();
        ExplicitSkills = explicitSkills;
    }

    public bool HasExplicitSkills => ExplicitSkills != null && ExplicitSkills.Count > 0;
}
