using TMPro;
using UnityEngine;
using UnityEngine.UI;

// [JC 신설 260513] 임시 캐릭터 세부 정보 모달. 900×700, 텍스트 출력 + 닫기.
// 로비씬 양식 확정 시 정식 구현으로 교체 예정.
[DisallowMultipleComponent]
[RequireComponent(typeof(Modal))]
public class CharacterDetailModal : MonoBehaviour
{
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Button closeButton;

    private const float InfluencePowerMax = 200f;

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
    }

    public void Open(int unitIndex, bool isEnemy)
    {
        if (bodyText != null) bodyText.text = ComposeBody(unitIndex, isEnemy);
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    private static string ComposeBody(int unitIndex, bool isEnemy)
    {
        if (unitIndex <= 0) return "-";

        if (isEnemy)
        {
            PersistentEnemyRepository enemyRepo = PersistentEnemyRepository.Instance;
            if (enemyRepo == null || !enemyRepo.TryGetUnit(unitIndex, out EnemyUnitPersistentData edata)) return "-";

            DHCsvTemplateCatalog catalog = DHCsvTemplateCatalog.Instance;
            EnemyData template = null;
            catalog?.TryGetEnemyTemplate(edata.UnitTemplateKey, out template);

            string ename = template != null && !string.IsNullOrWhiteSpace(template.Name) ? template.Name : edata.UnitTemplateKey;
            string ecls = template != null ? template.UnitType : "-";

            float emaxHp = Mathf.Max(0f, edata.IngameStats.HP);
            float ecurHp = Mathf.Clamp(edata.CurrentHp, 0f, emaxHp);
            float ecurIp = Mathf.Clamp(edata.IngameStats.Influence, 0f, InfluencePowerMax);

            return $"이름: {ename}\n클래스: {ecls}\n레벨: {edata.Level}\nHP: {Mathf.RoundToInt(ecurHp)}/{Mathf.RoundToInt(emaxHp)}\nIP: {Mathf.RoundToInt(ecurIp)}/{Mathf.RoundToInt(InfluencePowerMax)}";
        }

        PersistentUnitRepository repo = PersistentUnitRepository.Instance;
        if (repo == null || !repo.TryGetUnit(unitIndex, out UnitPersistentData data)) return "-";

        DHCsvTemplateCatalog catalog2 = DHCsvTemplateCatalog.Instance;
        UnitData template2 = null;
        catalog2?.TryGetPlayerTemplate(data.UnitTemplateKey, out template2);

        string name = template2 != null && !string.IsNullOrWhiteSpace(template2.Name) ? template2.Name : data.UnitTemplateKey;
        string cls = template2 != null ? template2.UnitType : "-";

        float maxHp = Mathf.Max(0f, data.IngameStats.HP);
        float curHp = Mathf.Clamp(data.CurrentHp, 0f, maxHp);
        float curIp = Mathf.Clamp(data.IngameStats.Influence, 0f, InfluencePowerMax);

        return $"이름: {name}\n클래스: {cls}\n레벨: {data.Level}\nHP: {Mathf.RoundToInt(curHp)}/{Mathf.RoundToInt(maxHp)}\nIP: {Mathf.RoundToInt(curIp)}/{Mathf.RoundToInt(InfluencePowerMax)}";
    }
}
