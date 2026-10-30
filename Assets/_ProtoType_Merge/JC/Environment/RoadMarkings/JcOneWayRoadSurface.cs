using System;
using System.Collections.Generic;
using UnityEngine;

namespace JC.EnvironmentDetail
{
    // A2Sector1: 중앙 인도로 분리된 x=15,17 / z=14~17 도로는 각각 일방통행입니다.
    // 레벨/이동 데이터를 변경하지 않고 표시 복사본에서 중앙선 도색만 제외합니다.
    public sealed class JcOneWayRoadSurface : MonoBehaviour
    {
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        public int RemovedTriangles { get; private set; }

        public static bool Contains(Vector3 explorationPosition) =>
            (explorationPosition.z >= 13.49f && explorationPosition.z <= 17.51f &&
             (Mathf.Abs(explorationPosition.x - 15) <= .51f || Mathf.Abs(explorationPosition.x - 17) <= .51f)) ||
            // A2Sector1 편의점(14,23) 오른쪽 주차장 진입로. 횡단보도/주차 구획은 유지합니다.
            (Mathf.Abs(explorationPosition.x - 16) <= .51f &&
             explorationPosition.z >= 21.49f && explorationPosition.z <= 25.51f);

        public static void Apply(GameObject root, Matrix4x4 worldToExploration)
        {
            if (root.GetComponent<JcOneWayRoadSurface>() != null) return;
            var profile = JC.BattleTesting.JcBattleSurfaceDressing.Profile;
            if (profile == null || profile.plainAsphalt == null) return;
            var owner = root.AddComponent<JcOneWayRoadSurface>();
            var plain = new Dictionary<Material, Material>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = JC.BattleTesting.JcBattleSurfaceDressing.ReadableRoadMesh(filter.sharedMesh);
                var renderer = filter.GetComponent<MeshRenderer>();
                if (mesh == null || !mesh.isReadable || renderer == null) continue;
                var materials = new List<Material>(renderer.sharedMaterials);
                var matrix = worldToExploration * filter.transform.localToWorldMatrix;
                var vertices = mesh.vertices;
                Mesh copy = null;
                int originalSubCount = mesh.subMeshCount;
                for (int sub = 0; sub < Math.Min(originalSubCount, materials.Count); sub++)
                {
                    var material = materials[sub];
                    var texture = material != null && material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                    if (texture == null || !texture.name.StartsWith("Road_Centerline_", StringComparison.Ordinal)) continue;
                    var keep = new List<int>();
                    var remove = new List<int>();
                    var triangles = mesh.GetTriangles(sub);
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        var center = (vertices[triangles[i]] + vertices[triangles[i + 1]] + vertices[triangles[i + 2]]) / 3;
                        var destination = Contains(matrix.MultiplyPoint3x4(center)) ? remove : keep;
                        destination.Add(triangles[i]); destination.Add(triangles[i + 1]); destination.Add(triangles[i + 2]);
                    }
                    if (remove.Count == 0) continue;
                    owner.RemovedTriangles += remove.Count / 3;
                    if (!plain.TryGetValue(material, out var replacement))
                    {
                        replacement = new Material(material) { name = material.name + "_OneWayPlain", hideFlags = HideFlags.DontSave };
                        replacement.SetTexture("_BaseMap", profile.plainAsphalt);
                        if (replacement.HasProperty("_MainTex")) replacement.SetTexture("_MainTex", profile.plainAsphalt);
                        plain.Add(material, replacement); owner.owned.Add(replacement);
                    }
                    if (keep.Count == 0) { materials[sub] = replacement; continue; }
                    if (copy == null)
                    {
                        copy = Instantiate(mesh); copy.name = mesh.name + "_OneWaySplit";
                        copy.hideFlags = HideFlags.DontSave; owner.owned.Add(copy);
                    }
                    int extra = copy.subMeshCount;
                    copy.subMeshCount = extra + 1;
                    copy.SetTriangles(keep, sub); copy.SetTriangles(remove, extra);
                    materials.Add(replacement);
                }
                if (copy != null) filter.sharedMesh = copy;
                renderer.sharedMaterials = materials.ToArray();
            }
        }

        void OnDestroy()
        {
            foreach (var item in owned) if (item != null)
            { if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
        }
    }
}
