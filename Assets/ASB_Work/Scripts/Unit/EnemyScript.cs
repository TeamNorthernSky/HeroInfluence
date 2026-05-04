using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    [Header("Debug")]
    [SerializeField] private bool enableAIDebugLog = true;


    [Header("Flat Stats (Inspector tuning)")]
    [SerializeField] private StatBlock inspectorBaseStats;

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

    public void Initialize(UnitData data = null)
    {
        enemyData = data;
        BattleCharactor battle = GetComponent<BattleCharactor>();
        if (battle == null)
        {
            return;
        }

        StatBlock baseStats;
        if (data != null)
        {
            baseStats = data.baseStats;
            battle.SetUnitNameForSkillMatching(data.Name);
        }
        else
        {
            baseStats = inspectorBaseStats;
            battle.SetUnitNameForSkillMatching(battle.UnitName);
        }

        battle.SetBaseStats(baseStats);
        battle.SetLevelScaling(false);
        battle.RecalculateStats();

        // 적 스킬 인덱스 규칙: enemyIndex * 10 + slot(1/2).
        // 기본 슬롯은 첫 번째(1)로 저장하고, ResolveSelectedSkill에서 classSkillIndex 우선 매칭합니다.
        if (data != null
            && !string.IsNullOrWhiteSpace(data.Index)
            && int.TryParse(data.Index.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int enemyIndexNum))
        {
            int defaultSkillSlot = 1;
            int combinedSkillIndex = (enemyIndexNum * 10) + defaultSkillSlot;
            battle.SetClassSkillIndex(combinedSkillIndex);
        }

        battle.ResolveSelectedSkill();
        battle.InitializeCurrentHpToMax();
        battle.MarkInitializedFromDataPipeline();

        EnemyData typedEnemyData = data as EnemyData;
        enemyData = typedEnemyData ?? data;
        EnsureAIReady();

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

        if (!EnsureAIReady())
        {
            Debug.LogError($"[EnemyScript] AI 초기화 실패로 적 턴을 건너뜁니다: unit={self.UnitName}");
            yield break;
        }

        List<BattleCharactor> targets = flowManager != null
            ? flowManager.GetAlivePlayerUnits()
            : new List<BattleCharactor>();

        //if (enableAIDebugLog)
        //{
        //    Debug.Log(
        //        $"[EnemyAI/Debug] RunAITurn start: self={self.UnitName}, aiNull={currentAI == null}, targets={targets.Count} [{FormatTargets(targets)}]");
        //}

        EnemyActionDecision decision = currentAI != null ? currentAI.DecideAction(self, targets) : null;
        if (decision != null && decision.Skip)
        {
            if (enableAIDebugLog)
            {
                Debug.Log($"[EnemyAI/Debug] SkipTurn: self={self.UnitName}");
            }

            yield return new WaitForSeconds(0.5f);
            yield break;
        }

        BattleCharactor target = decision != null ? decision.Target : null;

        if (enableAIDebugLog)
        {
            string actionTypeLabel = decision != null ? decision.ActionType.ToString() : "DecisionNull";
            string targetLabel = target != null ? target.UnitName : "null";
            string skillLabel = decision != null && decision.SelectedSkill != null ? decision.SelectedSkill.skillName : "null";
            //Debug.Log(
            //    $"[EnemyAI/Debug] DecideAction result: self={self.UnitName}, action={actionTypeLabel}, target={targetLabel}, selectedSkill={skillLabel}");
        }

        if (target == null)
        {
            target = targets.Find(t => t != null && !t.IsDead);
            if (target == null)
            {
                Debug.LogWarning($"[EnemyScript] AI 타겟이 없어 행동을 건너뜁니다: unit={self.UnitName}");
                yield break;
            }

            Debug.LogWarning(
                $"[EnemyScript] AI 결정 실패. 기본 공격으로 대체합니다: unit={self.UnitName}, aiNull={currentAI == null}, aliveTargets={targets.Count}, targetList=[{FormatTargets(targets)}]");
            yield return StartCoroutine(battleManager.ExecuteBasicAttack(self, target));
            yield break;
        }

        EnemyActionType actionType = decision != null ? decision.ActionType : EnemyActionType.BasicAttack;
        switch (actionType)
        {
            case EnemyActionType.ClassSkill:
                SkillData classSkill = decision != null ? decision.SelectedSkill : self.SelectedSkillData;
                if (classSkill != null)
                {
                    yield return StartCoroutine(battleManager.ExecuteGridSkill(self, target, classSkill));
                }
                else
                {
                    Debug.LogWarning($"[EnemyScript] ClassSkill 선택이지만 스킬이 없어 기본 공격으로 대체: unit={self.UnitName}");
                    yield return StartCoroutine(battleManager.ExecuteBasicAttack(self, target));
                }
                break;

            case EnemyActionType.WeaponSkill:
                if (self.EquippedWeaponData != null)
                {
                    SkillData converted = self.EquippedWeaponData.ToSkillData();
                    yield return StartCoroutine(battleManager.ExecuteGridSkill(self, target, converted));
                }
                else
                {
                    Debug.LogWarning($"[EnemyScript] WeaponSkill 선택이지만 무기가 없어 기본 공격으로 대체: unit={self.UnitName}");
                    yield return StartCoroutine(battleManager.ExecuteBasicAttack(self, target));
                }
                break;

            case EnemyActionType.BasicAttack:
            default:
                yield return StartCoroutine(battleManager.ExecuteBasicAttack(self, target));
                break;
        }
    }

    public bool EnsureAIReady()
    {
        if (currentAI != null)
        {
            return true;
        }

        int aiIndex = ResolveAiIndex(enemyData);
        currentAI = EnemyAIFactory.CreateAI(aiIndex);
        bool success = currentAI != null;
        if (enableAIDebugLog)
        {
            string aiType = (enemyData as EnemyData)?.UnitAI;
            Debug.Log(
                $"[EnemyAI/Debug] EnsureAIReady: unit={name}, aiTypeRaw='{aiType}', aiIndex={aiIndex}, success={success}");
        }

        return success;
    }

    private static int ResolveAiIndex(UnitData data)
    {
        EnemyData typedEnemyData = data as EnemyData;
        string aiType = typedEnemyData != null ? typedEnemyData.UnitAI : string.Empty;
        if (!string.IsNullOrWhiteSpace(aiType) && int.TryParse(aiType.Trim(), out int aiIndex))
        {
            return aiIndex;
        }

        return 20001;
    }

    private static string FormatTargets(List<BattleCharactor> targets)
    {
        if (targets == null || targets.Count == 0)
        {
            return string.Empty;
        }

        var labels = new List<string>(targets.Count);
        for (int i = 0; i < targets.Count; i++)
        {
            BattleCharactor t = targets[i];
            if (t == null)
            {
                labels.Add("null");
                continue;
            }

            labels.Add($"{t.UnitName}(dead={t.IsDead},hp={t.CurrentHp:0.#},isPlayer={t.IsPlayer})");
        }

        return string.Join(", ", labels);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        var battle = GetComponent<BattleCharactor>();
        if (battle == null)
        {
            return;
        }

        if (enemyData != null && !string.IsNullOrWhiteSpace(enemyData.Name))
        {
            battle.SetUnitNameForSkillMatching(enemyData.Name);
        }

        StatBlock baseStats = inspectorBaseStats;

        battle.SetBaseStats(baseStats);
        battle.SetLevelScaling(false);
        battle.RecalculateStats(false);
        battle.ResolveSelectedSkill(false);
    }
#endif
}
