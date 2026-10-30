using System;
using System.Collections.Generic;
using UnityEngine;

namespace JC.EnvironmentDetail
{
    // 튜토리얼은 월드 드레싱의 일정한 점선을 사용한다. 표시 복사본에서만 기존 도색을 제거한다.
    public sealed class JcTutorialRoadSurface : MonoBehaviour
    {
        private readonly List<Material> owned = new List<Material>();

        public static void Apply(GameObject root)
        {
            if (root == null || root.GetComponent<JcTutorialRoadSurface>() != null) return;
            var profile = JC.BattleTesting.JcBattleSurfaceDressing.Profile;
            if (profile == null || profile.plainAsphalt == null)
                throw new InvalidOperationException("튜토리얼 중앙선 중복 제거용 무표시 도로 텍스처가 없습니다.");
            var owner = root.AddComponent<JcTutorialRoadSurface>();
            var replacements = new Dictionary<Material, Material>();
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    var texture = source != null && source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : null;
                    if (texture == null || !texture.name.StartsWith("Road_Centerline_", StringComparison.Ordinal)) continue;
                    if (!replacements.TryGetValue(source, out var replacement))
                    {
                        replacement = new Material(source) { name = source.name + "_TutorialPlainRoad", hideFlags = HideFlags.DontSave };
                        replacement.SetTexture("_BaseMap", profile.plainAsphalt);
                        if (replacement.HasProperty("_MainTex")) replacement.SetTexture("_MainTex", profile.plainAsphalt);
                        replacements.Add(source, replacement);
                        owner.owned.Add(replacement);
                    }
                    materials[i] = replacement;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        private void OnDestroy()
        {
            foreach (var material in owned)
                if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            owned.Clear();
        }
    }
}
