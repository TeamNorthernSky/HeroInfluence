using UnityEngine;
using UnityEngine.Events;

/// <summary>Instantiate 후 Open(unitIndex)로 대상을 지정한다. 기존 Workshop을 통해 장착한다.</summary>
[DisallowMultipleComponent]
public class ExplorationCoreSelectController : MonoBehaviour
{
    [SerializeField] private ExplorationCoreCardView[] cards = new ExplorationCoreCardView[5];
    [SerializeField] private Sprite[] coreIcons = new Sprite[5];
    [SerializeField] private string[] coreNames = { "기본 코어", "광역 코어", "방어 코어", "회복 코어", "위압 코어" };

    private int selectedUnitIndex = -1;
    private WorkshopManager workshop;
    private UnityAction[] clickHandlers;
    private bool refreshing;

    private void Awake()
    {
        clickHandlers = new UnityAction[cards.Length];
        for (int i = 0; i < cards.Length; i++)
        {
            int weaponIndex = i + 1;
            clickHandlers[i] = () => Equip(weaponIndex);
            if (cards[i] != null && cards[i].Button != null)
                cards[i].Button.onClick.AddListener(clickHandlers[i]);
        }
    }

    public void Open(int unitIndex)
    {
        selectedUnitIndex = unitIndex;
        gameObject.SetActive(true);
        BindWorkshop();
        Refresh();
    }

    public void Close() => gameObject.SetActive(false);

    private void OnEnable() { BindWorkshop(); Refresh(); }

    private void Update()
    {
        var current = GameManager.Instance != null ? GameManager.Instance.Workshop : null;
        if (current != workshop) { BindWorkshop(); Refresh(); }
    }

    private void BindWorkshop()
    {
        var current = GameManager.Instance != null ? GameManager.Instance.Workshop : null;
        if (current == workshop) return;
        if (workshop != null) workshop.OnStateChanged -= Refresh;
        workshop = current;
        if (workshop != null) workshop.OnStateChanged += Refresh;
    }

    private void OnDisable()
    {
        if (workshop != null) workshop.OnStateChanged -= Refresh;
        workshop = null;
        selectedUnitIndex = -1;
    }

    private void OnDestroy()
    {
        if (clickHandlers == null) return;
        for (int i = 0; i < cards.Length; i++)
            if (cards[i] != null && cards[i].Button != null)
                cards[i].Button.onClick.RemoveListener(clickHandlers[i]);
    }

    public void Refresh()
    {
        // IsOwned(기본 코어)의 최초 생성 이벤트로 인한 재진입 방지.
        if (refreshing) return;
        refreshing = true;
        try
        {
            var units = PersistentUnitRepository.Instance;
            bool hasUnit = selectedUnitIndex > 0 && units != null &&
                           units.TryGetUnit(selectedUnitIndex, out var unit) && unit != null;
            int equipped = workshop != null && hasUnit ? workshop.GetEquippedWeaponIndex(selectedUnitIndex) : 0;
            var catalog = DHCsvTemplateCatalog.Instance;
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null) continue;
                string label = i < coreNames.Length ? coreNames[i] : $"코어 {i + 1}";
                if (catalog != null && catalog.TryGetWeaponTemplate(i + 1, out var data) &&
                    data != null && !string.IsNullOrWhiteSpace(data.WeaponName)) label = data.WeaponName;
                bool owned = workshop != null && workshop.IsOwned(i + 1);
                cards[i].SetState(label, i < coreIcons.Length ? coreIcons[i] : null,
                    owned, hasUnit && equipped == i + 1, hasUnit);
            }
        }
        finally { refreshing = false; }
    }

    private void Equip(int weaponIndex)
    {
        BindWorkshop();
        if (selectedUnitIndex <= 0 || workshop == null || !workshop.IsOwned(weaponIndex)) return;
        workshop.EquipWeapon(selectedUnitIndex, weaponIndex);
        Refresh();
    }
}
