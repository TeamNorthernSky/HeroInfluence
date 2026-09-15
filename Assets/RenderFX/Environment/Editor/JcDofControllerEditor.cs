using System;
using JC.Env;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

[CustomEditor(typeof(JcDofController))]
public sealed class JcDofControllerEditor : Editor
{
    private void OnEnable() => Undo.undoRedoPerformed += OnUndoRedo;
    private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;
    private void OnUndoRedo()
    {
        if (target is JcDofController controller) controller.ApplyNow();
        Repaint();
        SceneView.RepaintAll();
    }
    public override bool RequiresConstantRepaint() => Application.isPlaying;

    public override void OnInspectorGUI()
    {
        var controller = (JcDofController)target;
        serializedObject.Update();
        Field("profile", "DOF 프로파일");
        Field("targetVolume", "대상 Volume");
        Field("targetCamera", "초점 카메라");
        Field("loadProfileOnPlay", "플레이 시작 시 불러오기");
        EditorGUILayout.Space();
        DrawSettings(serializedObject.FindProperty("settings"));
        if (serializedObject.ApplyModifiedProperties())
        {
            controller.Settings = controller.Settings;
            MarkController(controller);
        }

        EditorGUILayout.Space();
        if (!controller.isActiveAndEnabled)
            EditorGUILayout.HelpBox("컨트롤러가 꺼져 있습니다. 기존 Volume/초점 추적기 동작으로 복귀합니다. DOF 자체를 끄려면 모드를 Off로 설정하세요.", MessageType.Info);
        else if (!string.IsNullOrEmpty(controller.Status))
            EditorGUILayout.HelpBox(controller.Status, MessageType.Warning);
        else if (controller.Settings.mode != DepthOfFieldMode.Off)
        {
            EditorGUILayout.LabelField("현재 초점", $"{controller.CurrentFocusDistance:F3}  ({(controller.HasAutoFocus ? "자동" : "수동")}, 월드 단위)");
            if (controller.Settings.mode == DepthOfFieldMode.Gaussian)
                EditorGUILayout.LabelField("실제 블러 시작 / 종료", $"{controller.CurrentGaussianStart:F3} / {controller.CurrentGaussianEnd:F3}");
        }
        EditorGUILayout.HelpBox("값 변경은 즉시 반영됩니다. 프로파일 저장은 아래 버튼으로만 수행합니다.\n플레이 중 저장한 파일은 종료 후에도 유지됩니다. 새 프로파일 연결은 플레이 종료 후 다시 지정하세요.", MessageType.None);

        bool persistentProfile = controller.Profile != null && AssetDatabase.Contains(controller.Profile);
        if (persistentProfile)
            EditorGUILayout.LabelField(JsonUtility.ToJson(controller.Settings.Sanitized()) == JsonUtility.ToJson(controller.Profile.settings.Sanitized())
                ? "프로파일과 동일한 값" : "● 프로파일과 다른 조정값", EditorStyles.miniLabel);

        using (new EditorGUI.DisabledScope(!persistentProfile))
        {
            if (GUILayout.Button(new GUIContent("프로파일 불러오기", "연결된 프로파일의 설정으로 현재 조정값을 교체하고 즉시 적용합니다. 저장하지 않은 조정값은 교체됩니다. 파일은 변경하지 않습니다.")))
            {
                Undo.RecordObject(controller, "DOF 프로파일 불러오기");
                controller.LoadProfile();
                MarkController(controller);
            }
            if (GUILayout.Button(new GUIContent("현재 값을 프로파일에 저장", "현재 DOF·초점 설정을 연결된 프로파일 파일에 덮어씁니다. 플레이 종료 후에도 유지됩니다. 카메라·Volume 연결과 씬은 저장하지 않습니다.")))
                SaveProfile(controller);
        }
        if (GUILayout.Button(new GUIContent("현재 값으로 새 프로파일 저장…", "현재 설정으로 새 .asset 파일을 만들고 연결합니다. 기존 파일명과 겹치면 새 이름을 사용합니다. 플레이 중 만든 파일은 유지되지만 연결 변경은 종료 시 복원됩니다.")))
        {
            string path = EditorUtility.SaveFilePanelInProject("DOF 프로파일 저장", "DOF_Miniature", "asset",
                "새 프로파일의 저장 위치를 선택하세요.", "Assets/RenderFX/Environment/Profiles");
            if (!string.IsNullOrEmpty(path)) CreateProfile(controller, AssetDatabase.GenerateUniqueAssetPath(path));
        }
        if (!Application.isPlaying && JcDofPlayDraft.TryRead(controller, out var draft))
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("마지막 플레이 조정값이 임시 보관되어 있습니다. 가져온 뒤 프로파일에 저장할 수 있습니다. Unity를 닫으면 임시 값은 사라집니다.", MessageType.None);
            if (GUILayout.Button(new GUIContent("마지막 플레이 조정값 가져오기", "플레이 중 임시 보관한 DOF 설정을 현재 조정값으로 가져옵니다. 프로파일 파일에는 기록하지 않습니다. 보관하려면 가져온 뒤 저장하세요.")))
            {
                Undo.RecordObject(controller, "DOF 플레이 조정값 가져오기");
                controller.Settings = draft;
                MarkController(controller);
            }
        }
    }

    private void Field(string name, string label)
    {
        var property = serializedObject.FindProperty(name);
        EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
    }

    internal static void DrawSettings(SerializedProperty settings)
    {
        void Draw(string name, string label)
        {
            var property = settings.FindPropertyRelative(name);
            EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip));
        }
        Draw("mode", "DOF 모드");
        var mode = (DepthOfFieldMode)settings.FindPropertyRelative("mode").intValue;
        if (mode == DepthOfFieldMode.Off) return;
        Draw("autoFocus", "바닥 자동 초점");
        bool auto = settings.FindPropertyRelative("autoFocus").boolValue;
        if (auto) Draw("groundY", "바닥 높이 (월드 Y)");
        if (mode == DepthOfFieldMode.Gaussian)
        {
            if (auto)
            {
                Draw("gaussianStartOffset", "블러 시작 오프셋");
                Draw("gaussianEndOffset", "최대 블러 오프셋");
            }
            Draw("gaussianStart", auto ? "추적 실패 시 시작 거리" : "블러 시작 거리");
            Draw("gaussianEnd", auto ? "추적 실패 시 종료 거리" : "최대 블러 거리");
            Draw("gaussianMaxRadius", "최대 블러 반경");
            Draw("highQualitySampling", "고품질 샘플링");
        }
        else
        {
            Draw("focusDistance", auto ? "추적 실패 시 초점 거리" : "수동 초점 거리");
            Draw("aperture", "조리개 (f-number)");
            Draw("focalLength", "렌즈 초점거리 (mm)");
            Draw("bladeCount", "조리개 날개 개수");
            Draw("bladeCurvature", "날개 곡률");
            Draw("bladeRotation", "날개 회전 (도)");
        }
    }

    public static void SaveProfile(JcDofController controller)
    {
        if (controller.Profile == null || !AssetDatabase.Contains(controller.Profile))
            throw new InvalidOperationException("저장할 DOF 프로파일 에셋을 연결하세요.");
        Undo.RecordObject(controller.Profile, "DOF 프로파일 저장");
        controller.Profile.settings = controller.Settings.Sanitized();
        EditorUtility.SetDirty(controller.Profile);
        AssetDatabase.SaveAssetIfDirty(controller.Profile);
        if (Application.isPlaying) JcDofPlayDraft.Remember(controller);
    }

    public static JcDofProfile CreateProfile(JcDofController controller, string path)
    {
        if (!path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)) || System.IO.File.Exists(path))
            throw new ArgumentException("Assets 안의 비어 있는 .asset 경로가 필요합니다.", nameof(path));
        var profile = CreateInstance<JcDofProfile>();
        profile.settings = controller.Settings.Sanitized();
        AssetDatabase.CreateAsset(profile, path);
        AssetDatabase.SaveAssetIfDirty(profile);
        Undo.RecordObject(controller, "DOF 프로파일 연결");
        controller.Profile = profile;
        MarkController(controller);
        return profile;
    }

    private static void MarkController(JcDofController controller)
    {
        if (Application.isPlaying) JcDofPlayDraft.Remember(controller);
        else
        {
            EditorUtility.SetDirty(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
    }
}

[CustomEditor(typeof(JcDofProfile))]
public sealed class JcDofProfileEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("일반 조정은 씬의 JC_Environment/JC_DOF에서 진행하세요. 여기서는 프로파일 에셋 자체를 편집하며, 씬에는 컨트롤러의 불러오기로 적용합니다.", MessageType.Info);
        serializedObject.Update();
        JcDofControllerEditor.DrawSettings(serializedObject.FindProperty("settings"));
        serializedObject.ApplyModifiedProperties();
        if (GUILayout.Button(new GUIContent("프로파일 파일 저장", "이 프로파일 에셋의 현재 값을 디스크에 저장합니다. 씬 컨트롤러의 조정값을 가져오지는 않습니다.")))
        {
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
        }
    }
}

