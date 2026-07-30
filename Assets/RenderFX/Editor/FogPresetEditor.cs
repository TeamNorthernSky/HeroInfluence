using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [JC 신설 260714] FogPreset 인스펙터 확장 — JSON 내보내기/불러오기 버튼.
/// 팀원 간 fog 튜닝 공유용. 플레이/에딧 모드 모두 동작하며,
/// 플레이 중 불러오면 NotifyChanged로 즉시 화면에 반영된다.
/// JSON은 프리셋의 직렬화 필드 전체(unexplored/fogged/shared)를 담는다.
/// </summary>
[CustomEditor(typeof(FogPreset))]
public class FogPresetEditor : Editor
{
    private const string DefaultJsonFolder = "Assets/RenderFX/FogPreset_json";   // 260729 폴더 이동(_Scripts 폐지) 반영

    /// <summary>다이얼로그 시작 폴더 — 없으면 생성 후 절대경로 반환.</summary>
    private static string GetJsonFolder()
    {
        string absolute = Path.GetFullPath(DefaultJsonFolder);
        if (!Directory.Exists(absolute))
        {
            Directory.CreateDirectory(absolute);
            AssetDatabase.Refresh();
        }
        return absolute;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var preset = (FogPreset)target;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("JSON 공유 (팀원 간 튜닝 전달)", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("JSON으로 내보내기…"))
                ExportJson(preset);
            if (GUILayout.Button("JSON 불러오기…"))
                ImportJson(preset);
        }
    }

    private static void ExportJson(FogPreset preset)
    {
        string path = EditorUtility.SaveFilePanel(
            "FogPreset JSON 내보내기", GetJsonFolder(), preset.name + ".json", "json");
        if (string.IsNullOrEmpty(path))
            return;

        File.WriteAllText(path, JsonUtility.ToJson(preset, true));
        Debug.Log($"[FogPreset] JSON 내보내기 완료: {path}");
    }

    private static void ImportJson(FogPreset preset)
    {
        string path = EditorUtility.OpenFilePanel("FogPreset JSON 불러오기", GetJsonFolder(), "json");
        if (string.IsNullOrEmpty(path))
            return;

        Undo.RecordObject(preset, "FogPreset JSON Import");
        JsonUtility.FromJsonOverwrite(File.ReadAllText(path), preset);
        EditorUtility.SetDirty(preset);
        preset.NotifyChanged();
        Debug.Log($"[FogPreset] JSON 불러오기 완료: {Path.GetFileName(path)} → {preset.name} (플레이 중이면 즉시 반영, 영구 저장은 Ctrl+S)");
    }
}
