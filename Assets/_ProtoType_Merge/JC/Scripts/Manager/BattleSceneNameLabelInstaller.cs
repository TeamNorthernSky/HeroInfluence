using System.Collections;
using System.Reflection;
using UnityEngine;

/// <summary>
/// [JC 신설 260512] TmpBattleScene의 BattleSceneManager GO에 부착.
/// ManualSpawn으로 캐릭터 인스턴스가 등장한 후 일정 지연 뒤 모든 BattleCharactor를 찾아 BattleNameLabel을 부착·이름 설정.
/// 이름은 BattleCharactor.UnitID(=unitTemplateKey) → DHCsvTemplateCatalog 조회 → DH*Template의 UnitName/EnemyName.
/// ASB 정식 흐름 도입 시 폐기 가능.
/// </summary>
[DisallowMultipleComponent]
public class BattleSceneNameLabelInstaller : MonoBehaviour
{
    [SerializeField] private float installDelaySeconds = 0.5f;
    [SerializeField] private bool logInstall = true;

    private void Start()
    {
        StartCoroutine(InstallAfterDelay());
    }

    private IEnumerator InstallAfterDelay()
    {
        yield return new WaitForSecondsRealtime(installDelaySeconds);
        InstallAll();
    }

    private void InstallAll()
    {
        var catalog = DHCsvTemplateCatalog.Instance;
        if (catalog == null)
        {
            if (logInstall) Debug.LogWarning("[BattleSceneNameLabelInstaller] DHCsvTemplateCatalog.Instance null");
            return;
        }

        int installed = 0;
        var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var mb = all[i];
            if (mb == null) continue;
            if (mb.GetType().FullName != "BattleCharactor") continue;

            string id = ExtractUnitId(mb);
            if (string.IsNullOrWhiteSpace(id)) continue;

            string displayName = ResolveDisplayName(catalog, id);
            if (string.IsNullOrWhiteSpace(displayName)) displayName = id;

            var label = mb.gameObject.GetComponent<BattleNameLabel>();
            if (label == null) label = mb.gameObject.AddComponent<BattleNameLabel>();
            label.SetName(displayName);
            installed++;
        }

        if (logInstall) Debug.Log($"[BattleSceneNameLabelInstaller] 부착 완료: {installed}건");
    }

    private static string ExtractUnitId(MonoBehaviour battleCharactor)
    {
        var t = battleCharactor.GetType();
        var prop = t.GetProperty("UnitID", BindingFlags.Instance | BindingFlags.Public);
        if (prop != null)
        {
            var v = prop.GetValue(battleCharactor);
            if (v is string s && !string.IsNullOrWhiteSpace(s)) return s;
        }
        var field = t.GetField("UnitId", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field != null)
        {
            var v = field.GetValue(battleCharactor);
            if (v is string s && !string.IsNullOrWhiteSpace(s)) return s;
        }
        return null;
    }

    private static string ResolveDisplayName(DHCsvTemplateCatalog catalog, string id)
    {
        // [JC 260805] DH 템플릿 전환 — 레거시 행 타입(UnitData/EnemyData) 대신 DH*Template 경유.
        if (catalog.TryGetPlayerUnitTemplate(id, out DHPlayerUnitTemplate playerTemplate) && playerTemplate != null &&
            !string.IsNullOrWhiteSpace(playerTemplate.UnitName))
            return playerTemplate.UnitName;

        if (catalog.TryGetEnemyUnitTemplate(id, out DHEnemyUnitTemplate enemyTemplate) && enemyTemplate != null &&
            !string.IsNullOrWhiteSpace(enemyTemplate.EnemyName))
            return enemyTemplate.EnemyName;

        return null;
    }
}