/// <summary>도메인 리로드를 통과하는 세션 임시 값입니다. 씬/프로파일 자동 저장은 하지 않습니다.</summary>
[InitializeOnLoad]
public static class JcDofPlayDraft
{
    static JcDofPlayDraft()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.ExitingPlayMode) return;
            foreach (var controller in UnityEngine.Object.FindObjectsByType<JcDofController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Remember(controller);
        };
    }

    private static string Key(JcDofController controller)
    {
        string path = controller.name + ":" + controller.transform.GetSiblingIndex();
        for (var parent = controller.transform.parent; parent != null; parent = parent.parent)
            path = parent.name + ":" + parent.GetSiblingIndex() + "/" + path;
        return "JC.DOF.PlayDraft:" + controller.gameObject.scene.path + ":" + path;
    }

    public static void Remember(JcDofController controller)
        => SessionState.SetString(Key(controller), JsonUtility.ToJson(controller.Settings.Sanitized()));

    public static bool TryRead(JcDofController controller, out JcDofSettings settings)
    {
        string json = SessionState.GetString(Key(controller), string.Empty);
        settings = string.IsNullOrEmpty(json) ? default : JsonUtility.FromJson<JcDofSettings>(json).Sanitized();
        return !string.IsNullOrEmpty(json);
    }
}
