using UnityEngine;

/// <summary>튜토리얼 파티의 템플릿 키로 탐사 HUD를 표시한다.</summary>
[DisallowMultipleComponent]
public sealed class TutorialExploreHeroList : MonoBehaviour
{
    [SerializeField] private TutorialPartyComposition party;
    [SerializeField] private TutorialCatalog catalog;
    [SerializeField] private ExplorationHeroBoxController.HeroSlot[] slots =
        System.Array.Empty<ExplorationHeroBoxController.HeroSlot>();

    private float nextRefresh;

    private void OnEnable() => Refresh();

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.3f;
        Refresh();
    }

    public void Refresh()
    {
        if (gameObject.scene.name != "TutorialExploreScene") return;
        if (party == null) party = FindFirstObjectByType<TutorialPartyComposition>();
        if (catalog == null) catalog = TutorialCatalog.Instance;
        if (catalog == null) catalog = FindFirstObjectByType<TutorialCatalog>();

        var keys = party != null ? party.GetJoinedUnitTemplateKeys() : null;
        var repository = TutorialProgressRepository.Instance;
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;
            bool visible = keys != null && i < keys.Count;
            if (slot.root != null) slot.root.SetActive(visible);
            if (!visible) continue;

            string key = keys[i];
            DHPlayerUnitTemplate template = null;
            if (catalog != null) catalog.TryGetPlayerUnitTemplate(key, out template);
            TutorialUnitProgressState state = null;
            if (repository != null) repository.TryGetUnitState(key, out state);

            // 초기화 전에는 기본값을 표시하고, 진행 데이터가 생기면 현재값을 사용한다.
            bool hasStats = state != null && (state.MaxHp > 0 || state.MaxIp > 0);
            float maxHp = hasStats ? state.MaxHp : (template != null ? template.BaseStats.HP : 0f);
            float hp = hasStats ? state.CurrentHp : maxHp;
            float maxIp = hasStats ? state.MaxIp : (template != null ? template.BaseStats.Influence : 0f);
            float ip = hasStats ? state.CurrentIp : maxIp;

            if (slot.profile != null)
            {
                var portrait = Sprites.Portrait.Hero(key);
                slot.profile.sprite = portrait;
                slot.profile.enabled = portrait != null;
            }
            if (slot.nameText != null)
                slot.nameText.text = template != null ? template.UnitName : key;
            if (slot.hpText != null) slot.hpText.text = $"{hp:F0}/{maxHp:F0}";
            if (slot.hpFill != null) slot.hpFill.fillAmount = maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;
            if (slot.ipText != null) slot.ipText.text = $"{ip:F0}";
            if (slot.ipMaxIcon != null) slot.ipMaxIcon.SetActive(maxIp > 0f && ip >= maxIp);
        }
    }
}
