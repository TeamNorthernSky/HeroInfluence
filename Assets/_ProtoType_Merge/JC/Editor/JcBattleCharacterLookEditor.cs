using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

[CustomEditor(typeof(JcBattleCharacterLook))]
public sealed class JcBattleCharacterLookEditor : Editor
{
    // JC | 2026-11-18 | 실제 실내광에서 무효인 가상 명암과 공통 색 보정을 분리해 조절 대상을 명확히 합니다.
    bool legacyExpanded;
    void OnEnable(){Undo.undoRedoPerformed+=RefreshAfterUndo;}
    void OnDisable(){Undo.undoRedoPerformed-=RefreshAfterUndo;}
    void RefreshAfterUndo(){if(target is JcBattleCharacterLook look){look.RefreshLook();Repaint();}}
    public override void OnInspectorGUI()
    {
        var look=(JcBattleCharacterLook)target;
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        foreach(var name in new[]{"profile","heroRoot","enemyRoot","additionalObjectRoots","characterShader","previewInEditor"})
            EditorGUILayout.PropertyField(serializedObject.FindProperty(name),true);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("캐릭터 채도·밝기 · 모든 시간대 공통",EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("colorAdjustmentEnabled"));
        using(new EditorGUI.DisabledScope(!serializedObject.FindProperty("colorAdjustmentEnabled").boolValue))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("saturation"),new GUIContent("채도","0은 무채색, 1은 원래 채도, 1보다 크면 더 선명해집니다."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("brightnessEv"));
        }
        EditorGUILayout.HelpBox("실내 실제 조명·IBL·그림자는 유지하고 캐릭터/전투 유닛의 불투명 몸체 색만 보정합니다. 배경·바닥·유리·파티클에는 적용하지 않습니다.\n밝기 EV부터 낮춘 뒤 채도를 조절하세요. 보정 사용을 끄면 수치는 보존한 채 보정 전과 비교합니다. 시간대별 조명 차이는 그대로 남습니다.",MessageType.Info);
        legacyExpanded=EditorGUILayout.Foldout(legacyExpanded,"기존 가상 명암 · 부드러운 캐릭터 명암 방식 전용",true);
        if(legacyExpanded)
        {
            EditorGUILayout.HelpBox("아래 최소 밝기·원본색 밝기 보정·가상 주광·림·몰딩은 실내 실제 조명 방식에서 적용되지 않습니다. 보스의 밝기는 위의 밝기 EV로 조절하세요.",MessageType.Info);
            DrawPropertiesExcluding(serializedObject,"m_Script","amplifierSurface","profile","heroRoot","enemyRoot","additionalObjectRoots","characterShader","previewInEditor","saturation","colorAdjustmentEnabled","brightnessEv");
        }
        bool changed=EditorGUI.EndChangeCheck();serializedObject.ApplyModifiedProperties();
        if(changed){look.RefreshLook();if(Application.isPlaying)JcBattleLookDraft.Remember(look);}
        // JC | 2026-11-18 | 공통 보정만 선택 저장/복원하여 증폭기·가상 명암의 미저장 초안을 덮어쓰지 않습니다.
        if(GUILayout.Button("채도·밝기 중립값 (1 / 0 EV)"))
        {
            Undo.RecordObject(look,"캐릭터 색 중립값");look.saturation=1;look.brightnessEv=0;
            RefreshColorDraft(look);
        }
        using(new EditorGUI.DisabledScope(!look.profile))
        {
            if(GUILayout.Button("채도·밝기만 프로필 저장"))SaveColorProfile(look);
            if(GUILayout.Button("채도·밝기 저장값 다시 적용"))
            {
                Undo.RecordObject(look,"캐릭터 색 불러오기");
                look.colorAdjustmentEnabled=look.profile.settings.colorAdjustmentEnabled;
                look.saturation=look.profile.settings.saturation;look.brightnessEv=look.profile.settings.brightnessEv;
                RefreshColorDraft(look);
            }
        }
        EditorGUILayout.HelpBox("위치: JC_BattleLook_캐릭터명암 → 캐릭터 채도·밝기. 플레이 조절 후 종료 전에 ‘채도·밝기만 프로필 저장’을 누르세요. 연결된 BattleLook 프로필을 공유하는 본게임·테스트 및 다른 전투에도 같은 값이 적용됩니다. 시간대 프로필을 저장할 필요는 없습니다.",MessageType.Info);
        // JC | 2026-11-18 | 기존 외형값과 증폭기만의 표면 저장을 구분하여 다른 캐릭터 초안을 실수로 덮어쓰지 않습니다.
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("증폭기 몸체 · 40002 / 40003",EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("amplifierSurface"),new GUIContent("표면 조절"),true);
        if(serializedObject.ApplyModifiedProperties()){look.RefreshLook();if(Application.isPlaying)JcBattleLookDraft.Remember(look);}
        // JC | 2026-11-18 | 법선 정리와 실루엣 수정의 범위를 구분하고 0으로 원복 가능한 조절법을 안내합니다.
        EditorGUILayout.HelpBox("정점 위치·UV·삼각형과 중앙 구체 VFX는 유지합니다. 판넬 법선 정리 0은 원본, 1은 정리된 명암이며 외곽선의 찌그러짐은 바꾸지 않습니다. 본게임과 테스트는 연결된 프로필을 공유합니다.\n플레이 종료 전 아래 저장 버튼으로 보존하세요. 홈 음영은 베이크 AO이며 씬 전체 SSAO 설정은 바꾸지 않습니다.",MessageType.Info);
        if(look.profile && (!look.profile.amplifierMesh || !look.profile.amplifierMask))
            EditorGUILayout.HelpBox("이 프로필에 증폭기 메시 또는 색 영역·AO 맵이 연결되지 않아 표면 보정이 적용되지 않습니다.",MessageType.Warning);
        using(new EditorGUI.DisabledScope(!look.profile))
        {
            if(GUILayout.Button("증폭기 표면만 프로필 저장")) SaveAmplifierProfile(look);
            if(GUILayout.Button("증폭기 표면 저장값 다시 적용"))
            {
                Undo.RecordObject(look,"증폭기 표면 불러오기");
                look.amplifierSurface=JsonUtility.FromJson<JcAmplifierSurfaceSettings>(JsonUtility.ToJson(look.profile.settings.amplifierSurface));
                look.RefreshLook();serializedObject.Update();
            }
        }
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
            if(!string.IsNullOrEmpty(path)){
                var profile=ScriptableObject.CreateInstance<JcBattleLookProfile>();profile.settings=look.CaptureSettings();
                // JC | 2026-11-18 | 새 외형 프로필도 기존 증폭기 데이터 연결을 보존합니다.
                if(look.profile){profile.amplifierMesh=look.profile.amplifierMesh;profile.amplifierShadingMesh=look.profile.amplifierShadingMesh;profile.amplifierMask=look.profile.amplifierMask;}
                AssetDatabase.CreateAsset(profile,path);Undo.RecordObject(look,"룩 프로필 연결");look.profile=profile;EditorUtility.SetDirty(look);if(!Application.isPlaying)EditorSceneManager.MarkSceneDirty(look.gameObject.scene);
            }
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
    // JC | 2026-11-18 | 색 보정은 시간대 설정이 아닌 기존 BattleLook 프로필의 공통 값으로만 보존합니다.
    public static void SaveColorProfile(JcBattleCharacterLook look)
    {
        if(!look.profile)return;
        Undo.RecordObject(look.profile,"캐릭터 채도 밝기 저장");
        look.profile.settings.colorAdjustmentEnabled=look.colorAdjustmentEnabled;
        look.profile.settings.saturation=look.saturation;look.profile.settings.brightnessEv=look.brightnessEv;
        EditorUtility.SetDirty(look.profile);AssetDatabase.SaveAssetIfDirty(look.profile);
        if(Application.isPlaying)JcBattleLookDraft.Remember(look);
    }
    void RefreshColorDraft(JcBattleCharacterLook look)
    {
        look.RefreshLook();serializedObject.Update();EditorUtility.SetDirty(look);
        if(Application.isPlaying)JcBattleLookDraft.Remember(look);
    }
    // JC | 2026-11-18 | 증폭기 설정만 복사하여 동일 프로필의 캐릭터 명암·채도는 저장 전 값 그대로 보존합니다.
    public static void SaveAmplifierProfile(JcBattleCharacterLook look)
    {
        if(!look.profile)return;
        Undo.RecordObject(look.profile,"증폭기 표면 저장");
        look.profile.settings.amplifierSurface=JsonUtility.FromJson<JcAmplifierSurfaceSettings>(JsonUtility.ToJson(look.amplifierSurface));
        EditorUtility.SetDirty(look.profile);AssetDatabase.SaveAssetIfDirty(look.profile);
        if(Application.isPlaying)JcBattleLookDraft.Remember(look);
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
