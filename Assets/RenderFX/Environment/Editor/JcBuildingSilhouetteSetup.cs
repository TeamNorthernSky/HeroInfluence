using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class JcBuildingSilhouetteSetup
{
    public const string MaterialPath = "Assets/RenderFX/Environment/Material/M_BuildingSilhouette.mat";
    public const string PresetPath = "Assets/RenderFX/Environment/Profiles/BUILDING_Silhouette_Default.asset";
    private static readonly string[] RendererPaths =
    {
        "Assets/Settings/URP-HighFidelity-Renderer.asset",
        "Assets/_ProtoType_Merge/JC/URP-JC-Renderer.asset"
    };

    [MenuItem("JC/건물 실루엣/JC_Environment에 조절 오브젝트 설치")]
    public static void InstallControllerCurrentScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("플레이모드 종료 후 실행하세요.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Transform environment = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == "JC_Environment");
        if (environment == null) throw new InvalidOperationException("현재 씬에서 JC_Environment를 찾지 못했습니다.");

        var preset = AssetDatabase.LoadAssetAtPath<JcBuildingSilhouettePreset>(PresetPath);
        if (preset == null)
        {
            preset = ScriptableObject.CreateInstance<JcBuildingSilhouettePreset>();
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                preset.settings = new JcBuildingSilhouetteSettings
                {
                    fillColor = material.GetColor("_FillColor"),
                    opacity = material.GetFloat("_JcOcclusionAlpha") * material.GetColor("_FillColor").a,
                    outlineColor = material.GetColor("_OutlineColor"),
                    outlineOpacity = material.GetFloat("_JcOcclusionAlpha") * material.GetColor("_FillColor").a,
                    outlineWidthMode = JcBuildingOutlineWidthMode.WorldSpace,
                    outlineWorldWidth = JcBuildingSilhouetteSettings.Default.outlineWorldWidth,
                    outlineWidth = material.GetFloat("_OutlineWidth"),
                    depthBias = material.GetFloat("_DepthBias")
                }.Sanitized();
            }
            AssetDatabase.CreateAsset(preset, PresetPath);
            AssetDatabase.SaveAssetIfDirty(preset);
        }

        var controller = environment.GetComponentInChildren<JcBuildingSilhouetteController>(true);
        if (controller == null)
        {
            var child = new GameObject("JC_BuildingSilhouette");
            Undo.RegisterCreatedObjectUndo(child, "건물 반투명 조절 오브젝트 설치");
            child.transform.SetParent(environment, false);
            controller = Undo.AddComponent<JcBuildingSilhouetteController>(child);
            controller.Preset = preset;
            controller.LoadPreset();
            EditorSceneManager.MarkSceneDirty(scene);
        }
        Selection.activeGameObject = controller.gameObject;
        EditorGUIUtility.PingObject(controller);
        Debug.Log("[JC Silhouette] JC_Environment/JC_BuildingSilhouette 준비 완료. 씬은 직접 저장하세요.");
    }

    [MenuItem("JC/건물 실루엣/렌더러 설치·재적용")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("플레이모드 종료 후 실행하세요.");

        Shader shader = Shader.Find("JC/Environment/Building Silhouette");
        if (shader == null) throw new InvalidOperationException("실루엣 셰이더를 찾을 수 없습니다.");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "M_BuildingSilhouette" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        foreach (string path in RendererPaths)
        {
            UniversalRendererData data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null) throw new InvalidOperationException("렌더러 없음: " + path);
            JcBuildingSilhouetteFeature feature = data.rendererFeatures
                .OfType<JcBuildingSilhouetteFeature>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<JcBuildingSilhouetteFeature>();
                feature.name = "JC Building Silhouette";
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
            }
            feature.silhouetteMaterial = material;
            feature.SetActive(true);
            // URP 14는 참조 복구용 local ID 목록도 병행 보관한다.
            var serialized = new SerializedObject(data);
            SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = data.rendererFeatures.Count;
            for (int i = 0; i < data.rendererFeatures.Count; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i], out string _, out long id);
                map.GetArrayElementAtIndex(i).longValue = id;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
            data.SetDirty();
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[JC Silhouette] 렌더러 설치 완료. 채움 색상은 " + MaterialPath);
    }

    [MenuItem("JC/건물 실루엣/현재 씬 가림 머티리얼 연결")]
    public static void ConnectCurrentScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("플레이모드 종료 후 실행하세요.");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null) throw new InvalidOperationException("렌더러 설치를 먼저 실행하세요.");
        int count = 0;
        foreach (PartyOcclusionFadeController controller in UnityEngine.Object.FindObjectsByType<PartyOcclusionFadeController>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("transparentOverrideMaterial").objectReferenceValue = material;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            count++;
        }
        Debug.Log($"[JC Silhouette] {count}개 가림 컨트롤러 연결. 씬은 직접 저장하세요.");
    }

    // 닫힌 에디터에서 최초 설치할 때만 사용. 기존 씬은 머티리얼 GUID 한 곳만 교체하여
    // 전체 씬 재직렬화에 따른 팀원 변경/줄바꿈 손실을 방지한다.
    public static void InstallBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("배치 실행 전용입니다.");
        Install();
        string guid = AssetDatabase.AssetPathToGUID(MaterialPath);
        string oldReference = "transparentOverrideMaterial: {fileID: 2100000, guid: cded4d3e5ffc28649a69c5f090f14279,";
        string newReference = "transparentOverrideMaterial: {fileID: 2100000, guid: " + guid + ",";
        foreach (string scene in new[] { "DHScene_3", "TutorialExploreScene" })
        {
            string path = "Assets/_ProtoType_Merge/Scenes/" + scene + ".unity";
            string text = File.ReadAllText(path);
            if (!text.Contains(oldReference) && !text.Contains(newReference))
                throw new InvalidOperationException("예상하지 못한 가림 머티리얼: " + path);
            if (text.Contains(oldReference))
                File.WriteAllText(path, text.Replace(oldReference, newReference));
        }
        AssetDatabase.Refresh();
        Debug.Log("[JC Silhouette] INSTALL_OK");
    }
}
