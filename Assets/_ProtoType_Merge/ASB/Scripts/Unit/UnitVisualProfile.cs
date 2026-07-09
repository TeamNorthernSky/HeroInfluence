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
    [Tooltip("타겟에 꽂히는 화살 프리팹. 충돌 시 Instantiate 후 자동 Destroy됩니다.")]
    public GameObject TargetArrowPrefab;
    [Tooltip("타겟 몸의 화살 꽂힘 위치. 없으면 target.transform을 사용합니다.")]
    public Transform ArrowHitPoint;
    [Tooltip("꽂힌 화살이 사라지기까지 대기 시간(초).")]
    public float ArrowLifetime = 1.5f;
    [FormerlySerializedAs("ArrowHitDelay")]
    [Tooltip("ArrowImpactAction 종료 후 데미지·피격 이펙트·데미지 팝업까지 대기(초).")]
    public float ArrowDamagePopupDelay = 0.2f;
    [Tooltip("트레일 시작(또는 트레일 없을 때 화살 스폰) 후 타겟 Hit 애니까지 대기(초).")]
    public float ArrowHitAnimationDelay = 0.2f;

    [Header("Arrow Trail")]
    [Tooltip("포물선 궤적 LineRenderer 프리팹. 발사 시 Instantiate 후 자동 Destroy됩니다.")]
    public GameObject ArrowTrailPrefab;
    [Tooltip("포물선 호의 최고 높이.")]
    public float ArcHeight = 2f;
    [Tooltip("라인이 페이드아웃되는 시간(초).")]
    public float TrailFadeDuration = 0.3f;
}
