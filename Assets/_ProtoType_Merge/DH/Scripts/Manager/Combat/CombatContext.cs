using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class CombatEventBattleUnitData
{
    public string UnitKey;
    public int Slot;
    public int Level;
    public string EnemyName;
    public string EnemyConcept;
    public int MaxHp;
    public int Atk;
    public int Def;
    public float CriticalRate;
    public float CounterRate;
    public float ReduceRate;
    public int Speed;
    public string UnitAI;
    public int ExperiencePoint;
    public string PrefabResourcePath;
    public List<SkillData> Skills = new List<SkillData>();

    public CombatEventBattleUnitData() { }

    // DH 이벤트 전투 전용 임시 유닛 데이터.
    // ASB 전투씬은 이벤트 전투일 때 PersistentEnemyRepository 대신 이 값을 그대로 스폰 입력으로 사용한다.
    public CombatEventBattleUnitData(string unitKey, int slot, int level, DHEventBattleUnitTemplate source)
    {
        UnitKey = unitKey;
        Slot = slot;
        Level = level;

        if (source == null)
            return;

        EnemyName = source.EnemyName;
        EnemyConcept = source.EnemyConcept;
        MaxHp = source.BaseMaxHp;
        Atk = source.BaseAtk;
        Def = source.BaseDef;
        CriticalRate = source.CriticalRate;
        CounterRate = source.CounterRate;
        ReduceRate = source.ReduceRate;
        Speed = source.Speed;
        UnitAI = source.UnitAI;
        ExperiencePoint = source.ExperiencePoint;
    }
}

[Serializable]
public class CombatEventBattleData
{
    public int ZoneId;
    // 이벤트 전투 테이블의 전투 키(Start_BE###). 일반 EnemyGroupKey와 다른 테이블을 가리킨다.
    public string BattleKey;
    // 전투 종료 후 DH 채팅 흐름이 재개할 Chat_ID. 0이면 별도 재개 채팅 없음.
    public int ResumeChatId;
    // 이동형 적 이벤트 전투에서 원본 필드 적을 찾기 위한 MapProgress placement key.
    public string SourceEnemyPlacementKey;
    // 메인이벤트 전투에서 승리/패배 후 원본 이벤트 오브젝트 처리에 사용하는 EventKey.
    public string SourceMainEventKey;
    public int EnemyLevel;
    // ASB EnemySpawner가 이벤트 전투일 때 읽는 확정 적 유닛 목록.
    // [TEMP:EVENTBUILD] 전투씬 키-빌드 이관의 과도기 폴백 기준선. 런타임 동등성 검증 후 §6에서 제거 예정.
    public List<CombatEventBattleUnitData> EnemyUnits = new List<CombatEventBattleUnitData>();
    // 전투씬이 돌려줘야 하는 숫자 결과값. 예: HostageInjuredCount.
    public List<DHEventNumericState> NumericResults = new List<DHEventNumericState>();
    // 인질전 같은 전투 특수 규칙. null이면 일반 이벤트 전투처럼 처리한다.
    // [TEMP:EVENTBUILD] 위와 동일 — 과도기 폴백 기준선.
    public BattleScenarioConfig Scenario;

    // ASB 공통 처리에서 "적 그룹 키" 이름으로 읽어야 할 경우를 위한 별칭.
    // 이벤트 전투에서는 BattleKey가 곧 전투 그룹 키 역할을 한다.
    public string EnemyGroupKey => string.IsNullOrWhiteSpace(BattleKey) ? string.Empty : BattleKey.Trim();
    public CombatEnemySourceType SourceType => CombatEnemySourceType.Event;

    public CombatEventBattleData() { }

    public CombatEventBattleData(
        int zoneId,
        string battleKey,
        int resumeChatId,
        int enemyLevel,
        IReadOnlyList<CombatEventBattleUnitData> enemyUnits)
    {
        ZoneId = zoneId;
        BattleKey = battleKey;
        ResumeChatId = resumeChatId;
        EnemyLevel = enemyLevel;

        EnemyUnits.Clear();
        if (enemyUnits == null)
            return;

        for (int i = 0; i < enemyUnits.Count; i++)
        {
            CombatEventBattleUnitData unit = enemyUnits[i];
            if (unit != null)
                EnemyUnits.Add(unit);
        }
    }

    public void SetNumericResult(string key, float value)
    {
        string normalizedKey = DHEventStateRepository.NormalizeKey(key);
        if (string.IsNullOrEmpty(normalizedKey))
            return;

        for (int i = 0; i < NumericResults.Count; i++)
        {
            DHEventNumericState state = NumericResults[i];
            if (state == null)
                continue;

            if (!string.Equals(DHEventStateRepository.NormalizeKey(state.key), normalizedKey, StringComparison.Ordinal))
                continue;

            state.key = normalizedKey;
            state.value = value;
            return;
        }

        NumericResults.Add(new DHEventNumericState(normalizedKey, value));
    }

