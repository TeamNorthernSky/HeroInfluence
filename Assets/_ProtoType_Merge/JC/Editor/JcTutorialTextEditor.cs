using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class JcTutorialTextEditor : AssetPostprocessor, IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    [MenuItem("JC/튜토리얼/외부 대본 열기")]
    static void Open() => EditorUtility.OpenWithDefaultApp(JcTutorialText.FilePath);

    [MenuItem("JC/튜토리얼/대본 검사")]
    public static void ValidateMenu()
    {
        if (Validate(out var error)) Debug.Log("[튜토리얼 대본] ID·구문·치환 값 검사를 통과했습니다.");
        else Debug.LogError("[튜토리얼 대본] " + error);
    }
    public static bool Validate(out string error)
    {
        error = null;
        try
        {
            var asset = Resources.Load<TextAsset>(JcTutorialText.ManifestResource);
            if (asset == null) { error = "문구 연결 목록이 없습니다."; return false; }
            var schema = JsonUtility.FromJson<JcTutorialText.Manifest>(asset.text);
            return JcTutorialText.TryValidate(JcTutorialText.ReadSource(JcTutorialText.FilePath), schema, out _, out error);
        }
        catch (Exception e) { error = e.Message; return false; }
    }
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        string path = "Assets/StreamingAssets/" + JcTutorialText.RelativePath;
        if (Array.IndexOf(imported, path) >= 0 || Array.IndexOf(deleted, path) >= 0 || Array.IndexOf(movedFrom, path) >= 0)
        {
            if (!Validate(out var error)) Debug.LogError("[튜토리얼 대본] " + error);
            else if (EditorApplication.isPlaying) JcTutorialText.Reload(true);
        }
    }
    public void OnPreprocessBuild(BuildReport report)
    {
        if (!Validate(out var error)) throw new BuildFailedException("튜토리얼 대본 오류: " + error);
    }
}
