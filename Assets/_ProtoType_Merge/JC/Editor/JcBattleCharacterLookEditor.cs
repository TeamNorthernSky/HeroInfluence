using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

[CustomEditor(typeof(JcBattleCharacterLook))]
public sealed class JcBattleCharacterLookEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var look=(JcBattleCharacterLook)target;
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        DrawPropertiesExcluding(serializedObject,"m_Script");
        bool changed=EditorGUI.EndChangeCheck();serializedObject.ApplyModifiedProperties();
        if(changed){look.RefreshLook();if(Application.isPlaying)JcBattleLookDraft.Remember(look);}
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("인스펙터의 값은 조절 중인 초안입니다. 플레이 중에도 '프로필 저장'을 누르면 종료 후 값이 유지됩니다. 대상 루트·셰이더·편집 미리보기 연결은 씬에만 저장됩니다.",MessageType.Info);
        using(new EditorGUI.DisabledScope(look.profile==null))
        {
            if(GUILayout.Button(new GUIContent("현재 값으로 프로필 저장","현재 외형값을 연결된 프로필 파일에 저장합니다. 플레이 중에도 디스크에 보존됩니다.")))SaveProfile(look);
            if(GUILayout.Button(new GUIContent("프로필 불러오기","현재 초안을 버리고 프로필의 저장값으로 복원합니다."))){Undo.RecordObject(look,"룩 프로필 불러오기");look.LoadProfile();}
        }
        if(GUILayout.Button(new GUIContent("새 프로필로 저장","현재 외형값으로 새 프로필을 만듭니다. 플레이 중 생성할 때는 씬 연결이 종료 후 되돌아갈 수 있으므로 편집 모드에서 연결을 확인하세요.")))
        {
            string path=EditorUtility.SaveFilePanelInProject("캐릭터 룩 프로필 저장","BattleLookProfile","asset","저장할 위치를 지정하세요.");
            if(!string.IsNullOrEmpty(path)){var profile=ScriptableObject.CreateInstance<JcBattleLookProfile>();profile.settings=look.CaptureSettings();AssetDatabase.CreateAsset(profile,path);Undo.RecordObject(look,"룩 프로필 연결");look.profile=profile;EditorUtility.SetDirty(look);if(!Application.isPlaying)EditorSceneManager.MarkSceneDirty(look.gameObject.scene);}
        }
        if(GUILayout.Button(new GUIContent("마지막 플레이 조절값 복구","같은 에디터 세션에서 보관한 플레이 초안을 불러옵니다. 복구 후 프로필 저장을 눌러야 영구 보존됩니다.")))
        {
            var draft=JcBattleLookDraft.Read(look);if(draft!=null){Undo.RecordObject(look,"플레이 룩 복구");look.ApplySettings(draft);}else EditorUtility.DisplayDialog("캐릭터 룩","보관한 플레이 값이 없습니다.","확인");
        }
    }
    public static void SaveProfile(JcBattleCharacterLook look)
    {
        if(look.profile==null)return;Undo.RecordObject(look.profile,"캐릭터 룩 프로필 저장");look.profile.settings=look.CaptureSettings();EditorUtility.SetDirty(look.profile);AssetDatabase.SaveAssetIfDirty(look.profile);if(Application.isPlaying)JcBattleLookDraft.Remember(look);
    }
}
[InitializeOnLoad]
public static class JcBattleLookDraft
{
    static JcBattleLookDraft(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingPlayMode)foreach(var look in Object.FindObjectsOfType<JcBattleCharacterLook>(true))Remember(look);};}
    private static string Key(JcBattleCharacterLook look)=>"JC.BattleLook.Draft."+look.gameObject.scene.path+"/"+look.name;
    public static void Remember(JcBattleCharacterLook look)=>SessionState.SetString(Key(look),JsonUtility.ToJson(look.CaptureSettings()));
    public static JcBattleLookSettings Read(JcBattleCharacterLook look){var json=SessionState.GetString(Key(look),"");return string.IsNullOrEmpty(json)?null:JsonUtility.FromJson<JcBattleLookSettings>(json);}
}