    public bool TryGetNumericResult(string key, out float value)
    {
        value = 0f;
        string normalizedKey = DHEventStateRepository.NormalizeKey(key);
        if (string.IsNullOrEmpty(normalizedKey))
            return false;

        for (int i = 0; i < NumericResults.Count; i++)
        {
            DHEventNumericState state = NumericResults[i];
            if (state == null)
                continue;

            if (!string.Equals(DHEventStateRepository.NormalizeKey(state.key), normalizedKey, StringComparison.Ordinal))
                continue;

            value = state.value;
            return true;
        }

        return false;
    }
}

[DisallowMultipleComponent]
public class CombatContext : MonoBehaviour
{
    public static CombatContext Instance { get; private set; }

    [Header("Current Combat Context")]
    [SerializeField] private CombatPartyPersistentData combatParty;
    [SerializeField] private CombatEnemyPersistentData combatEnemy;
    [SerializeField] private CombatEventBattleData eventBattle;
    [SerializeField, Min(1)] private int enemyLevel = 1;
    [SerializeField] private CombatResult combatResult = CombatResult.None;

    public CombatPartyPersistentData CombatParty => combatParty;
    // 일반 필드/거점/빌런연합 전투 정보. 이벤트 전투일 때는 null로 비운다.
    public CombatEnemyPersistentData CombatEnemy => combatEnemy;
    // 이벤트 전투 정보. HasEventBattle이 true면 ASB는 CombatEnemy보다 이 데이터를 우선 사용한다.
    public CombatEventBattleData EventBattle => eventBattle;
    public CombatResult Result => combatResult;
    public bool HasEventBattle => eventBattle != null && !string.IsNullOrWhiteSpace(eventBattle.BattleKey);
    public int EnemyLevel => Mathf.Max(1, enemyLevel);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void RegisterCombatParty(string partyId, System.Collections.Generic.IReadOnlyList<int> unitIndices)
    {
        if (string.IsNullOrWhiteSpace(partyId))
            return;

        if (combatParty == null)
        {
            combatParty = new CombatPartyPersistentData(partyId, unitIndices);
            return;
        }

        combatParty.SetPartyId(partyId);
        combatParty.SetUnitIndices(unitIndices);
    }

    public void RegisterCombatEnemy(string enemyId, System.Collections.Generic.IReadOnlyList<int> unitIndices)
    {
        RegisterCombatEnemy(enemyId, string.Empty, unitIndices);
    }

    public void RegisterCombatEnemy(string enemyId, string placementKey, System.Collections.Generic.IReadOnlyList<int> unitIndices)
    {
        RegisterCombatEnemy(enemyId, placementKey, string.Empty, 1, CombatEnemySourceType.None, unitIndices);
    }

    public void RegisterCombatEnemy(
        string enemyId,
        string placementKey,
        string enemyGroupKey,
        int enemyLevel,
        CombatEnemySourceType sourceType,
        System.Collections.Generic.IReadOnlyList<int> unitIndices)
    {
        if (string.IsNullOrWhiteSpace(enemyId))
            return;

        // 일반 전투 등록 시 이벤트 전투 데이터는 반드시 비운다.
        // ASB는 HasEventBattle false 상태에서 CombatEnemy를 읽으면 된다.
        eventBattle = null;

        if (combatEnemy == null)
        {
            combatEnemy = new CombatEnemyPersistentData(enemyId, placementKey, enemyGroupKey, sourceType, unitIndices);
            SetEnemyLevel(enemyLevel);
            return;
        }

        combatEnemy.SetEnemyId(enemyId);
        combatEnemy.SetPlacementKey(placementKey);
        combatEnemy.SetEnemyGroupKey(enemyGroupKey);
        combatEnemy.SetSourceType(sourceType);
        combatEnemy.SetUnitIndices(unitIndices);
        SetEnemyLevel(enemyLevel);
    }

    public void RegisterEventBattle(CombatEventBattleData nextEventBattle)
    {
        // 이벤트 전투는 EnemyUnits/Scenario를 CombatContext에 임시로 담아 전투씬에 넘긴다.
        // 이 경로는 PersistentEnemyRepository에 적 개체를 만들지 않는다.
        combatEnemy = null;
        eventBattle = nextEventBattle;
        SetEnemyLevel(nextEventBattle != null ? nextEventBattle.EnemyLevel : 1);
    }

    public void SetEnemyLevel(int nextEnemyLevel)
    {
        enemyLevel = Mathf.Max(1, nextEnemyLevel);
    }

    public void SetCombatResult(CombatResult nextResult)
    {
        combatResult = nextResult;
    }

    public void Clear()
    {
        combatParty = null;
        combatEnemy = null;
        eventBattle = null;
        enemyLevel = 1;
        combatResult = CombatResult.None;
    }
}
