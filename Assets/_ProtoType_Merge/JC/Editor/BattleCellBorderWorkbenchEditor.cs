using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(BattleCellBorderWorkbench))]
public sealed class BattleCellBorderWorkbenchEditor : Editor
{
    private static readonly string[] Names={"현재 턴 · 노랑","선택 가능 · 보라","확정 대상 · 빨강","선택 불가 · 회색"};
    private static readonly string[] Fields={"currentTurn","selectable","confirmedArea","unavailable"};
    public override void OnInspectorGUI()
    {
        var workbench=(BattleCellBorderWorkbench)target;
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("profile"),new GUIContent("저장 프로필"));
        EditorGUILayout.HelpBox("초안은 즉시 미리보기에 반영됩니다. 프로필 저장을 누르면 전투씬도 같은 값을 사용합니다. 프리팹을 직접 편집할 필요가 없습니다.",MessageType.Info);
        int index=Mathf.Clamp((int)workbench.selectedState-1,0,3);
        index=EditorGUILayout.Popup("편집할 상태",index,Names);
        serializedObject.FindProperty("selectedState").enumValueIndex=index+1;
        var draft=serializedObject.FindProperty("draft");
        var style=draft.FindPropertyRelative(Fields[index]);
        EditorGUILayout.LabelField("선택한 상태의 형태",EditorStyles.boldLabel);
        var shape=style.FindPropertyRelative("shape");
        string[] shapeFields={"size","padding","cornerRadius","curveSegments","outerWidth","mainWidth","innerWidth","depth","floatHeight"};
        string[] shapeLabels={"기준 셀 크기","셀 안쪽 여백","모서리 반지름","모서리 곡선 분할 수","바깥 보조선 폭","대상색 본체 폭","안쪽 보조선 폭","메시 세로 두께","기본 부유 높이"};
        for(int i=0;i<shapeFields.Length;i++)Draw(shape,shapeFields[i],shapeLabels[i]);
        EditorGUILayout.LabelField("색상과 표시",EditorStyles.boldLabel);
        string[] styleFields={"showOuter","showInner","outlineColor","mainColor","allowSweep","useFill","fillColor","fillOpacity","fillTexture"};
        string[] styleLabels={"바깥 보조선 표시","안쪽 보조선 표시","보조선 공통 색상","대상색 본체 색상","스윕 허용","내부 채움 표시","내부 채움 색상","내부 채움 불투명도","내부 채움 문양"};
        for(int i=0;i<styleFields.Length;i++)Draw(style,styleFields[i],styleLabels[i]);
        EditorGUILayout.Space();EditorGUILayout.LabelField("공통 상하 움직임",EditorStyles.boldLabel);
        Draw(draft,"bobPeriod","기본 왕복 주기(초)");Draw(draft,"bobAmplitude","상하 한쪽 진폭");Draw(draft,"resyncDuration","상태 전환 합류 시간(초)");
        EditorGUILayout.LabelField("빨간 확정 대상만 1.5배 속도 · 복귀 후 공통 높이와 방향에 합류",EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space();EditorGUILayout.LabelField("공통 스윕",EditorStyles.boldLabel);
        foreach(string field in new[]{"useSweep","sweepColor","sweepIntensity","sweepWidth","sweepSoftness","sweepTilt","sweepDuration","sweepInterval","reverseSweep"})EditorGUILayout.PropertyField(draft.FindPropertyRelative(field));
        EditorGUILayout.Space();EditorGUILayout.LabelField("하단 표시의 상태 전환 검수",EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("autoSwitch"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("switchInterval"));
        bool changed=serializedObject.ApplyModifiedProperties();
        using(new EditorGUILayout.HorizontalScope())
        {
            if(GUILayout.Button("빨간 확정 대상으로 전환")){Undo.RecordObject(workbench,"확정 대상 검수");workbench.autoSwitch=false;workbench.transitionState=BattleCellVisualState.ConfirmedArea;changed=true;}
            if(GUILayout.Button("선택 불가로 복귀")){Undo.RecordObject(workbench,"기본 움직임 복귀 검수");workbench.autoSwitch=false;workbench.transitionState=BattleCellVisualState.Unavailable;changed=true;}
        }
        EditorGUILayout.Space();
        if(GUILayout.Button(new GUIContent("현재 외형값으로 모든 테두리 통일","현재 상태의 크기·여백·곡률·분할수·각 선 폭·세로 두께·부유 높이만 복사합니다. 색상·채움·선 토글·스윕 허용은 보존합니다.")))
        {Undo.RecordObject(workbench,"테두리 외형 통일");workbench.UnifyShape();changed=true;}
        using(new EditorGUILayout.HorizontalScope())
        {
            if(GUILayout.Button("프로필 불러오기")){Undo.RecordObject(workbench,"테두리 프로필 불러오기");workbench.LoadProfile();changed=true;}
            if(GUILayout.Button(new GUIContent("프로필 저장","현재 초안을 연결 프로필에 저장합니다. 씬이나 다른 에셋을 저장하지 않습니다.")))Save(workbench);
        }
        if(!Application.isPlaying && BattleCellBorderPlayDraft.TryRead(workbench,out var remembered) && GUILayout.Button("마지막 플레이 초안 가져오기"))
        {Undo.RecordObject(workbench,"테두리 플레이 초안 가져오기");workbench.draft=remembered;changed=true;}
        if(workbench.profile!=null)EditorGUILayout.LabelField(JsonUtility.ToJson(workbench.draft)==JsonUtility.ToJson(workbench.profile.settings)?"프로필과 동일":"● 프로필과 다른 초안",EditorStyles.miniLabel);
        if(changed){BattleCellBorderPlayDraft.Remember(workbench);EditorUtility.SetDirty(workbench);if(!Application.isPlaying)EditorSceneManager.MarkSceneDirty(workbench.gameObject.scene);SceneView.RepaintAll();}
    }
    private static void Draw(SerializedProperty parent,string field,string label)
    {
        var p=parent.FindPropertyRelative(field);EditorGUILayout.PropertyField(p,new GUIContent(label,p.tooltip),true);
    }
    public static void Save(BattleCellBorderWorkbench workbench)
    {
        if(workbench.profile==null)
        {
            string path=EditorUtility.SaveFilePanelInProject("테두리 프로필 저장","BattleCellBorderProfile","asset","프로필 저장 위치를 선택하세요.");
            if(string.IsNullOrEmpty(path))return;
            Undo.RecordObject(workbench,"테두리 프로필 연결");workbench.profile=CreateInstance<BattleCellBorderProfile>();AssetDatabase.CreateAsset(workbench.profile,path);
            EditorUtility.SetDirty(workbench);
            if(!Application.isPlaying)EditorSceneManager.MarkSceneDirty(workbench.gameObject.scene);
        }
        Undo.RecordObject(workbench.profile,"전투 테두리 프로필 저장");
        workbench.profile.settings=workbench.draft.Copy();EditorUtility.SetDirty(workbench.profile);AssetDatabase.SaveAssetIfDirty(workbench.profile);
        BattleCellBorderPlayDraft.Remember(workbench);
    }
}

[InitializeOnLoad]
public static class BattleCellBorderPlayDraft
{
    static BattleCellBorderPlayDraft()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(state!=PlayModeStateChange.ExitingPlayMode)return;
            foreach(var wb in Object.FindObjectsByType<BattleCellBorderWorkbench>(FindObjectsInactive.Include,FindObjectsSortMode.None))Remember(wb);
        };
    }
    private static string Key(BattleCellBorderWorkbench wb)=>"JC.BattleBorder.Draft:"+wb.gameObject.scene.path+":"+wb.name;
    public static void Remember(BattleCellBorderWorkbench wb)=>SessionState.SetString(Key(wb),JsonUtility.ToJson(wb.draft));
    public static bool TryRead(BattleCellBorderWorkbench wb,out BattleCellBorderConfiguration config)
    {
        string value=SessionState.GetString(Key(wb),"");config=string.IsNullOrEmpty(value)?null:JsonUtility.FromJson<BattleCellBorderConfiguration>(value);return config!=null;
    }
}
