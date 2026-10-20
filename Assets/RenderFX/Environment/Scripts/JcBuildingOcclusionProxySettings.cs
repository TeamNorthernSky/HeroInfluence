using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>지정한 건물에만 보이지 않는 읽기 가능한 판정 메시를 추가합니다.</summary>
[ExecuteAlways, DefaultExecutionOrder(-10000)]
public sealed class JcBuildingOcclusionProxySettings : MonoBehaviour
{
    [Serializable]
    public sealed class Entry
    {
        [Tooltip("대상 건물입니다. 재생성으로 참조가 사라지면 아래 키와 위치로 같은 건물을 찾습니다.")]
        public DecorativeObjectPlacement target;
        [Tooltip("재생성된 건물을 식별하는 기존 카탈로그 키입니다. 카탈로그 자체를 수정하지 않습니다.")]
        public string prefabKey;
        [Tooltip("건물 부모 기준 위치입니다. 동일 키 중 이 위치에서 0.05 이내인 건물만 적용합니다. 씬에 저장됩니다.")]
        public Vector3 buildingLocalPosition;
        [Tooltip("건물 원점에서 판정 중심까지의 거리(월드 좌표축·월드 단위)입니다. Y를 올리면 위로 이동합니다. 씬에 저장됩니다.")]
        public Vector3 center;
        [Tooltip("판정 상자의 전체 너비(X), 높이(Y), 깊이(Z), 월드 단위입니다. 건물 스케일과 무관하며 각 축 최소 0.01. 씬에 저장됩니다.")]
        public Vector3 size = Vector3.one;
    }

    [Tooltip("적용할 건물 목록입니다. 지정된 건물에만 적용하며 공용 프리팹은 변경하지 않습니다.")]
    public Entry[] buildings = Array.Empty<Entry>();
    [Tooltip("CPU 읽기가 가능한 단위 박스 메시입니다. 빌드에서도 Read/Write를 유지합니다.")]
    public Mesh proxyMesh;
    [Tooltip("판정 렌더러용 재질입니다. forceRenderingOff로 실제 화면 출력은 항상 차단합니다.")]
    public Material proxyMaterial;

    private readonly Dictionary<DecorativeObjectPlacement, GameObject> proxies = new Dictionary<DecorativeObjectPlacement, GameObject>();
    private double nextRefresh;
    private void OnEnable() { nextRefresh = 0; }
    private void OnValidate() { nextRefresh = 0; }
    private void Update()
    {
        if (Time.realtimeSinceStartupAsDouble < nextRefresh) return;
        nextRefresh = Time.realtimeSinceStartupAsDouble + 0.2;
        RefreshProxies();
    }

    public void RefreshProxies()
    {
        if (!isActiveAndEnabled || proxyMesh == null || !proxyMesh.isReadable) return;
        var candidates = FindObjectsByType<DecorativeObjectPlacement>(FindObjectsSortMode.None);
        var wanted = new HashSet<DecorativeObjectPlacement>();
        foreach (var entry in buildings)
        {
            if (entry == null) continue;
            var target = entry.target;
            if (target == null || target.gameObject.scene != gameObject.scene)
            {
                target = null;
                foreach (var candidate in candidates)
                    if (candidate.gameObject.scene == gameObject.scene && candidate.PrefabKey == entry.prefabKey &&
                        Vector3.Distance(candidate.transform.localPosition, entry.buildingLocalPosition) < 0.05f)
                    { target = candidate; break; }
            }
            if (target == null || !wanted.Add(target)) continue;
            if (!proxies.TryGetValue(target, out var proxy) || proxy == null)
            {
                proxy = new GameObject("JC_OcclusionProxy (판정 전용)");
                proxy.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                proxy.transform.SetParent(target.transform, false);
                proxy.AddComponent<MeshFilter>().sharedMesh = proxyMesh;
                var renderer = proxy.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = proxyMaterial;
                renderer.forceRenderingOff = true;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                proxies[target] = proxy;
                target.RestoreOcclusionFade();
                target.RefreshRenderers();
                NotifyGeometryChanged();
            }
            proxy.SetActive(true);
            proxy.transform.position = target.transform.position + entry.center;
            proxy.transform.rotation = Quaternion.identity;
            proxy.transform.localScale = Vector3.one;
            var scale = proxy.transform.lossyScale;
            proxy.transform.localScale = new Vector3(
                Mathf.Max(0.01f, entry.size.x) / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                Mathf.Max(0.01f, entry.size.y) / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                Mathf.Max(0.01f, entry.size.z) / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
        }
        var removed = new List<DecorativeObjectPlacement>();
        foreach (var pair in proxies)
            if (pair.Key == null || !wanted.Contains(pair.Key))
            {
                RemoveProxy(pair.Value);
                if (pair.Key != null) { pair.Key.RestoreOcclusionFade(); pair.Key.RefreshRenderers(); }
                removed.Add(pair.Key);
            }
        foreach (var target in removed) proxies.Remove(target);
        if (removed.Count > 0) NotifyGeometryChanged();
    }

    private void NotifyGeometryChanged()
    {
        foreach (var controller in FindObjectsByType<PartyOcclusionFadeController>(FindObjectsSortMode.None))
            if (controller.gameObject.scene == gameObject.scene) controller.RefreshOcclusionMeshes();
    }

    private static void RemoveProxy(GameObject proxy)
    {
        if (proxy == null) return;
        proxy.SetActive(false);
        if (Application.isPlaying) Destroy(proxy); else DestroyImmediate(proxy);
    }

    private void OnDisable()
    {
        foreach (var pair in proxies)
        {
            RemoveProxy(pair.Value);
            if (pair.Key != null) { pair.Key.RestoreOcclusionFade(); pair.Key.RefreshRenderers(); }
        }
        proxies.Clear();
        NotifyGeometryChanged();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.8f);
        foreach (var pair in proxies)
        {
            if (pair.Value == null) continue;
            Gizmos.matrix = pair.Value.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
}
