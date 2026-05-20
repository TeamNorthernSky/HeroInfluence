using UnityEngine;

/// <summary>
/// 자식 <c>model</c> 등 Animator가 붙은 오브젝트에서 클립 Animation Event를 수신해
/// 상위 <see cref="CharactorAnimationController"/>로 전달합니다.
/// </summary>
[DisallowMultipleComponent]
public class AnimationEventBridge : MonoBehaviour
{
    private CharactorAnimationController _owner;

    public void Bind(CharactorAnimationController owner) => _owner = owner;

    /// <summary>클립 이벤트 함수명과 동일해야 합니다.</summary>
    public void AniEvent_OnHit() => _owner?.AniEvent_OnHit();
}
