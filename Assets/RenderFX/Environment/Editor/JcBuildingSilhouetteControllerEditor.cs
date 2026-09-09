using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(JcBuildingSilhouetteController))]
public sealed class JcBuildingSilhouetteControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var controller = (JcBuildingSilhouetteController)target;
        serializedObject.Update();
        DrawProperty(serializedObject.FindProperty("preset"), "프리셋");
        DrawProperty(serializedObject.FindProperty("loadPresetOnPlay"), "플레이 시작 시 프리셋 불러오기");
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("반투명 실시간 조절", EditorStyles.boldLabel);
        var settings = serializedObject.FindProperty("settings");
        DrawProperty(settings.FindPropertyRelative("holdBuildingFadeWhileMoving"), "이동 중 반투명 유지");
        DrawProperty(settings.FindPropertyRelative("buildingRestoreDelay"), "복원 대기 시간 (초)");
        DrawProperty(settings.FindPropertyRelative("preciseBuildingOcclusion"), "일반 건물 메시 정밀 판정");
        DrawProperty(settings.FindPropertyRelative("occlusionBoxOffset"), "판정 상자 중심 오프셋");
        DrawProperty(settings.FindPropertyRelative("occlusionBoxSize"), "판정 상자 전체 크기");
        DrawProperty(settings.FindPropertyRelative("occlusionCenterPriority"), "중심점 가림 시 즉시 적용");
        DrawProperty(settings.FindPropertyRelative("occlusionRequiredCorners"), "최소 가림 꼭지점 수");
        DrawProperty(settings.FindPropertyRelative("showOcclusionBox"), "Scene 뷰 판정 상자 표시");
        EditorGUILayout.Space();
        DrawProperty(settings.FindPropertyRelative("fillColor"), "채움 색상");
        DrawProperty(settings.FindPropertyRelative("opacity"), "채움 불투명도");
        DrawProperty(settings.FindPropertyRelative("outlineColor"), "윤곽선 색상");
        DrawProperty(settings.FindPropertyRelative("outlineOpacity"), "윤곽선 불투명도");
        var widthMode = settings.FindPropertyRelative("outlineWidthMode");
        DrawProperty(widthMode, "윤곽선 두께 기준");
        if (widthMode.intValue == (int)JcBuildingOutlineWidthMode.WorldSpace)
            DrawProperty(settings.FindPropertyRelative("outlineWorldWidth"), "윤곽선 두께 (월드 단위)");
        else
            DrawProperty(settings.FindPropertyRelative("outlineWidth"), "윤곽선 두께 (px)");
        DrawProperty(settings.FindPropertyRelative("depthBias"), "깊이 비교 여유");
        if (serializedObject.ApplyModifiedProperties())
        {
            if (Application.isPlaying) JcBuildingSilhouettePlayDraft.Remember(controller);
            SceneView.RepaintAll();
        }

        EditorGUILayout.HelpBox("파티를 가리는 건물에만 적용됩니다. 채움과 윤곽선의 불투명도를 각각 조절할 수 있습니다. 두께 0이면 윤곽선을 끕니다.\n게임 공간 기준은 건물과 함께 선이 확대·축소됩니다. 화면 픽셀 고정은 줌과 무관하게 같은 두께입니다.\n값 변경은 즉시 반영됩니다. 프리셋은 저장 버튼으로 기록하며, 원본 머티리얼은 변경하지 않습니다.", MessageType.Info);
        if (controller.Preset != null)
        {
            bool different = JsonUtility.ToJson(controller.Settings) != JsonUtility.ToJson(controller.Preset.settings.Sanitized());
            EditorGUILayout.LabelField(different ? "● 프리셋과 다른 조정값" : "프리셋과 동일한 값", EditorStyles.miniLabel);
        }

        using (new EditorGUILayout.HorizontalScope())
        using (new EditorGUI.DisabledScope(controller.Preset == null))
        {
            if (GUILayout.Button(new GUIContent("프리셋 불러오기", "지정된 프리셋의 값으로 현재 조정값을 교체하고 화면에 즉시 반영합니다. 프리셋 파일에는 기록하지 않습니다. 프리셋을 지정해야 사용할 수 있습니다.")))
            {
                Undo.RecordObject(controller, "건물 반투명 프리셋 불러오기");
                controller.LoadPreset();
                MarkController(controller);
            }
            if (GUILayout.Button(new GUIContent("현재 값을 프리셋에 저장", "현재 색상·불투명도·윤곽선·깊이 비교 여유를 지정된 프리셋 파일에 덮어씁니다. 플레이 중 저장해도 종료 후 유지됩니다. 씬이나 원본 머티리얼은 저장하지 않습니다."))) SavePreset(controller);
        }
        if (GUILayout.Button(new GUIContent("현재 값으로 새 프리셋 저장…", "저장 위치를 선택하여 현재 조정값의 새 프리셋을 만들고 연결합니다. 플레이 중 바꾼 프리셋 연결은 종료 시 복원되지만 새 파일은 유지됩니다.")))
        {
            string path = EditorUtility.SaveFilePanelInProject("건물 반투명 프리셋 저장", "BUILDING_Silhouette", "asset", "새 프리셋의 저장 위치를 선택하세요.", "Assets/RenderFX/Environment/Profiles");
            if (!string.IsNullOrEmpty(path))
            {
                var preset = CreateInstance<JcBuildingSilhouettePreset>();
                preset.settings = controller.Settings;
                AssetDatabase.CreateAsset(preset, path);
                AssetDatabase.SaveAssetIfDirty(preset);
                Undo.RecordObject(controller, "건물 반투명 프리셋 지정");
                controller.Preset = preset;
                MarkController(controller);
            }
        }

        if (!Application.isPlaying && JcBuildingSilhouettePlayDraft.TryRead(controller, out var draft))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("마지막 플레이 조정값이 남아 있습니다. 가져온 뒤 프리셋에 저장할 수 있습니다. 이 임시 값은 Unity를 닫으면 사라집니다.", MessageType.None);
            if (GUILayout.Button(new GUIContent("마지막 플레이 조정값 가져오기", "플레이 종료 직전에 임시 보관한 값으로 현재 조정값을 교체합니다. 프리셋에 남기려면 저장 버튼을 누르세요. 임시 보관값은 Unity를 닫으면 사라집니다.")))
            {
                Undo.RecordObject(controller, "건물 반투명 플레이 조정값 가져오기");
                controller.Settings = draft;
                MarkController(controller);
            }
        }
    }

    private static void DrawProperty(SerializedProperty property, string label)
        => EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));

    public static void SavePreset(JcBuildingSilhouetteController controller)
    {
        if (controller.Preset == null) throw new InvalidOperationException("프리셋을 지정하세요.");
        Undo.RecordObject(controller.Preset, "건물 반투명 프리셋 저장");
        controller.Preset.settings = controller.Settings;
        EditorUtility.SetDirty(controller.Preset);
        // 다른 환경/재질의 미저장 튜닝은 함께 저장하지 않는다.
        AssetDatabase.SaveAssetIfDirty(controller.Preset);
        if (Application.isPlaying) JcBuildingSilhouettePlayDraft.Remember(controller);
    }

    private static void MarkController(JcBuildingSilhouetteController controller)
    {
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }
        else JcBuildingSilhouettePlayDraft.Remember(controller);
        SceneView.RepaintAll();
    }
}

