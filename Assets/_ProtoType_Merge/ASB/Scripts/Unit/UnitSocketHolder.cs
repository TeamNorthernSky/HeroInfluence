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
            default:
                return null;
        }
    }
}
