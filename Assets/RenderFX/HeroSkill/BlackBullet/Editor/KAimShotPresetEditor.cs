using UnityEngine;
using UnityEditor;
using System.Linq;

namespace JC.VFX
{
    /// <summary>
    /// K_AimShot 마스터 프리셋 에디터 — 저스티스·플레어 계열과 동일한 표준 패턴.
    /// 라이브 프리뷰 토글 / 「▶ 프리팹에 적용」 / 「● 현재값 캡처」.
    ///
    /// 설계 주의(저스티스 오배선 사고의 교훈):
    ///  - 대상 프리팹 경로를 코드에 상수로 박지 않는다. 프리셋의 targetPrefab을 쓴다.
    ///  - 라이브 푸시는 씬의 모든 이펙트에 무차별 반영하지 않고, UsesPreset()으로
    ///    이 프리셋을 실제 참조하는 인스턴스에만 적용한다.
    ///
    /// 이 에디터는 루미나 계열 베이스(FlareOrbPresetEditorBase)를 상속하지 않는다 —
    /// 타 PC가 그 파일을 병행 수정하므로 결합을 만들지 않기 위해 독립 구현한다.
    /// </summary>
    [CustomEditor(typeof(KAimShotPreset))]
    public class KAimShotPresetEditor : Editor
    {
        private const string LiveKey = "JC.VFX.KAimShot.LivePreview";

        private const string HelpText =
            "런타임 권위는 이 프리셋입니다 — KAimShotVfx가 스폰될 때(Awake) 프리셋을 다시 읽습니다.\n" +
            "따라서 플레이 중 값을 수정하면 다음 발사에 버튼 조작 없이 반영됩니다.\n" +
            "· 라이브 프리뷰: 씬에 상주하는 인스턴스에 즉시 반영(비파괴, 저장 안 됨)\n" +
            "· ▶ 프리팹에 적용: 프리셋 값을 프리팹에 굳힘(에디터 표시·프리셋 없을 때의 폴백용)\n" +
            "· ● 현재값 캡처: 대상 프리팹 → 프리셋으로 역방향 읽기\n" +
            "대상 프리팹은 위 '적용 대상' 슬롯으로 지정합니다.";

