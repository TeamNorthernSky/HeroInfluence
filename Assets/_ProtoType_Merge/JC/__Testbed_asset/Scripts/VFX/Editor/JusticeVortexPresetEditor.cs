using UnityEditor;
using UnityEngine;

namespace JC.VFX.EditorTools
{
    /// <summary>
    /// 용권풍 프리셋 인스펙터 — 「적용」이 컨테이너 프리팹에 확정 기록한다.
    /// 런타임은 바인더가 스폰마다 프리셋을 다시 읽으므로, 플레이 중 튜닝은 다음 재생에 반영된다.
    /// </summary>
    [CustomEditor(typeof(JusticeVortexPreset))]
    public class JusticeVortexPresetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var p = (JusticeVortexPreset)target;

            EditorGUILayout.Space(4);

            float life = Life(p);
            float revPerSec = p.turns / Mathf.Max(life, 0.01f);
            string msg =
                "전체 수명 = 발사 지연 + 거리 ÷ 속도 = " + life.ToString("F2") + "초\n" +
                "회전 속도 = " + revPerSec.ToString("F2") + " 바퀴/초 (" + (revPerSec * 360f).ToString("F0") + "°/초)\n" +
                "자식의 스윕은 이 수명으로 덮어써지고, 비행이 끝나는 순간 스스로 방출을 끊는다.\n" +
                "반경·두께·개수·색·투명도는 자식 프리셋에서 조절한다 — 여기는 시간축과 경로만 담당한다.";

            // 서브스텝 한도(프레임당 2.4m) 기준의 회전수 상한 — 먼저 걸리는 층이 한도를 정한다.
            float radius = LimitingRadius(p, out string layer);
            float turnLimit = radius > 0f ? 2.4f * 60f * life / (2f * Mathf.PI * radius) : 0f;
            if (turnLimit > 0f)
                msg += "\n회전수 한도(서브스텝 기준) ≈ " + turnLimit.ToString("F1") +
                       " 바퀴 — 현재 " + p.turns.ToString("F1") + " · 한도를 정하는 층: " + layer;

            msg += p.preserveStrokeArc
                ? "\n획 각도 보존: 켜짐 — 거리·회전수를 바꿔도 획 하나가 덮는 각도가 유지된다."
                : "\n★획 각도 보존이 꺼져 있다 — 거리를 줄이면 획이 여러 바퀴를 덮어 링이 굵어 보인다.";

            EditorGUILayout.HelpBox(msg,
                turnLimit > 0f && p.turns > turnLimit ? MessageType.Warning : MessageType.Info);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("적용 — 프리팹에 확정 기록", GUILayout.Height(26)))
                Apply(p);
        }

        private static float Life(JusticeVortexPreset p)
            => p.launchDelay + p.distance / Mathf.Max(p.speed, 0.1f);

        /// <summary>
        /// 회전수 한도를 정하는 유효 반경 — 두 층 중 「큰 쪽」이 먼저 서브스텝 상한에 걸린다.
        /// ★포인트는 반경이 아니라 「반경 + 바깥 오프셋」으로 서브스텝을 잡으므로 대개 이쪽이 한도를 정한다.
        /// </summary>
        private static float LimitingRadius(JusticeVortexPreset p, out string layer)
        {
            layer = "";
            if (p.targets == null) return 0f;
            float r = 0f;

            var s = p.targets.strokePrefab != null
                ? p.targets.strokePrefab.GetComponent<JC.VFX.Seam.JcArcStrokeEffect>() : null;
            if (p.useStroke && s != null) { r = s.Radius; layer = "호 획"; }

            var pt = p.targets.pointPrefab != null
                ? p.targets.pointPrefab.GetComponent<JC.VFX.Seam.JcArcPointEffect>() : null;
            if (p.usePoint && pt != null)
            {
                float rp = pt.Radius + Mathf.Max(pt.OffsetMin, pt.OffsetMax);
                if (rp > r) { r = rp; layer = "포인트 획"; }
            }
            return r;
        }

        private static void Apply(JusticeVortexPreset p)
        {
            var t = p.targets;
            string prefabPath = t != null && t.vortexPrefab != null ? AssetDatabase.GetAssetPath(t.vortexPrefab) : null;
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogWarning("[JusticeVortex] " + p.name + ": 대상 프리팹이 비어 있어 건너뜁니다.", p);
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                JusticeVortexRuntime.ApplyVortex(root, p);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            AssetDatabase.SaveAssets();
            Debug.Log("[JusticeVortex] " + p.name + " → 프리팹에 확정 기록 완료", p);
        }
    }
}
