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
    }

    /// <summary>레거시 클립 이벤트가 BattleCharactor에 걸려 있어도 컨트롤러로 전달합니다.</summary>
    public void AniEvent_OnHit()
    {
        EnsureAnimationController();
        Anim?.AniEvent_OnHit();
    }
}
