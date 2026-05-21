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

    /// <summary>런타임 전투 식별자. 영속 저장소 키로 사용하지 마세요(인스턴스마다 달라짐).</summary>
    public string UnitID
    {
        get
        {
            if (enemyData == null || string.IsNullOrWhiteSpace(enemyData.Index))
            {
                return gameObject.GetInstanceID().ToString();
            }

            return $"{enemyData.Index.Trim()}_{gameObject.GetInstanceID()}";
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

    public void Initialize(EnemyUnitPersistentData persistentData, EnemyData fallbackData = null)
    {
        if (persistentData == null)
        {
            Initialize(fallbackData);
            return;
        }

        enemyData = fallbackData;
        BattleCharactor battle = GetComponent<BattleCharactor>();
        if (battle == null)
        {
            return;
        }

        battle.BindPersistentEnemySourceData(persistentData);
        battle.SetBaseStats(persistentData.BaseStats);
        battle.SetLevelScaling(false);

        if (!string.IsNullOrWhiteSpace(persistentData.UnitTemplateKey))
        {
            battle.SetUnitNameForSkillMatching(persistentData.UnitTemplateKey);
        }
        else if (fallbackData != null)
        {
            battle.SetUnitNameForSkillMatching(fallbackData.Name);
        }

        int resolvedSkillIndex = ExtractSkillIndexFromPersistent(persistentData, fallbackData);
        int resolvedWeaponIndex = ExtractWeaponIndexFromPersistent(persistentData);
        battle.LoadPersistentEquipment(resolvedSkillIndex, resolvedWeaponIndex);

        battle.RecalculateStats();
        battle.InitializeCurrentHpToMax();
        battle.MarkInitializedFromDataPipeline(true);
        Debug.Log(
            $"[Stats/Persistent] {battle.UnitName} uses precomputed snapshot. " +
            $"LevelScaling=false, " +
            $"FinalStats HP={battle.FinalStats.HP}, Atk={battle.FinalStats.Atk}, DEF={battle.FinalStats.DEF}");

        enemyData = fallbackData;
        EnsureAIReady();
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
                    SkillData classSkillForExecution = PrepareEnemySkillExecutionCopy(classSkill);
                    yield return StartCoroutine(battleManager.ExecuteGridSkill(self, target, classSkillForExecution));
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
                    SkillData converted = PrepareEnemySkillExecutionCopy(self.EquippedWeaponData.ToSkillData());
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

    private static int ExtractWeaponIndexFromPersistent(EnemyUnitPersistentData persistentData)
    {
        if (persistentData == null)
        {
            return 0;
        }

        System.Reflection.PropertyInfo weaponProp =
            persistentData.GetType().GetProperty("CurrentWeaponIndex", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (weaponProp != null && weaponProp.GetValue(persistentData) is int propWeapon && propWeapon > 0)
        {
            return propWeapon;
        }

        System.Reflection.FieldInfo weaponField = persistentData.GetType().GetField(
            "currentWeaponIndex",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (weaponField != null && weaponField.GetValue(persistentData) is int fieldWeapon && fieldWeapon > 0)
        {
            return fieldWeapon;
        }

        return 0;
    }

    private static int ExtractSkillIndexFromPersistent(EnemyUnitPersistentData persistentData, EnemyData fallbackData)
    {
        return EnemySkillIndexResolver.ResolveSkillIndexFromPersistent(persistentData, fallbackData);
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

    /// <summary>적 스킬 실행용 복사본. 애니 필드가 비어 있으면 EnemyDataSheet 폴백 기본값을 채웁니다(원본 SkillData는 변경하지 않음).</summary>
    private static SkillData PrepareEnemySkillExecutionCopy(SkillData source)
    {
        if (source == null)
        {
            return null;
        }

        SkillData copy = CloneSkillData(source);
        if (!NeedsEnemyAnimationFallback(copy))
        {
            return copy;
        }

        if (string.IsNullOrWhiteSpace(copy.AnimationTrigger))
        {
            copy.AnimationTrigger = "Attack";
        }

        if (copy.HitDelay <= 0f)
        {
            copy.HitDelay = 0.25f;
        }

        if (copy.TotalDelay <= 0f)
        {
            copy.TotalDelay = 0.5f;
        }

        if (string.IsNullOrWhiteSpace(copy.TargetAnimationTrigger))
        {
            copy.TargetAnimationTrigger = "Hit";
        }

        copy.UseAnimEvent = false;
        return copy;
    }

    private static bool NeedsEnemyAnimationFallback(SkillData skill)
    {
        if (skill == null)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(skill.AnimationTrigger)
               || string.IsNullOrWhiteSpace(skill.TargetAnimationTrigger)
               || skill.HitDelay <= 0f
               || skill.TotalDelay <= 0f;
    }

    private static SkillData CloneSkillData(SkillData source)
    {
        return new SkillData
        {
            skillIndex = source.skillIndex,
            skillClass = source.skillClass,
            acquireLevel = source.acquireLevel,
            skillName = source.skillName,
            description = source.description,
            ipCost = source.ipCost,
            classSkillEffect = source.classSkillEffect,
            classSkillRange = source.classSkillRange,
            EnemySkill1Range = source.EnemySkill1Range,
            EnemySkill2Range = source.EnemySkill2Range,
            classSkillRangeLine = source.classSkillRangeLine,
            classSkillTarget = source.classSkillTarget,
            boundary = source.boundary != null ? new List<int>(source.boundary) : new List<int>(),
            multiTargetCount = source.multiTargetCount,
            skillValue = source.skillValue,
            skillSubValue = source.skillSubValue,
            AnimationTrigger = source.AnimationTrigger,
            StateName = source.StateName,
            UseAnimEvent = source.UseAnimEvent,
            HitDelay = source.HitDelay,
            TotalDelay = source.TotalDelay,
            TargetAnimationTrigger = source.TargetAnimationTrigger
        };
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