/// <summary>플레이 종료 전에 초안만 SessionState에 보관한다. 에셋/씬 자동 저장은 하지 않는다.</summary>
[InitializeOnLoad]
public static class JcBuildingSilhouettePlayDraft
{
    static JcBuildingSilhouettePlayDraft()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.ExitingPlayMode) return;
            foreach (var controller in UnityEngine.Object.FindObjectsByType<JcBuildingSilhouetteController>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None)) Remember(controller);
        };
    }

    private static string Key(JcBuildingSilhouetteController controller)
    {
        string path = controller.name;
        for (Transform parent = controller.transform.parent; parent != null; parent = parent.parent)
            path = parent.name + "/" + path;
        return "JC.BuildingSilhouette.PlayDraft:" + controller.gameObject.scene.path + ":" + path;
    }

    public static void Remember(JcBuildingSilhouetteController controller)
        => SessionState.SetString(Key(controller), JsonUtility.ToJson(controller.Settings));

    public static bool TryRead(JcBuildingSilhouetteController controller, out JcBuildingSilhouetteSettings settings)
    {
        string json = SessionState.GetString(Key(controller), string.Empty);
        settings = string.IsNullOrEmpty(json) ? default : JsonUtility.FromJson<JcBuildingSilhouetteSettings>(json);
        return !string.IsNullOrEmpty(json);
    }
}
