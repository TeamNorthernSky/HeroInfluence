using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace KJ_Work.Integration
{
    public static class KJ_FOWBootstrap
    {
        private const string PrefabPath = "Assets/KJ_Work/Prefabs/[KJ_FOWManager].prefab";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Execute()
        {
            if (KJ_FOWManager.Instance != null) return;

            GameObject prefab = LoadPrefab();
            if (prefab != null)
            {
                var go = Object.Instantiate(prefab);
                go.name = "[KJ_FOWManager]";
                Object.DontDestroyOnLoad(go);
                Debug.Log("[KJ_FOWBootstrap] KJ_FOWManager가 자동 생성되었습니다.");
            }
            else
            {
                if (Application.isEditor)
                {
                    Debug.LogWarning($"[KJ_FOWBootstrap] '{PrefabPath}' 프리팹을 찾을 수 없습니다. 프리팹 생성이 필요합니다.");
                }
            }
        }

        private static GameObject LoadPrefab()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
#else
            return null;
#endif
        }
    }
}
