using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [JC 신설 260513] PartyInfoModal의 1슬롯. 프로필/이름/클래스/레벨/HP·IP 게이지 표시.
// 클릭 시 CharacterDetailModal.Open(unitIndex) — LIFO 스택 위에 세부 모달.
[DisallowMultipleComponent]
public class PartyMemberSlotView : MonoBehaviour
{
    [SerializeField] private Image profileImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text classText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Image hpFill;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private Image ipFill;
    [SerializeField] private TMP_Text ipText;
    [SerializeField] private Button button;

    private const float InfluencePowerMax = 200f;  // StatBlock.Influence Clamp 상한

    private int unitIndex;
    private CharacterDetailModal detailModal;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnClick);
    }

    public void SetDetailModal(CharacterDetailModal modal) => detailModal = modal;

    private bool isEnemy;

    public void Bind(int unitIndex, bool isEnemy)
    {
        this.unitIndex = unitIndex;
        this.isEnemy = isEnemy;

        if (unitIndex <= 0)
        {
            ApplyEmpty();
            return;
        }

        string name; string cls; int level; float maxHp; float curHp; float curIp;
        if (isEnemy)
        {
            PersistentEnemyRepository enemyRepo = PersistentEnemyRepository.Instance;
            if (enemyRepo == null || !enemyRepo.TryGetUnit(unitIndex, out EnemyUnitPersistentData edata))
            {
                ApplyEmpty();
                return;
            }

            DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
            EnemyData template = null;
            catalog?.TryGetEnemyTemplate(edata.UnitTemplateKey, out template);

            name = template != null && !string.IsNullOrWhiteSpace(template.Name) ? template.Name : edata.UnitTemplateKey;
            cls = template != null ? template.UnitType : "-";
            level = edata.Level;
            maxHp = Mathf.Max(0f, edata.IngameStats.HP);
            curHp = Mathf.Clamp(edata.CurrentHp, 0f, maxHp);
            curIp = Mathf.Clamp(edata.IngameStats.Influence, 0f, InfluencePowerMax);
        }
        else
        {
            PersistentUnitRepository repo = PersistentUnitRepository.Instance;
            if (repo == null || !repo.TryGetUnit(unitIndex, out UnitPersistentData data))
            {
                ApplyEmpty();
                return;
            }

            DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
            UnitData template = null;
            catalog?.TryGetPlayerTemplate(data.UnitTemplateKey, out template);

            name = template != null && !string.IsNullOrWhiteSpace(template.Name) ? template.Name : data.UnitTemplateKey;
            cls = template != null ? template.UnitType : "-";
            level = data.Level;
            maxHp = Mathf.Max(0f, data.IngameStats.HP);
            curHp = Mathf.Clamp(data.CurrentHp, 0f, maxHp);
            curIp = Mathf.Clamp(data.IngameStats.Influence, 0f, InfluencePowerMax);
        }

        if (nameText != null) nameText.text = name;
        if (classText != null) classText.text = cls;
        if (levelText != null) levelText.text = $"Lv {level}";
        if (hpFill != null) hpFill.fillAmount = maxHp > 0f ? curHp / maxHp : 0f;
        if (hpText != null) hpText.text = $"{Mathf.RoundToInt(curHp)}/{Mathf.RoundToInt(maxHp)}";
        if (ipFill != null) ipFill.fillAmount = InfluencePowerMax > 0f ? curIp / InfluencePowerMax : 0f;
        if (ipText != null) ipText.text = $"{Mathf.RoundToInt(curIp)}/{Mathf.RoundToInt(InfluencePowerMax)}";
        if (button != null) button.interactable = true;
    }

    private void ApplyEmpty()
    {
        if (nameText != null) nameText.text = "-";
        if (classText != null) classText.text = "-";
        if (levelText != null) levelText.text = "";
        if (hpFill != null) hpFill.fillAmount = 0f;
        if (hpText != null) hpText.text = "";
        if (ipFill != null) ipFill.fillAmount = 0f;
        if (ipText != null) ipText.text = "";
        if (button != null) button.interactable = false;
    }

    private void OnClick()
    {
        if (detailModal == null || unitIndex <= 0) return;
        detailModal.Open(unitIndex, isEnemy);
    }
}
