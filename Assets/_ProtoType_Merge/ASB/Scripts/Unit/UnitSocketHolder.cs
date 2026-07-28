using UnityEngine;

/// <summary>
/// Presentation Cue가 참조하는 유닛 공용 소켓 종류.
/// None은 소켓 미지정 → 기본 공격 소켓(UnitVisualProfile.AttackEffectSocket)으로 폴백.
/// </summary>
public enum UnitSocket
{
    None = 0,
    RightWeapon,
    LeftWeapon,
    Hit,
    Chest,
    Floor,
    RightFoot,
    LeftFoot,
    // 새 값은 반드시 맨 끝에 추가(기존 직렬화된 CueBinding.Socket int가 밀리지 않도록).
    Overhead,
    FrontProjectile,
    WeaponVfx,        // 왼손 장착 무기의 VfxOrigin(WeaponSocketHolder)으로 브리지. 없으면 손 마운트로 폴백.
    WeaponVfxRight,   // 오른손 장착 무기의 VfxOrigin.
}

/// <summary>
/// Holds common unit socket references used by equipment and hit presentation.
/// </summary>
public class UnitSocketHolder : MonoBehaviour
{
    [Header("Weapon Sockets")]
    [SerializeField] private GameObject rightWeaponSocketObject;
    [SerializeField] private GameObject leftWeaponSocketObject;

    [Header("Foot Sockets")]
    [SerializeField] private GameObject rightFootSocketObject;
    [SerializeField] private GameObject leftFootSocketObject;

    [Header("Hit Socket")]
    [SerializeField] private GameObject hitSocketObject;

    [Header("Effect Sockets")]
    [SerializeField] private GameObject chestSocketObject;
    [SerializeField] private GameObject floorSocketObject;

    [Header("Air Socket")]
    [Tooltip("공중 부유 발사/캐스트 지점. 캐릭터 루트의 자식으로 공중에 offset해 두면 발사점이 공중에 유지됩니다.")]
    [SerializeField] private GameObject overheadSocketObject;

    [Header("Projectile Socket")]
    [Tooltip("투사체를 차징하거나 발사하기 전, 캐릭터 전방에 배치할 기준 소켓입니다.")]
    [SerializeField] private GameObject frontProjectileSocketObject;

    [Header("Scale Reference")]
    [SerializeField]
    [Tooltip("캐릭터 전체 크기를 결정하는 모델 루트. UseVisualRoot 이펙트 스케일 모드에서 사용.")]
    private Transform visualRoot;

    public Transform RightWeaponSocket => rightWeaponSocketObject != null ? rightWeaponSocketObject.transform : null;
    public Transform LeftWeaponSocket => leftWeaponSocketObject != null ? leftWeaponSocketObject.transform : null;
    public Transform RightFootSocket => rightFootSocketObject != null ? rightFootSocketObject.transform : null;
    public Transform LeftFootSocket => leftFootSocketObject != null ? leftFootSocketObject.transform : null;
    public Transform HitSocket => hitSocketObject != null ? hitSocketObject.transform : null;
    public Transform ChestSocket => chestSocketObject != null ? chestSocketObject.transform : null;
    public Transform FloorSocket => floorSocketObject != null ? floorSocketObject.transform : null;
    public Transform OverheadSocket => overheadSocketObject != null ? overheadSocketObject.transform : null;
    public Transform FrontProjectileSocket => frontProjectileSocketObject != null ? frontProjectileSocketObject.transform : null;
    public Transform VisualRoot => visualRoot;

    /// <summary>
    /// 명시적 루트가 없으면 공통 모델 프리팹과 전투 유닛 루트 사이의 최상위 비주얼 컨테이너를 런타임에 찾는다.
    /// </summary>
    public Transform ResolveVisualRoot(Transform casterRoot)
    {
        if (visualRoot != null)
        {
            return visualRoot;
        }

        Transform resolved = transform;
        while (resolved.parent != null && resolved.parent != casterRoot)
        {
            resolved = resolved.parent;
        }

        return resolved;
    }

    public Transform GetWeaponSocket(bool rightHand)
    {
        return rightHand ? RightWeaponSocket : LeftWeaponSocket;
    }

    /// <summary>
    /// 지정한 손에 장착된 무기의 소켓 홀더를 해당 손 마운트(Left/RightWeaponSocket) 하위에서 탐색한다.
    /// 쌍수(양손 각각 무기)를 지원하기 위해 손별로 개별 조회한다. 무기가 없으면 null
    /// (예: 근접 유닛은 손 소켓 자체를 이펙트 소켓으로 쓴다).
    /// 지금은 무기가 프리팹에 박혀 있어 마운트 하위 탐색으로 충분하고,
    /// 런타임 무기 교체가 도입되면 여기가 갱신 지점이 된다.
    /// </summary>
    public WeaponSocketHolder GetEquippedWeapon(bool rightHand)
    {
        Transform mount = GetWeaponSocket(rightHand);
        return mount != null ? mount.GetComponentInChildren<WeaponSocketHolder>(true) : null;
    }

    /// <summary>
    /// 무기 고유 발사/이펙트 원점을 조회하는 단일 API.
    /// 우선순위: 지정 손의 장착 무기 소켓 → 해당 손 마운트(기존 Left/Right WeaponSocket) → null.
    /// 쌍수도 <paramref name="rightHand"/>로 좌/우 무기를 각각 정확히 해석한다.
    /// 전투 코드는 LeftWeaponSocket을 직접 참조하지 말고 이 메서드를 사용한다.
    /// </summary>
    /// <param name="socket">조회할 무기 소켓 종류(Muzzle/VfxOrigin/TrailOrigin).</param>
    /// <param name="rightHand">오른손 무기/마운트를 기준으로 해석할지 여부. 기본은 왼손.</param>
    public Transform ResolveWeaponOrigin(WeaponSocket socket, bool rightHand = false)
    {
        WeaponSocketHolder weapon = GetEquippedWeapon(rightHand);
        Transform origin = weapon != null ? weapon.GetSocket(socket) : null;
        if (origin != null)
        {
            return origin;
        }

        return GetWeaponSocket(rightHand);
    }

    /// <summary>Presentation Cue의 Socket 종류로 공용 소켓 Transform을 조회. 미지정/미설정은 null.</summary>
    public Transform GetNamedSocket(UnitSocket socket)
    {
        switch (socket)
        {
            case UnitSocket.RightWeapon:
                return RightWeaponSocket;
            case UnitSocket.LeftWeapon:
                return LeftWeaponSocket;
            case UnitSocket.RightFoot:
                return RightFootSocket;
            case UnitSocket.LeftFoot:
                return LeftFootSocket;
            case UnitSocket.Hit:
                return HitSocket;
            case UnitSocket.Chest:
                return ChestSocket;
            case UnitSocket.Floor:
                return FloorSocket;
            case UnitSocket.Overhead:
                return OverheadSocket;
            case UnitSocket.FrontProjectile:
                return FrontProjectileSocket;
            case UnitSocket.WeaponVfx:
                // 왼손 장착 무기의 VFX 원점으로 브리지. 무기/소켓 없으면 손 마운트로 폴백.
                return ResolveWeaponOrigin(WeaponSocket.VfxOrigin, false);
            case UnitSocket.WeaponVfxRight:
                // 오른손 장착 무기의 VFX 원점으로 브리지.
                return ResolveWeaponOrigin(WeaponSocket.VfxOrigin, true);
            default:
                return null;
        }
    }
}
