using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 이펙트·팝업 소켓 위치 정의. BattleVisualDirector가 이펙트 생성 시 참조합니다.
/// </summary>
public class UnitVisualProfile : MonoBehaviour
{
    [Header("Effect Sockets")]
    [Tooltip("공격 이펙트 생성 위치 (없으면 unit root 사용)")]
    public Transform AttackEffectSocket;
    [Tooltip("피격 이펙트 생성 위치 (없으면 unit root 사용)")]
    public Transform HitEffectSocket;

    [Header("Popup")]
    [Tooltip("데미지 팝업 앵커 위치 (없으면 unit root + offset 사용)")]
    public Transform DamagePopupSocket;

    [Header("Hit Timing")]
    [Tooltip("피격 처리 시작 후 데미지·피격 이펙트·데미지 팝업까지 대기(초). (근접/비화살)")]
    public float HitDamagePopupDelay;
    [Tooltip("피격 처리 시작 후 Hit 애니 시작까지 대기(초). (근접/비화살)")]
    public float HitAnimationDelay;

    [Header("Arrow (Archer)")]
    [Tooltip("평소 손에 들고 있는 화살. 발사 시 비활성화됩니다.")]
    public GameObject HoldArrow;
    [FormerlySerializedAs("ArrowHitDelay")]
    [Tooltip("ResolveHitAction(투사체 없는 fallback 경로) 기준 데미지·피격 이펙트·데미지 팝업까지 대기(초).")]
    public float ArrowDamagePopupDelay = 0.2f;
}
