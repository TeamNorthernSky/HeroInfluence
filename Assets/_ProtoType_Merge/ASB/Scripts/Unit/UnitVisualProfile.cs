using UnityEngine;

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

    [Header("Arrow (Archer)")]
    [Tooltip("평소 손에 들고 있는 화살. 발사 시 비활성화됩니다.")]
    public GameObject HoldArrow;
    [Tooltip("타겟에 꽂히는 화살. 평소 비활성화, 발사 시 ArrowHitPoint 위치에 표시됩니다.")]
    public GameObject TargetArrow;
    [Tooltip("타겟 몸의 화살 꽂힘 위치. 없으면 target.transform을 사용합니다.")]
    public Transform ArrowHitPoint;
}
