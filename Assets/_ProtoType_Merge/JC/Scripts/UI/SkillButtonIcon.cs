using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// [JC 260622] 전투 스킬버튼(클래스/무기스킬)의 SkillIcon을 현재 턴 유닛 정보로 라이브러리에서 동적 출력.
/// SkillIcon(Image) GameObject에 부착. skillType로 클래스스킬/무기스킬 구분(SkillButtonTooltip과 동일 enum).
/// 아이콘 해석은 HeroIcons(클래스스킬=GetClassSkillIcon / 무기스킬=GetWeaponSkillIcon)로 통일.
/// 적 턴에는 갱신하지 않음(플레이어 스킬 아이콘 유지).
/// </summary>
public class SkillButtonIcon : MonoBehaviour
{
    [SerializeField] private SkillButtonType skillType = SkillButtonType.ClassSkill;
    [Tooltip("비우면 같은 GameObject의 Image 사용")]
    [SerializeField] private Image iconImage;
    [SerializeField] private BattleFlowManager battleFlowManager;

    private void Awake()
    {
        if (iconImage == null) iconImage = GetComponent<Image>();
        if (battleFlowManager == null) battleFlowManager = FindFirstObjectByType<BattleFlowManager>();
    }

    private void OnEnable()
    {
        if (battleFlowManager != null) battleFlowManager.OnTurnStarted += OnTurnStarted;
    }

    private void OnDisable()
    {
        if (battleFlowManager != null) battleFlowManager.OnTurnStarted -= OnTurnStarted;
    }

    private void OnTurnStarted(int round, BattleCharactor unit)
    {
        if (iconImage == null || unit == null || !unit.IsPlayer) return;
        Sprite sp = Resolve(unit);
        if (sp != null) { iconImage.sprite = sp; iconImage.enabled = true; }
    }

    private Sprite Resolve(BattleCharactor unit)
    {
        var gm = GameManager.Instance;
        int uIdx = unit.SourceData != null ? unit.SourceData.UnitIndex : 0;

        if (skillType == SkillButtonType.ClassSkill)
        {
            unit.ResolveSelectedSkill();
            SkillData sd = unit.SelectedSkillData;
            if (sd == null) return null;
            int lv = (gm != null && gm.Lab != null && uIdx > 0)
                ? gm.Lab.GetSkillLevel(uIdx, sd.skillIndex)
                : (unit.SourceData != null ? unit.SourceData.SkillLevel : 1);
            return HeroIcons.GetClassSkillIcon(sd.skillIndex, lv);
        }

        WeaponData wd = unit.EquippedWeaponData;
        if (wd == null || wd.WeaponSkillIndex <= 0) return null;
        int wIdx = unit.EquippedWeaponIndex > 0
            ? unit.EquippedWeaponIndex
            : (unit.SourceData != null ? unit.SourceData.CurrentWeaponIndex : 0);
        int wl = (gm != null && gm.Workshop != null && uIdx > 0)
            ? gm.Workshop.GetWeaponLevel(uIdx, wIdx)
            : 1;
        return HeroIcons.GetWeaponSkillIcon(wd.WeaponSkillIndex, wl);
    }
}
