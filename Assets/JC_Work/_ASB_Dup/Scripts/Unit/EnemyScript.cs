using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EnemyAI;

/// <summary>
/// CSV 기반 적 데이터 보관. Combat tuning은 BattleCharactor에만 반영하고,
/// 에디터 OnValidate에서 BattleCharactor를 덮어쓰지 않습니다.
/// </summary>
public class EnemyScript : MonoBehaviour, IUnitIdentifier
{
    public UnitData enemyData;
    private EnemyAI.IEnemyAI currentAI;

    public StatBlock currentStats;

    [Header("Stat Weights (CSV Initialize 시 BattleCharactor로 전달)")]
    [SerializeField] private StatWeights unitWeight = new StatWeights(1f, 1f, 1f);

    [SerializeField] private StatWeights levelWeight = new StatWeights(1f, 1f, 1f);

    [SerializeField] private StatWeights classWeight = new StatWeights(1f, 1f, 1f);

    [Range(1, 100)] public int level = 1;

    [Range(1, 50)] public int unitCount = 1;

    public StatWeights UnitWeight => unitWeight;

    public StatWeights LevelWeight => levelWeight;

    public StatWeights ClassWeight => classWeight;

    public UnitData Data
    {
        get => enemyData;
        set => enemyData = value;
    }

    public string UnitID
    {
        get
        {
            if (enemyData == null || string.IsNullOrWhiteSpace(enemyData.Index))
            {
                return string.Empty;
            }

            return enemyData.Index.Trim();
        }
    }

    public void Initialize(UnitData data)
    {
        enemyData = data;
        currentStats = data != null ? data.baseStats : default;

        var battle = GetComponent<BattleCharactor>();
        if (battle == null)
        {
            return;
        }

        if (data != null)
        {
            battle.SetBaseStats(data.baseStats);
            battle.SetUnitNameForSkillMatching(data.Name);
        }
        else
        {
            // 프로토타입 직접 입력 모드: 유닛 데이터 에셋이 없으면 인스펙터 base 값을 원본으로 사용
            battle.BuildBaseStatsFromInspector();
            battle.SetUnitNameForSkillMatching(battle.UnitName);
        }

        battle.ApplyCombatTuning(level, unitCount, unitWeight, levelWeight, classWeight);
        battle.RecalculateStats();
        battle.ResolveSelectedSkill();
        battle.InitializeCurrentHpToMax();
        battle.MarkInitializedFromDataPipeline();

        EnemyData typedEnemyData = data as EnemyData;
        enemyData = typedEnemyData ?? data;
        string aiType = typedEnemyData != null ? typedEnemyData.UnitAI : string.Empty;
        int aiIndex = 0;
        if (!string.IsNullOrWhiteSpace(aiType))
        {
            int.TryParse(aiType.Trim(), out aiIndex);
        }
        currentAI = EnemyAIFactory.CreateAI(aiIndex);

        string id = UnitID;
        string nm = data != null ? data.Name : "null";
        Debug.Log($"[EnemyScript] Initialize: name={nm}, id='{id}'");
    }

    public IEnumerator RunAITurn(BattleManager battleManager, BattleFlowManager flowManager)
    {
        BattleCharactor self = GetComponent<BattleCharactor>();
        if (self == null || self.IsDead || battleManager == null)
        {
            yield break;
        }

        List<BattleCharactor> targets = flowManager != null
            ? flowManager.GetAlivePlayerUnits()
            : new List<BattleCharactor>();

        EnemyActionDecision decision = currentAI != null ? currentAI.DecideAction(self, targets) : null;
        BattleCharactor target = decision != null ? decision.Target : null;

        if (target == null)
        {
            target = targets.Find(t => t != null && !t.IsDead);
            if (target == null)
            {
                Debug.LogWarning($"[EnemyScript] AI 타겟이 없어 행동을 건너뜁니다: unit={self.UnitName}");
                yield break;
            }

            Debug.LogWarning($"[EnemyScript] AI 결정 실패. 기본 공격으로 대체합니다: unit={self.UnitName}");
            battleManager.ExecuteBasicAttack(self, target);
            yield return new WaitForSeconds(1.5f);
            yield break;
        }

        EnemyActionType actionType = decision != null ? decision.ActionType : EnemyActionType.BasicAttack;
        switch (actionType)
        {
            case EnemyActionType.ClassSkill:
                SkillData classSkill = decision != null ? decision.SelectedSkill : self.SelectedSkillData;
                if (classSkill != null)
                {
                    battleManager.ExecuteGridSkill(self, target, classSkill);
                }
                else
                {
                    Debug.LogWarning($"[EnemyScript] ClassSkill 선택이지만 스킬이 없어 기본 공격으로 대체: unit={self.UnitName}");
                    battleManager.ExecuteBasicAttack(self, target);
                }
                break;

            case EnemyActionType.WeaponSkill:
                if (self.EquippedWeaponData != null)
                {
                    SkillData converted = self.EquippedWeaponData.ToSkillData();
                    battleManager.ExecuteGridSkill(self, target, converted);
                }
                else
                {
                    Debug.LogWarning($"[EnemyScript] WeaponSkill 선택이지만 무기가 없어 기본 공격으로 대체: unit={self.UnitName}");
                    battleManager.ExecuteBasicAttack(self, target);
                }
                break;

            case EnemyActionType.BasicAttack:
            default:
                battleManager.ExecuteBasicAttack(self, target);
                break;
        }

        yield return new WaitForSeconds(1.5f);
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        level = Mathf.Clamp(level, 1, 100);
        unitCount = Mathf.Clamp(unitCount, 1, 50);

        var battle = GetComponent<BattleCharactor>();
        if (battle == null)
        {
            return;
        }

        if (enemyData == null || string.IsNullOrWhiteSpace(enemyData.Name))
        {
            return;
        }

        battle.SetUnitNameForSkillMatching(enemyData.Name);
        battle.ResolveSelectedSkill(false);
    }
}
