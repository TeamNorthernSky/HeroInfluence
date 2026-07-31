using System;
using UnityEngine;
using GridCellRef = ASB.Work.BattleGrid.GridCell;

[DisallowMultipleComponent]
public sealed class HostageBattleActor : MonoBehaviour, ISkillTarget
{
    // ── ISkillTarget: 인질은 연출/타깃수집 공통 경로에 위치로만 참여하고, 피해는 ApplyFriendlyDamage로 분기한다.
    //    (BattleCharactor로 캐스팅/컴포넌트 추가하지 않기 위한 공통 seam)
    UnitTargetCategory ISkillTarget.TargetCategory => UnitTargetCategory.Hostage;
    Transform ISkillTarget.TargetTransform => transform;
    Vector3 ISkillTarget.TargetPosition => transform.position;
    bool ISkillTarget.IsValidSkillTarget => IsSafe;
    GridCellRef ISkillTarget.TargetCell => GetComponentInParent<GridCellRef>();

    private HostageSpawnConfig config;
    private Renderer[] cachedRenderers = Array.Empty<Renderer>();
    private Color[] initialColors = Array.Empty<Color>();

    public string HostageId => config != null ? config.HostageId : string.Empty;
    public int Slot => config != null ? config.Slot : 0;
    public float MaxHp { get; private set; }
    public float CurrentHp { get; private set; }
    public float Defense { get; private set; }
    public float HostageAggro { get; private set; }
    public HostageBattleState State { get; private set; } = HostageBattleState.Safe;
    public bool IsSafe => State == HostageBattleState.Safe;

    public event Action<HostageBattleActor> Injured;

    public void Initialize(HostageSpawnConfig nextConfig)
    {
        config = nextConfig ?? throw new ArgumentNullException(nameof(nextConfig));
        MaxHp = Mathf.Max(1f, config.MaxHp);
        CurrentHp = Mathf.Clamp(config.InitialHp, 0f, MaxHp);
        Defense = Mathf.Max(0f, config.Defense);
        HostageAggro = Mathf.Max(0f, config.InitialAggro);
        State = CurrentHp > 0f ? HostageBattleState.Safe : HostageBattleState.Injured;
        CacheVisualColors();
        RefreshVisual();
    }

    public void AdvanceRound(float fullHealthAggroGain, float damagedAggroGain)
    {
        if (!IsSafe)
            return;

        bool isFullHealth = Mathf.Approximately(CurrentHp, MaxHp);
        float gain = isFullHealth ? fullHealthAggroGain : damagedAggroGain;
        HostageAggro = Mathf.Max(0f, HostageAggro + Mathf.Max(0f, gain));
    }

    public void ApplyThreatDamage(float damage, float aggroReduction)
    {
        if (!IsSafe)
            return;

        CurrentHp = Mathf.Max(0f, CurrentHp - Mathf.Max(0f, damage));
        HostageAggro = Mathf.Max(0f, HostageAggro - Mathf.Max(0f, aggroReduction));
        if (CurrentHp <= 0f)
        {
            State = HostageBattleState.Injured;
            HostageAggro = 0f;
            RefreshVisual();
            Injured?.Invoke(this);
            return;
        }

        RefreshVisual();
    }

    /// <summary>
    /// 아군 공격에 의한 인질 피해입니다. 적 위협과 달리 인질 방어력을 적용합니다.
    /// </summary>
    public void ApplyFriendlyDamage(float damage)
    {
        if (!IsSafe)
            return;

        float finalDamage = Mathf.Max(0f, damage - Defense);
        CurrentHp = Mathf.Max(0f, CurrentHp - finalDamage);
        if (CurrentHp <= 0f)
        {
            State = HostageBattleState.Injured;
            HostageAggro = 0f;
            RefreshVisual();
            Injured?.Invoke(this);
            return;
        }

        RefreshVisual();
    }

    private void CacheVisualColors()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        initialColors = new Color[cachedRenderers.Length];
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer targetRenderer = cachedRenderers[i];
            initialColors[i] = targetRenderer != null && targetRenderer.material != null
                ? targetRenderer.material.color
                : Color.white;
        }
    }

    private void RefreshVisual()
    {
        Color injuryTint = new Color(0.35f, 0.35f, 0.35f, 1f);
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer targetRenderer = cachedRenderers[i];
            if (targetRenderer == null || targetRenderer.material == null)
                continue;

            targetRenderer.material.color = IsSafe ? initialColors[i] : injuryTint;
        }
    }
}
