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
}
