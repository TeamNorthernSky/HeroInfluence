using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [JC 260628] HQLobbyScene UI 컴포저. Awake에 LobbyUILayout의 프리팹을 캔버스에 Instantiate하고
/// 각 번들의 LobbyUIModulePlacer로 레이어 분산. 씬 골격(캔버스+레이어)만 두고 UI는 전부 런타임 스폰.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class HQLobbyUIComposer : MonoBehaviour
{
    [SerializeField] private LobbyUILayout layout;
    [SerializeField] private Transform backgroundLayer;
    [SerializeField] private Transform baseLayer;
    [SerializeField] private Transform modalLayer;
    [SerializeField] private Transform tooltipLayer;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private bool done;

    private void Awake() => SpawnNow();

    /// <summary>에디트모드 검증/런타임 공용. 이미 스폰했으면 무동작(중복 가드).</summary>
    public void SpawnNow()
    {
        if (done) return;
        if (layout == null) { done = true; return; }
        foreach (var prefab in layout.prefabs)
        {
            if (prefab == null) continue;
            var inst = Instantiate(prefab, transform); // 캔버스 자식
            inst.name = prefab.name;                   // (Clone) 접미 제거 — 검증/탐색 일관
            var placer = inst.GetComponent<LobbyUIModulePlacer>();
            if (placer != null) placer.Place(backgroundLayer, baseLayer, modalLayer, tooltipLayer);
            spawned.Add(inst);
        }
        done = true;
    }
}
