using UnityEngine;

/// <summary>
/// 자식 <c>model</c> 등 Animator가 붙은 오브젝트에서 클립 Animation Event를 수신해
/// 상위 <see cref="CharactorAnimationController"/>로 전달합니다.
/// </summary>
[DisallowMultipleComponent]
public class AnimationEventBridge : MonoBehaviour
{
    private CharactorAnimationController _owner;
    private UnitAnimationEventRouter _router;

    public void Bind(CharactorAnimationController owner) => _owner = owner;

    private UnitAnimationEventRouter Router
    {
        get
        {
            if (_router == null && _owner != null)
            {
                _router = _owner.GetComponent<UnitAnimationEventRouter>();
            }
            return _router;
        }
    }

    /// <summary>클립 이벤트 함수명과 동일해야 합니다. (OnHit은 기존 직결 유지)</summary>
    public void AniEvent_OnHit() => _owner?.AniEvent_OnHit();
    public void AniEvent_AdvanceCombo() => _owner?.AniEvent_AdvanceCombo();
    // 지정 시간(배속-초)만큼 애니를 정지 후 자동 재개. 차지/앞동작 홀드 연출용.
    public void AniEvent_HoldBegin(float seconds) => _owner?.AniEvent_HoldBegin(seconds);
    // 지정 블렌드(초)로 Idle로 CrossFade. 공격 중간 이탈 → Idle 블렌딩용.
    public void AniEvent_ReturnIdle(float seconds) => _owner?.AniEvent_ReturnIdle(seconds);

    // 연출 Cue 통일 이벤트: 유닛 라우터로 분배(이펙트+사운드). 라우터 없으면 no-op(휴면).
    public void AniEvent_PresentationCue(string cueName) => Router?.PresentationCue(cueName);
}
