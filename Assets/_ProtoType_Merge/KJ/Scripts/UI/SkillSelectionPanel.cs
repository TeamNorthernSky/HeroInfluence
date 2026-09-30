using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>자동 해금된 스킬 한 개를 안내합니다. 확인은 장착·해금 상태를 변경하지 않습니다.</summary>
public class SkillSelectionPanel : MonoBehaviour
{
    [Tooltip("신규 습득 또는 강화 안내 문구입니다. 제목과 나머지 레이아웃은 공통입니다.")]
    [SerializeField] private TextMeshProUGUI announcementText;
    [Tooltip("이번에 해금된 스킬의 이름입니다.")]
    [SerializeField] private TextMeshProUGUI skillNameText;
    [Tooltip("이번 스킬의 현재 강화 계수를 반영한 효과 설명입니다.")]
    [SerializeField] private TextMeshProUGUI effectText;
    [Tooltip("이번에 해금된 스킬의 아이콘입니다.")]
    [SerializeField] private Image skillImage;
    [Tooltip("스킬을 습득한 히어로의 원형 초상화입니다.")]
    [SerializeField] private Image portraitImage;
    [Tooltip("안내를 닫고 다음 스킬 또는 전투 결과를 표시합니다. 해금 취소 기능은 없습니다.")]
    [SerializeField] private Button confirmButton;

    // 기존 호출 계약 유지. 알림 완료는 -1로 전달하여 이전의 단일 장착 변경을 하지 않습니다.
    public event System.Action<int> OnCompleted;
    private bool completed;

    private void Awake() => confirmButton?.onClick.AddListener(OnConfirm);

    public void Setup(UnitRewardPreview preview)
    {
        int id = preview.UnlockCandidateSkillIds != null && preview.UnlockCandidateSkillIds.Count > 0
            ? preview.UnlockCandidateSkillIds[0] : 0;
        Setup(preview, id);
    }

    public void Setup(UnitRewardPreview preview, int skillId)
    {
        completed = false;
        bool upgraded = HeroSkillRules.IsCurrentHeroSkill(skillId) && HeroSkillRules.FamilyId(skillId) != skillId;
        if (announcementText != null)
            announcementText.text = upgraded ? "히어로의 스킬이 강화되었습니다!" : "새로운 히어로 스킬을 습득했습니다!";
        var catalog = DHCsvTemplateCatalog.Instance;
        DHClassSkillTemplate skill = null;
        catalog?.TryGetClassSkillTemplate(skillId, out skill);
        int level = GameManager.Instance != null && GameManager.Instance.Lab != null
            ? GameManager.Instance.Lab.GetSkillLevel(preview.UnitIndex, skillId) : 1;
        level = Mathf.Max(1, level);
        if (skillNameText != null) skillNameText.text = skill != null ? skill.SkillName : $"스킬 {skillId}";
        if (effectText != null)
        {
            float value = skill != null ? catalog.GetClassSkillValueAtLevel(skillId, level) : 0;
            float subValue = skill != null ? catalog.GetClassSkillSubValueAtLevel(skillId, level) : 0;
            effectText.text = skill != null
                ? ClassSkillTooltipText.ReplaceBattleCoefficients(skill.Description, "ClassSkill", value, subValue)
                : "스킬 정보를 불러올 수 없습니다.";
        }
        SetSprite(skillImage, Sprites.Icon.ClassSkill(skillId, level));
        SetSprite(portraitImage, Sprites.Portrait.HeroByUnit(preview.UnitIndex));
    }

    private static void SetSprite(Image image, Sprite sprite)
    {
        if (image == null) return;
        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private void OnConfirm()
    {
        if (completed) return;
        completed = true;
        gameObject.SetActive(false);
        OnCompleted?.Invoke(-1);
        Destroy(gameObject);
    }
}
