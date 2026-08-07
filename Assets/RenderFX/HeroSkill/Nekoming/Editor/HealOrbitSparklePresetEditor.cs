using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    /// <summary>
    /// 궤도 반짝임 프리셋(10번) 에디터 — 변종 인식(B/A 쌍) + 따름 잠금 UI.
    /// 적용/캡처 대상 = 제 변종 프리팹의 OrbSparkles PS.
    /// 색 베이크는 PS startColor 로 — 재질은 투사체와 공유라 만지지 않는다(라이브 색은 MPB 가 정본).
    /// </summary>
    [CustomEditor(typeof(HealOrbitSparklePreset))]
    public class HealOrbitSparklePresetEditor : Editor
    {
        const string DIR = "Assets/RenderFX/HeroSkill/Nekoming/Heal";
        const string LFL = "Assets/RenderFX/HeroSkill/Nekoming/LetsFightingLove";
        const string TAO = "Assets/RenderFX/HeroSkill/Nekoming/Taosenaiyo";
        string Sfx => JcPresetEditorUtil.VariantSuffix(target) ?? "_Alter";
        // ★소속 분기 — L10(LFL)/T7(Tao) 프리셋이면 제 스킬 프리팹만 만진다(힐과 완전 절연, 260807).
        bool IsTao => target != null &&
            UnityEditor.AssetDatabase.GetAssetPath(target).Replace('\\', '/').Contains("/Taosenaiyo/");
        string OrbitPrefab => IsTao ? TAO + "/Prefabs/Tao_Revive" + Sfx + ".prefab"
            : JcPresetEditorUtil.IsLfl(target) ? LFL + "/Prefabs/LFL_LandAura" + Sfx + ".prefab"
            : DIR + "/Prefabs/HealOrbit" + Sfx + ".prefab";
        // Tao 는 자식 이름이 다르다(OrbSparkles ↛ Sparkles).
        string SparkleChild => IsTao ? "Sparkles" : "OrbSparkles";

        static readonly string[] TransformProps = { "rate", "sizeMin", "sizeMax", "lifetime", "shapeRadius" };

        public override void OnInspectorGUI()
        {
            JcPresetEditorUtil.DrawWithFollowLock(serializedObject, "JC.OrbSparkle.Fold", TransformProps);
            var p = (HealOrbitSparklePreset)target;
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("▶ 프리팹에 적용", GUILayout.Height(30))) Apply(p);
                if (GUILayout.Button("● 현재값 캡처", GUILayout.Height(30))) Capture(p);
                JcPresetEditorUtil.DrawSaveButton(target);
            }
            EditorGUILayout.HelpBox("적용: 이 값을 HealOrbit" + Sfx + " 프리팹의 OrbSparkles(PS)에 반영.\n" +
                "색 베이크는 PS startColor — 재질(투사체와 공유)은 만지지 않습니다. 라이브 색은 MPB 로 즉시 반영.", MessageType.Info);
        }

        void Apply(HealOrbitSparklePreset p)
        {
            var root = PrefabUtility.LoadPrefabContents(OrbitPrefab);
            var t = root.transform.Find(SparkleChild);
            var ps = t ? t.GetComponent<ParticleSystem>() : null;
            if (ps == null) { PrefabUtility.UnloadPrefabContents(root); Debug.LogError("[HealOrbitSparklePreset] " + SparkleChild + " 없음"); return; }
            var m = ps.main;
            m.startSize = new ParticleSystem.MinMaxCurve(p.sizeMin, p.sizeMax);
            m.startLifetime = p.lifetime;
            m.startColor = p.color;
            var e = ps.emission; e.rateOverTime = p.rate;
            var sh = ps.shape; sh.radius = p.shapeRadius;
            PrefabUtility.SaveAsPrefabAsset(root, OrbitPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log($"[HealOrbitSparklePreset] 프리팹에 적용 완료 (HealOrbit{Sfx})");
        }

        void Capture(HealOrbitSparklePreset p)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(OrbitPrefab);
            var t = root != null ? root.transform.Find(SparkleChild) : null;
            var ps = t ? t.GetComponent<ParticleSystem>() : null;
            if (ps == null) { Debug.LogError("[HealOrbitSparklePreset] " + SparkleChild + " 없음"); return; }
            Undo.RecordObject(p, "Capture Orbit Sparkle");
            p.sizeMin = ps.main.startSize.constantMin;
            p.sizeMax = ps.main.startSize.constantMax;
            p.lifetime = ps.main.startLifetime.constant;
            p.color = ps.main.startColor.color;
            p.rate = ps.emission.rateOverTime.constant;
            p.shapeRadius = ps.shape.radius;
            EditorUtility.SetDirty(p);
            Debug.Log($"[HealOrbitSparklePreset] 현재값 캡처 완료 (HealOrbit{Sfx})");
        }
    }
}
