using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace JC.EnvironmentDetail.Editor
{
    [InitializeOnLoad]
    static class JcExploreDetailEditorBootstrap
    {
        static bool checkQueued;
        static readonly System.Collections.Generic.Dictionary<int,string> warnings = new System.Collections.Generic.Dictionary<int,string>();
        static JcExploreDetailEditorBootstrap()
        {
            JcExploreDetailOverlay.EditorDependencyCheck = JcExploreDetailBaker.CheckDependencies;
            EditorSceneManager.sceneOpened += Opened;
            EditorApplication.delayCall += ApplyLoaded;
            AssemblyReloadEvents.beforeAssemblyReload += RestoreAll;
            EditorApplication.projectChanged += QueueCheck;
            EditorApplication.hierarchyChanged += QueueCheck;
            Undo.undoRedoPerformed += QueueCheck;
            Undo.postprocessModifications += changes => { QueueCheck(); return changes; };
            EditorApplication.playModeStateChanged += state =>
            {
                if(state==PlayModeStateChange.EnteredEditMode)
                { JcExploreDetailOverlay.EditorDependencyCheck=JcExploreDetailBaker.CheckDependencies; EditorApplication.delayCall+=ApplyLoaded; }
            };
        }
        static void QueueCheck()
        {
            if(Application.isPlaying || EditorApplication.isCompiling || checkQueued) return;
            checkQueued=true;
            EditorApplication.delayCall+=CheckChangedInputs;
        }
        static void CheckChangedInputs()
        {
            checkQueued=false;
            if(Application.isPlaying || EditorApplication.isCompiling) return;
            foreach(var overlay in Resources.FindObjectsOfTypeAll<JcExploreDetailOverlay>())
            {
                if(!overlay.gameObject.scene.IsValid() || !overlay.isActiveAndEnabled || overlay.IsPreview) continue;
                var bake=JcExploreDetailBakeCatalog.Load()?.Find(overlay.gameObject.scene.path);
                if(bake==null) continue;
                string reason;
                try { reason=bake.Compare(JcExploreDetailBake.CaptureInputs(overlay.gameObject.scene)) ?? JcExploreDetailBaker.CheckDependencies(bake); }
                catch(System.Exception e) { reason="입력 구조 확인 실패: "+e.Message; }
                int id=overlay.GetInstanceID();
                if(reason==null) { warnings.Remove(id); continue; }
                if(!warnings.TryGetValue(id,out var previous) || previous!=reason)
                { warnings[id]=reason; Debug.LogWarning("[JC Bake] "+reason+" 자동 재생성하지 않습니다.",overlay); }
                if(!overlay.ForceBakedOnMismatch && overlay.VisualRoot!=null) overlay.ShowOriginal();
            }
        }
        static void Opened(Scene scene, OpenSceneMode mode)
        {
            if (Application.isPlaying) return;
            EditorApplication.delayCall += () => { if (scene.IsValid() && scene.isLoaded && !Application.isPlaying) JcExploreDetailOverlay.Ensure(scene)?.Refresh(); };
        }
        static void ApplyLoaded()
        {
            if (Application.isPlaying) return;
            for (int i = 0; i < SceneManager.sceneCount; i++) JcExploreDetailOverlay.Ensure(SceneManager.GetSceneAt(i))?.Refresh();
        }
        static void RestoreAll() { foreach (var c in Resources.FindObjectsOfTypeAll<JcExploreDetailOverlay>()) if (c.gameObject.scene.IsValid()) c.Restore(); }
        [MenuItem("JC/환경/탐사 외형 보강 설정")]
        static void SelectProfile(){Selection.activeObject=Resources.Load<JcExploreDetailProfile>("JC_ExploreDetail_Default");}
    }
    [CustomEditor(typeof(JcExploreDetailOverlay))]
    public sealed class JcExploreDetailOverlayEditor:UnityEditor.Editor
    {
        UnityEditor.Editor settingsEditor;
        bool showBoundaries,showWaterBoundaries;
        Vector2 waterScroll;
        Vector2 boundaryScroll;
        void OnDisable(){if(settingsEditor!=null)Object.DestroyImmediate(settingsEditor);}
        public override void OnInspectorGUI(){
            var c=(JcExploreDetailOverlay)target;
            EditorGUI.BeginChangeCheck(); DrawDefaultInspector();
            if(EditorGUI.EndChangeCheck()) c.Refresh();
            EditorGUILayout.HelpBox(c.Status ?? "미적용", MessageType.Info);
            if (GUILayout.Button(new GUIContent("원본 변경 검사", "현재 DH 입력과 확정본을 비교합니다. 외형을 재생성하지 않습니다.")))
                Debug.Log("[JC Bake] " + JcExploreDetailBaker.Check(c), c);
            using(new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if(GUILayout.Button(new GUIContent("드레싱 재생성 · 미리보기", "현재 설정으로 임시 외형을 만듭니다. 확정본은 보존합니다."))) JcExploreDetailBaker.Preview(c);
                using(new EditorGUI.DisabledScope(!c.IsPreview))
                    if(GUILayout.Button(new GUIContent("베이크 확정", "검토한 미리보기를 에셋으로 저장합니다. 직전 확정본은 복구용으로 보존하며 씬은 저장하지 않습니다."))) JcExploreDetailBaker.Commit(c);
                if(GUILayout.Button(new GUIContent("직전 확정본 복구", "직전 세대와 현재 세대를 교환합니다. 원본 불일치 정책은 그대로 적용됩니다."))) JcExploreDetailBaker.RestorePrevious(c);
            }
            if(GUILayout.Button(new GUIContent("확정 결과 표시 / 미리보기 취소", "저장된 결과를 적용합니다. 새 메시를 생성하지 않습니다."))) c.Refresh();
            if(GUILayout.Button(new GUIContent("DH 원본 표시", "드레싱을 제거하고 숨겨진 원본 렌더러를 복원합니다."))) c.ShowOriginal();
            if(c.profile!=null){EditorGUILayout.Space();EditorGUILayout.LabelField("월드 드레싱 · 탐사 지면 설정",EditorStyles.boldLabel);CreateCachedEditor(c.profile,null,ref settingsEditor);settingsEditor.OnInspectorGUI();}
            showWaterBoundaries=EditorGUILayout.Foldout(showWaterBoundaries,"수로 적용 위치 (월드 X/Z)",true);
            if(showWaterBoundaries){waterScroll=EditorGUILayout.BeginScrollView(waterScroll,GUILayout.MaxHeight(240));
            foreach(var b in c.WaterBoundaries){EditorGUILayout.BeginHorizontal();EditorGUILayout.LabelField($"{b.category}: ({b.position.x:F1}, {b.position.z:F1})");if(GUILayout.Button(new GUIContent("보기","수로 경계를 Scene 뷰에서 확인합니다."),GUILayout.Width(45))&&SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.Frame(new Bounds(b.position,Vector3.one*3),false);EditorGUILayout.EndHorizontal();}
            EditorGUILayout.EndScrollView();}
            showBoundaries=EditorGUILayout.Foldout(showBoundaries,"도로-풀밭 변경 위치 (월드 X/Z)",true);
            if(showBoundaries){boundaryScroll=EditorGUILayout.BeginScrollView(boundaryScroll,GUILayout.MaxHeight(300));foreach(var b in c.Boundaries){EditorGUILayout.BeginHorizontal();EditorGUILayout.LabelField($"{b.category}: ({b.position.x:F1}, {b.position.z:F1}) {b.tile}");if(GUILayout.Button(new GUIContent("보기","이 경계를 Scene 뷰 중앙에 표시합니다."),GUILayout.Width(45))&&SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.Frame(new Bounds(b.position,Vector3.one*2),false);EditorGUILayout.EndHorizontal();}EditorGUILayout.EndScrollView();}
        }
    }
    [CustomEditor(typeof(JcExploreDetailProfile))]
    public sealed class JcExploreDetailProfileEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI(){EditorGUILayout.HelpBox("설정은 생성용 초안입니다. 수정만으로 외형이 바뀌지 않습니다. 편집 모드에서 JC_ExploreDetail의 재생성 → 베이크 확정을 사용하세요. 런타임은 확정본만 사용합니다.",MessageType.Info);
            DrawDefaultInspector();
            if(GUILayout.Button(new GUIContent("흙길 타일 경계 다시 분석","DH 레지스트리의 ParkRoad 이미지에서 입구 폭과 방향을 읽습니다. 원본 텍스처는 변경하지 않습니다. 변경 후 설정 저장을 누르세요."))){JcPathEdgeAnalyzer.Analyze((JcExploreDetailProfile)target);}
            if(GUILayout.Button(new GUIContent("설정 저장","현재 탐사 외형 설정을 에셋에 저장합니다. 씬은 저장하지 않습니다."))){EditorUtility.SetDirty(target);AssetDatabase.SaveAssetIfDirty(target);}
        }
    }
}
