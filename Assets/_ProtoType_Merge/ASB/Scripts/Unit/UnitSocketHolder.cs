using UnityEngine;

/// <summary>
/// Holds common unit socket references used by equipment and hit presentation.
/// </summary>
public class UnitSocketHolder : MonoBehaviour
{
    [Header("Weapon Sockets")]
    [SerializeField] private GameObject rightWeaponSocketObject;
    [SerializeField] private GameObject leftWeaponSocketObject;

    [Header("Hit Socket")]
    [SerializeField] private GameObject hitSocketObject;

    public Transform RightWeaponSocket => rightWeaponSocketObject != null ? rightWeaponSocketObject.transform : null;
    public Transform LeftWeaponSocket => leftWeaponSocketObject != null ? leftWeaponSocketObject.transform : null;
    public Transform HitSocket => hitSocketObject != null ? hitSocketObject.transform : null;

    public Transform GetWeaponSocket(bool rightHand)
    {
        return rightHand ? RightWeaponSocket : LeftWeaponSocket;
    }
}
