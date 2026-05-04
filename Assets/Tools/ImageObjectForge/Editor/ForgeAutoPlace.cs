using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace Orora.ImageObjectForge
{
    internal static class ForgeAutoPlace
    {
        public class Report
        {
            public int placed;
            public int skippedNoSidecar;
            public int skippedMissingComponents;
            public int skippedInvalidSidecar;
            public List<string> warnings = new List<string>();
            public List<string> errors = new List<string>();
        }

        // ----- 폴더 진입점 -----

        public static Report Run(Canvas targetCanvas, string scanFolderAssetPath)
        {
            if (targetCanvas == null) { var r = new Report(); r.errors.Add("Target Canvas가 지정되지 않음"); return r; }
            return Run(targetCanvas.transform as RectTransform, scanFolderAssetPath, useCanvasScalerCheck: true, activateAncestors: false);
        }

        public static Report Run(RectTransform parent, string scanFolderAssetPath)
        {
            return Run(parent, scanFolderAssetPath, useCanvasScalerCheck: false, activateAncestors: true);
        }

        static Report Run(RectTransform parent, string scanFolderAssetPath, bool useCanvasScalerCheck, bool activateAncestors)
        {
            var r = new Report();
            if (string.IsNullOrEmpty(scanFolderAssetPath)) { r.errors.Add("Scan Folder가 지정되지 않음"); return r; }
            if (!AssetDatabase.IsValidFolder(scanFolderAssetPath)) { r.errors.Add($"유효한 폴더가 아님: {scanFolderAssetPath}"); return r; }

            var prefabGUIDs = AssetDatabase.FindAssets("t:Prefab", new[] { scanFolderAssetPath });
            var prefabs = new List<GameObject>(prefabGUIDs.Length);
            foreach (var guid in prefabGUIDs)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (p != null) prefabs.Add(p);
            }
            return RunInternal(parent, prefabs, useCanvasScalerCheck, activateAncestors);
        }

        // ----- Prefabs 진입점 -----

        public static Report RunForPrefabs(Canvas targetCanvas, IList<GameObject> prefabs)
        {
            if (targetCanvas == null) { var r = new Report(); r.errors.Add("Target Canvas가 지정되지 않음"); return r; }
            return RunInternal(targetCanvas.transform as RectTransform, prefabs, useCanvasScalerCheck: true, activateAncestors: false);
        }

        public static Report RunForPrefabs(RectTransform parent, IList<GameObject> prefabs)
        {
            return RunInternal(parent, prefabs, useCanvasScalerCheck: false, activateAncestors: true);
        }

        // ----- 내부 -----

        static Report RunInternal(RectTransform parent, IList<GameObject> prefabs, bool useCanvasScalerCheck, bool activateAncestors)
        {
            var r = new Report();
            if (parent == null) { r.errors.Add("Target parent가 지정되지 않음"); return r; }
            if (prefabs == null || prefabs.Count == 0) { r.errors.Add("배치할 프리팹이 없음"); return r; }

            // 비활성 조상 일시 활성화 (RectTransform Mode 전용)
            var deactivated = new List<GameObject>();
            if (activateAncestors)
            {
                var t = (Transform)parent;
                while (t != null)
                {
                    if (!t.gameObject.activeSelf)
                    {
                        deactivated.Add(t.gameObject);
                        t.gameObject.SetActive(true);
                    }
                    t = t.parent;
                }
            }

            try
            {
                var warnedSizes = new HashSet<string>();

                foreach (var prefab in prefabs)
                {
                    if (prefab == null) continue;

                    var img = prefab.GetComponentInChildren<Image>(true);
                    if (img == null || img.sprite == null) { r.skippedMissingComponents++; continue; }

                    var spritePath = AssetDatabase.GetAssetPath(img.sprite);
                    if (string.IsNullOrEmpty(spritePath)) { r.skippedMissingComponents++; continue; }

                    var sidecarPath = ForgeIO.GetSidecarPathFor(spritePath);
                    var meta = ForgeIO.LoadCropSidecar(sidecarPath);
                    if (meta == null) { r.skippedNoSidecar++; continue; }

                    int canvasW = meta.canvasSize.width;
                    int canvasH = meta.canvasSize.height;
                    if (canvasW <= 0 || canvasH <= 0
                        || meta.cropBounds.width <= 0 || meta.cropBounds.height <= 0)
                    {
                        r.skippedInvalidSidecar++;
                        r.warnings.Add($"{prefab.name}: canvasSize 또는 cropBounds 무효");
                        continue;
                    }

                    // 크기 비교 — Canvas Mode는 CanvasScaler.referenceResolution, 그 외는 parent.rect.size
                    int compW, compH;
                    string compLabel;
                    if (useCanvasScalerCheck)
                    {
                        var canvas = parent.GetComponent<Canvas>();
                        var scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
                        if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                        {
                            compW = Mathf.RoundToInt(scaler.referenceResolution.x);
                            compH = Mathf.RoundToInt(scaler.referenceResolution.y);
                            compLabel = "CanvasScaler RefResolution";
                        }
                        else
                        {
                            compW = Mathf.RoundToInt(parent.rect.width);
                            compH = Mathf.RoundToInt(parent.rect.height);
                            compLabel = "Canvas rect";
                        }
                    }
                    else
                    {
                        compW = Mathf.RoundToInt(parent.rect.width);
                        compH = Mathf.RoundToInt(parent.rect.height);
                        compLabel = "parent rect";
                    }

                    if (compW != canvasW || compH != canvasH)
                    {
                        var key = $"{canvasW}x{canvasH}->{compW}x{compH}";
                        if (warnedSizes.Add(key))
                        {
                            r.warnings.Add($"{compLabel} ({compW}x{compH}) != 사이드카 canvasSize ({canvasW}x{canvasH}). 환산 없이 사이드카 픽셀 그대로 적용 (위치 어긋날 수 있음).");
                        }
                    }

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    if (instance == null) { r.warnings.Add($"{prefab.name}: Instantiate 실패"); continue; }
                    Undo.RegisterCreatedObjectUndo(instance, "Auto-Place Prefab");

                    var rt = instance.GetComponent<RectTransform>();
                    if (rt == null)
                    {
                        r.warnings.Add($"{prefab.name}: RectTransform 없음, 위치 설정 스킵");
                        r.placed++;
                        continue;
                    }

                    if (rt.localScale != Vector3.one)
                    {
                        r.warnings.Add($"{prefab.name}: localScale = ({rt.localScale.x:0.###}, {rt.localScale.y:0.###}, {rt.localScale.z:0.###}) (1,1,1 아님)");
                    }

                    // 좌상단 표준 강제
                    ForgePrefabFactory.ApplyTopLeftAnchor(rt);

                    // 사이드카 좌상단(이미지 y-up → UI y-down 변환):
                    //   topLeftX = cropBounds.x
                    //   topLeftYFromImageTop = canvasH - (cropBounds.y + cropBounds.height)
                    float topLeftX = meta.cropBounds.x;
                    float topLeftYFromTop = canvasH - (meta.cropBounds.y + meta.cropBounds.height);
                    rt.anchoredPosition = new Vector2(topLeftX, -topLeftYFromTop);
                    rt.sizeDelta = new Vector2(meta.cropBounds.width, meta.cropBounds.height);

                    r.placed++;
                }

                EditorUtility.SetDirty(parent);
            }
            finally
            {
                // 활성화한 조상들을 역순으로 원상 복구
                for (int i = deactivated.Count - 1; i >= 0; i--)
                {
                    if (deactivated[i] != null) deactivated[i].SetActive(false);
                }
            }

            return r;
        }
    }
}