        public override void OnInspectorGUI()
        {
            bool live = EditorPrefs.GetBool(LiveKey, true);
            bool newLive = EditorGUILayout.ToggleLeft(new GUIContent("라이브 프리뷰 (씬에 상주하는 인스턴스에 즉시 반영, 비파괴)","이 프리셋을 사용하는 살아 있는 사격 이펙트에 조절값을 반영합니다. 디스크 저장과 별개입니다."), live);
            if (newLive != live) EditorPrefs.SetBool(LiveKey, newLive);

            EditorGUILayout.Space(2);

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            bool changed = EditorGUI.EndChangeCheck();
            // 다음 발사 반영은 KAimShotVfx.Awake의 PullFromPreset이 담당하므로
            // 프리팹에 굳히는 조작(자동 적용)은 필요하지 않다. 라이브 푸시는 씬 상주 인스턴스용.
            if (changed && newLive) LivePush();

            DrawTrailLifetimeInfo();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("▶ 프리팹에 적용","현재 스킬의 연결된 총구·비행·착탄 부품에 값을 확정합니다."), GUILayout.Height(30))) ApplyPreset(false);
                if (GUILayout.Button(new GUIContent("● 현재값 캡처","이 프리셋을 참조하는 실제 부품의 저장값을 가져옵니다. 디스크 저장은 별도입니다."), GUILayout.Height(30))) CapturePreset();
            }
            EditorGUILayout.HelpBox(HelpText, MessageType.Info);
        }

        private KAimShotPreset Preset => (KAimShotPreset)target;

        /// <summary>
        /// 궤적 수명은 런타임에 의미 있는 창으로 제한된다(무의미 설정 차단).
        /// 인스펙터 입력값과 실제 동작이 달라질 수 있으므로 실효값을 드러낸다.
        /// </summary>
        private void DrawTrailLifetimeInfo()
        {
            var p = Preset;
            if (p == null || p.trail == null) return;

            float sd = p.trail.startDelay;
            float min = Mathf.Max(0.01f, (p.launchDelay + p.travelTime) - sd);
            float max = Mathf.Max(min, (p.launchDelay + p.travelTime + p.tailReachSeconds) - sd);
            float eff = Mathf.Clamp(p.trail.lifetime, min, max);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"궤적 실효 수명 {eff:F2}s   (허용 창 {min:F2} ~ {max:F2}s)");

            if (!Mathf.Approximately(eff, p.trail.lifetime))
                sb.AppendLine($"⚠ 입력값 {p.trail.lifetime:F2}s 는 창을 벗어나 {eff:F2}s 로 제한됩니다.");

            if (p.trail.fadeOut <= 1e-4f)
                sb.Append("페이드아웃 0 → 알파는 유지되고 꼬리부터 감겨 사라집니다.");
            else
            {
                float fadeStart = eff * (1f - p.trail.fadeOut);
                sb.Append($"페이드아웃 시작 {fadeStart:F2}s · ");
                sb.Append(Mathf.Approximately(eff, max)
                    ? "실효 수명이 상한 → 꼬리부터 감기는 소멸이 지배적입니다."
                    : "알파가 먼저 0에 닿아 전체가 흐려지며 사라집니다.");
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.HelpBox(sb.ToString(), MessageType.None);
        }

        /// <summary>씬 인스턴스에 즉시 반영. 이 프리셋을 참조하는 것만 스코프에 넣는다.</summary>
        private void LivePush()
        {
            var p = Preset;
            foreach (var vfx in Object.FindObjectsByType<KAimShotVfx>(FindObjectsSortMode.None))
            {
                if (!vfx.UsesPreset(p)) continue;
                CopyPresetToComponent(p, vfx);
            }
        }

        /// <param name="silent">자동 적용 경로. 대화상자를 띄우지 않고 조용히 처리한다.</param>
        private void ApplyPreset(bool silent)
        {
            var p = Preset;
            if (p.targetParts != null && p.targetParts.Length > 0)
            {
                foreach (var part in p.targetParts)
                {
                    if (part == null) continue;
                    string partPath = AssetDatabase.GetAssetPath(part);
                    var contents = PrefabUtility.LoadPrefabContents(partPath);
                    try
                    {
                        var shot = contents.GetComponentInChildren<KAimShotVfx>(true);
                        if (shot == null || !shot.UsesPreset(p)) { Debug.LogError("[KAimShotPreset] 실제 프리셋 참조가 다른 부품: " + partPath); continue; }
                        CopyPresetToComponent(p, shot);
                        PrefabUtility.SaveAsPrefabAsset(contents, partPath);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(contents); }
                }
                return;
            }
            if (p.targetPrefab == null)
            {
                if (!silent) EditorUtility.DisplayDialog("적용 대상 없음",
                    "프리셋의 '적용 대상 → targetPrefab' 슬롯이 비어 있습니다.", "확인");
                return;
            }

            string path = AssetDatabase.GetAssetPath(p.targetPrefab);
            if (string.IsNullOrEmpty(path))
            {
                if (!silent) EditorUtility.DisplayDialog("경로 확인 불가", "targetPrefab이 프로젝트 에셋이 아닙니다.", "확인");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var vfx = root.GetComponentInChildren<KAimShotVfx>(true);
                if (vfx == null)
                {
                    if (!silent) EditorUtility.DisplayDialog("대상 없음", "대상 프리팹에 KAimShotVfx가 없습니다.", "확인");
                    return;
                }
                // ★이 프리셋을 실제로 참조하는지 확인 — 다른 스킬 프리팹에 기록되는 사고 차단
                if (!vfx.UsesPreset(p))
                {
                    if (silent) return;   // 자동 적용은 불일치 시 조용히 건너뛴다
                    if (!EditorUtility.DisplayDialog("참조 불일치",
                        "대상 프리팹의 KAimShotVfx가 이 프리셋을 참조하지 않습니다.\n그래도 적용할까요?",
                        "적용", "취소")) return;
                }

                CopyPresetToComponent(p, vfx);
                EditorUtility.SetDirty(vfx);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                if (!silent) Debug.Log($"[KAimShotPresetEditor] 적용 완료 → {path}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private void CapturePreset()
        {
            var p = Preset;
            var candidates=(p.targetParts??new GameObject[0]).Concat(new[]{p.targetPrefab}).Where(g=>g!=null);
            var vfx=candidates.SelectMany(g=>g.GetComponentsInChildren<KAimShotVfx>(true)).FirstOrDefault(v=>v.UsesPreset(p));
            if(vfx==null){Debug.LogError("[KAimShotPreset] 이 프리셋을 참조하는 캡처 대상이 없습니다.",p);return;}

            Undo.RecordObject(p, "Capture K_AimShot Preset");
            CopyComponentToPreset(vfx, p);
            EditorUtility.SetDirty(p);
            Debug.Log("[KAimShotPresetEditor] 캡처 완료 (프리팹 → 프리셋)");
        }

        // ── 값 이동 (KElementLife는 참조 공유를 피해 깊은 복사) ──

        private static void CopyPresetToComponent(KAimShotPreset p, KAimShotVfx v)
        {
            v.launchDelay = p.launchDelay;
            v.travelTime = p.travelTime;
            if (v.muzzleLife == null) v.muzzleLife = new KElementLife();
            v.muzzleLife.CopyFrom(p.muzzleFlash);
            if (v.bulletHead == null) v.bulletHead = new KElementLife();
            v.bulletHead.CopyFrom(p.bulletHead);
            if (v.trail == null) v.trail = new KElementLife();
            v.trail.CopyFrom(p.trail);
            v.tailReachSeconds = p.tailReachSeconds;
            v.trailTailWidthRatio = p.trailTailWidthRatio;
            v.trailTailBrightnessRatio = p.trailTailBrightnessRatio;
            v.trailTaperCurve = p.trailTaperCurve;
            if (v.impactBurst == null) v.impactBurst = new KElementLife();
            v.impactBurst.CopyFrom(p.impactBurst);
            if (v.muzzleColors == null) v.muzzleColors = new KColorSet();
            v.muzzleColors.CopyFrom(p.muzzleColors);
            if (v.headColors == null) v.headColors = new KColorSet();
            v.headColors.CopyFrom(p.headColors);
            if (v.trailColors == null) v.trailColors = new KColorSet();
            v.trailColors.CopyFrom(p.trailColors);
            if (v.impactColors == null) v.impactColors = new KColorSet();
            v.impactColors.CopyFrom(p.impactColors);
            v.casterFallbackOffset = p.casterFallbackOffset;
        }

        private static void CopyComponentToPreset(KAimShotVfx v, KAimShotPreset p)
        {
            p.launchDelay = v.launchDelay;
            p.travelTime = v.travelTime;
            if (p.muzzleFlash == null) p.muzzleFlash = new KElementLife();
            p.muzzleFlash.CopyFrom(v.muzzleLife);
            if (p.bulletHead == null) p.bulletHead = new KElementLife();
            p.bulletHead.CopyFrom(v.bulletHead);
            if (p.trail == null) p.trail = new KElementLife();
            p.trail.CopyFrom(v.trail);
            p.tailReachSeconds = v.tailReachSeconds;
            p.trailTailWidthRatio = v.trailTailWidthRatio;
            p.trailTailBrightnessRatio = v.trailTailBrightnessRatio;
            p.trailTaperCurve = v.trailTaperCurve;
            if (p.impactBurst == null) p.impactBurst = new KElementLife();
            p.impactBurst.CopyFrom(v.impactBurst);
            if (p.muzzleColors == null) p.muzzleColors = new KColorSet();
            p.muzzleColors.CopyFrom(v.muzzleColors);
            if (p.headColors == null) p.headColors = new KColorSet();
            p.headColors.CopyFrom(v.headColors);
            if (p.trailColors == null) p.trailColors = new KColorSet();
            p.trailColors.CopyFrom(v.trailColors);
            if (p.impactColors == null) p.impactColors = new KColorSet();
            p.impactColors.CopyFrom(v.impactColors);
            p.casterFallbackOffset = v.casterFallbackOffset;
        }
    }
}
