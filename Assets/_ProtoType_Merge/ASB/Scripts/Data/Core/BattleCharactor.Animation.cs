using UnityEngine;

/// <summary>
/// <see cref="CharactorAnimationController"/> 중계. 메인 BattleCharactor.cs 스탯/전투 로직과 분리.
/// </summary>
public partial class BattleCharactor
{
    public CharactorAnimationController Anim { get; private set; }

    internal void EnsureAnimationController()
    {
        if (Anim == null)
        {
            Anim = GetComponent<CharactorAnimationController>();
        }

        if (Anim == null)
        {
            Anim = gameObject.AddComponent<CharactorAnimationController>();
        }

        if (Anim != null && BattleManager.Instance != null)
        {
            Anim.SetAnimationSpeed(BattleManager.Instance.CurrentBattleSpeed);
        }
    }

    /// <summary>레거시 클립 이벤트가 BattleCharactor에 걸려 있어도 컨트롤러로 전달합니다.</summary>
    public void AniEvent_OnHit()
    {
        EnsureAnimationController();
        Anim?.AniEvent_OnHit();
    }

    public void AniEvent_AdvanceCombo()
    {
        EnsureAnimationController();
        Anim?.AniEvent_AdvanceCombo();
    }

    /// <summary>클립 이벤트가 루트 BattleCharactor에 걸린 경우의 홀드(정지→재개) 전달.</summary>
    public void AniEvent_HoldBegin(float seconds)
    {
        EnsureAnimationController();
        Anim?.AniEvent_HoldBegin(seconds);
    }

    // 연출 Cue가 루트 BattleCharactor에 걸린 경우도 유닛 라우터로 분배. 라우터 없으면 no-op(휴면).
    public void AniEvent_PresentationCue(string cueName) => GetComponent<UnitAnimationEventRouter>()?.PresentationCue(cueName);

    /// <summary>
    /// 연출 재료(이벤트 기반) 사용 시 필요한 유닛 로컬 컴포넌트를 보장합니다.
    /// 재료를 쓰는 스킬에서만 시퀀서가 호출하므로, 일반 스킬 유닛에는 부착되지 않습니다.
    /// </summary>
    internal PresentationRuntimeContext EnsurePresentationComponents()
    {
        PresentationRuntimeContext ctx = GetComponent<PresentationRuntimeContext>();
        if (ctx == null) ctx = gameObject.AddComponent<PresentationRuntimeContext>();
        if (GetComponent<UnitEffectPresenter>() == null) gameObject.AddComponent<UnitEffectPresenter>();
        if (GetComponent<UnitSoundPresenter>() == null) gameObject.AddComponent<UnitSoundPresenter>();
        if (GetComponent<UnitAnimationEventRouter>() == null) gameObject.AddComponent<UnitAnimationEventRouter>();
        return ctx;
    }
}
