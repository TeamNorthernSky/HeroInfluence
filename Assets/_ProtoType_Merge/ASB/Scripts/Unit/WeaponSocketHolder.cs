using UnityEngine;

/// <summary>
/// 교체 가능한 무기 프리팹이 가지는 고유 소켓 종류.
/// 신체 고정 소켓(UnitSocket)과 달리 장착 무기마다 위치가 달라진다.
/// </summary>
public enum WeaponSocket
{
    Muzzle = 0,     // 총구/시위 등 발사 원점
    VfxOrigin,      // 무기 고유 VFX 생성 위치
    TrailOrigin,    // 궤적(트레일) 시작 위치
    // 새 값은 반드시 맨 끝에 추가(직렬화된 int가 밀리지 않도록).
}

/// <summary>
/// 무기 프리팹에 부착되어 발사/VFX/트레일 등 무기 고유 소켓을 관리한다.
/// 신체 고정 소켓은 <see cref="UnitSocketHolder"/>가 담당하고, 이 컴포넌트는
/// 교체 가능한 무기 쪽 소켓만 담당한다. 발사 원점 조회는
/// <see cref="UnitSocketHolder.ResolveWeaponOrigin"/>를 통해 이루어진다.
/// </summary>
public class WeaponSocketHolder : MonoBehaviour
{
    [Header("Weapon Sockets")]
    [Tooltip("총구/시위 등 투사체·화살 발사 원점.")]
    [SerializeField] private GameObject muzzleObject;
    [Tooltip("무기 고유 VFX 생성 위치. 없으면 Muzzle로 폴백하기 좋다.")]
    [SerializeField] private GameObject vfxOriginObject;
    [Tooltip("궤적(트레일) 시작 위치.")]
    [SerializeField] private GameObject trailOriginObject;

    public Transform Muzzle => muzzleObject != null ? muzzleObject.transform : null;
    public Transform VfxOrigin => vfxOriginObject != null ? vfxOriginObject.transform : null;
    public Transform TrailOrigin => trailOriginObject != null ? trailOriginObject.transform : null;

    /// <summary>무기 소켓 종류로 Transform 조회. 미설정이면 null.</summary>
    public Transform GetSocket(WeaponSocket socket)
    {
        switch (socket)
        {
            case WeaponSocket.Muzzle:
                return Muzzle;
            case WeaponSocket.VfxOrigin:
                return VfxOrigin;
            case WeaponSocket.TrailOrigin:
                return TrailOrigin;
            default:
                return null;
        }
    }
}
