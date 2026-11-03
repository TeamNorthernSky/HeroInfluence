using System;
using UnityEngine;

/// <summary>애니메이션 테스트 씬에서 보여 줄 네코밍 스킬 이펙트.</summary>
public enum AnimTestSkill
{
    Heal4010,
    PawForYou4020,
    FightingLove4030,
    Taosenaiyo4040,
    /// <summary>이펙트 없이 애니메이션만 본다.</summary>
    None,
}

/// <summary>
/// 애니메이션 테스트 씬에서 숫자키 1~4로 <see cref="AnimTestSkillFx"/>들이 재생할 스킬(4010~4040)을 바꾸고, 5로 이펙트를 끈다.
/// 스킬별 이펙트 프리팹도 여기 한곳에 모아 두고, 모델마다 붙은 AnimTestSkillFx는 시점·위치만 가진다.
/// <para>씬에 하나만 둔다. 전투 시스템과 무관한 테스트 전용이다.</para>
/// </summary>
public class AnimTestSkillFxSet : MonoBehaviour
{
    [Header("4010 (heal_cast 98018 / ProjectileVisual / HitEffectId 98019)")]
    [SerializeField] private GameObject healCharge;
    [SerializeField] private GameObject healProjectile;
    [SerializeField] private GameObject healHit;

    [Header("4020 (charge 98022)")]
    [SerializeField] private GameObject pawForYouCast;

    [Header("4030 (charge_r·charge_l 98024 / parts_merge 98026 / ProjectileVisual)")]
    [SerializeField] private GameObject fightingLoveCharge;
    [SerializeField] private GameObject fightingLoveMerge;
    [SerializeField] private GameObject fightingLoveProjectile;

    [Header("4040 (revive 98030 / tao_circle 98046 / fire 98031)")]
    [SerializeField] private GameObject taoRevive;
    [SerializeField] private GameObject taoCircle;
    [SerializeField] private GameObject taoBarrage;

    [Header("무기 소켓 (Unit_Supporter_10004의 R_Weapon·L_Weapon, 손 뼈 기준 로컬 위치)")]
    [Tooltip("테스트 모델에는 전투 프리팹의 무기 소켓이 없어서 같은 로컬 위치로 계산한다. 4030 구체·합체 위치.")]
    [SerializeField] private Vector3 rightWeaponLocal;
    [SerializeField] private Vector3 leftWeaponLocal;

    [Tooltip("Play를 시작할 때 재생할 스킬.")]
    [SerializeField] private AnimTestSkill current = AnimTestSkill.Heal4010;

    // 순서가 AnimTestSkill 값과 같다(5 = None).
    private static readonly KeyCode[] AlphaKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 };
    private static readonly KeyCode[] KeypadKeys = { KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5 };

    private GUIStyle _labelStyle;

    public static AnimTestSkillFxSet Instance { get; private set; }

    /// <summary>스킬이 바뀌면 알린다. 각 모델은 진행 중인 이펙트를 정리하고 다음 반복부터 새 스킬을 재생한다.</summary>
    public static event Action<AnimTestSkill> SkillChanged;

    public AnimTestSkill Current => current;
    public GameObject HealCharge => healCharge;
    public GameObject HealProjectile => healProjectile;
    public GameObject HealHit => healHit;
    public GameObject PawForYouCast => pawForYouCast;
    public GameObject FightingLoveCharge => fightingLoveCharge;
    public GameObject FightingLoveMerge => fightingLoveMerge;
    public GameObject FightingLoveProjectile => fightingLoveProjectile;
    public GameObject TaoRevive => taoRevive;
    public GameObject TaoCircle => taoCircle;
    public GameObject TaoBarrage => taoBarrage;
    public Vector3 RightWeaponLocal => rightWeaponLocal;
    public Vector3 LeftWeaponLocal => leftWeaponLocal;

    /// <summary>스킬 번호(4010~4040). None이면 0.</summary>
    public static int SkillIndex(AnimTestSkill skill) => skill == AnimTestSkill.None ? 0 : 4010 + (int)skill * 10;

    private void OnEnable() => Instance = this;

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        for (int i = 0; i < AlphaKeys.Length; i++)
        {
            if (Input.GetKeyDown(AlphaKeys[i]) || Input.GetKeyDown(KeypadKeys[i])) SetSkill((AnimTestSkill)i);
        }
    }

    public void SetSkill(AnimTestSkill skill)
    {
        if (skill == current) return;
        current = skill;
        SkillChanged?.Invoke(skill);
    }

    // 기본 GUI 폰트에 한글이 없을 수 있어 영문으로 표시한다.
    private void OnGUI()
    {
        if (_labelStyle == null) _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        string skill = current == AnimTestSkill.None ? "Off" : SkillIndex(current).ToString();
        GUI.Label(new Rect(16f, 10f, 760f, 34f), $"Skill FX {skill}   (keys 1-4 : 4010-4040, 5 : Off)", _labelStyle);
    }
}
