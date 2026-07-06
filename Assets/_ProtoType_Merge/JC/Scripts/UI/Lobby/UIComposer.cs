using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [JC 260628 / 일반화 260630] 씬-무관 UI 컴포저. Awake에 UILayout의 프리팹을 캔버스에 Instantiate하고
/// 각 번들의 UIModulePlacer로 레이어 분산. 씬 골격(캔버스+레이어)만 두고 UI는 전부 런타임 스폰.
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class UIComposer : MonoBehaviour
{
    [SerializeField] private UILayout layout;
    [SerializeField] private Transform backgroundLayer;
    [SerializeField] private Transform baseLayer;
    [SerializeField] private Transform overlayLayer;   // [JC 260703] 신설(HQLobby만 결선, DHScene_3는 null)
    [SerializeField] private Transform modalLayer;
    [SerializeField] private Transform tooltipLayer;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private bool done;

    private void Awake() => SpawnNow();

    public void SpawnNow()
    {
        if (done) return;
        if (layout == null) { done = true; return; }
        foreach (var prefab in layout.prefabs)
        {
            if (prefab == null) continue;
            var inst = Instantiate(prefab, transform);
            inst.name = prefab.name;
            var placer = inst.GetComponent<UIModulePlacer>();
            if (placer != null) placer.Place(backgroundLayer, baseLayer, overlayLayer, modalLayer, tooltipLayer);
            spawned.Add(inst);
        }
        done = true;
    }
}
