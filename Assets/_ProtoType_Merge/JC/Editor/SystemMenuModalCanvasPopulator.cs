using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SystemMenuModalCanvasPopulator
{
    private const string PrefabPath = "Assets/_ProtoType_Merge/JC/Prefabs/UI/SystemMenuModal.prefab";
    private const int TargetSortOrder = 50;

    [MenuItem("Tools/SystemMenu/Add Canvas to SystemMenuModal Prefab")]
    public static void Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[Populator] prefab 없음: {PrefabPath}");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            bool changed = false;

            var canvas = root.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = TargetSortOrder;
                changed = true;
                Debug.Log($"[Populator] Canvas 추가 (Overlay, sortOrder={TargetSortOrder})");
            }
            else
            {
                if (canvas.sortingOrder != TargetSortOrder)
                {
                    canvas.sortingOrder = TargetSortOrder;
                    changed = true;
                    Debug.Log($"[Populator] Canvas sortOrder → {TargetSortOrder}");
                }
                else
                {
                    Debug.Log("[Populator] Canvas 이미 있음 (sortOrder 일치)");
                }
            }

            if (root.GetComponent<CanvasScaler>() == null)
            {
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                changed = true;
                Debug.Log("[Populator] CanvasScaler 추가 (1920x1080, match=0.5)");
            }

            if (root.GetComponent<GraphicRaycaster>() == null)
            {
                root.AddComponent<GraphicRaycaster>();
                changed = true;
                Debug.Log("[Populator] GraphicRaycaster 추가");
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[Populator] prefab 저장 완료");
            }
            else
            {
                Debug.Log("[Populator] 변경사항 없음");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
