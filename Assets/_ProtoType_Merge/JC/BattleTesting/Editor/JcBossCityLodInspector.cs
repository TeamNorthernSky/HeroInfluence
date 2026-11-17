using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-17 | 에셋/씬 양쪽 인스펙터에서 같은 초안을 조절합니다. 생성은 프레임별로 나눕니다.
    [InitializeOnLoad]
    public static class JcBossCityLodEditing
    {
        sealed class Session
        {
            public JcBossBattleBackground source;
            public JcBossCityLodSettings draft, generated;
            public Dictionary<Mesh,Mesh> previews=new();
            public List<(Mesh source,Mesh target,float strength)> pending;
            public int next;
            public bool save;
            public double due;
            public string status;
            public bool Busy => pending!=null;
        }
        static readonly Dictionary<JcBossBattleBackground,Session> sessions=new();
        static JcBossCityLodEditing()
        {
            EditorApplication.update+=Tick;
            AssemblyReloadEvents.beforeAssemblyReload+=Clear;
            EditorApplication.quitting+=Clear;
            EditorApplication.playModeStateChanged+=state=> {
                if(state==PlayModeStateChange.ExitingEditMode || state==PlayModeStateChange.ExitingPlayMode)Clear();
            };
        }
        static Session Get(JcBossBattleBackground source)
        {
            if(!sessions.TryGetValue(source,out var s))sessions.Add(source,s=new Session{source=source,draft=source.cityLod.Copy()});
            return s;
        }
        static IEnumerable<JcBossCityLighting> Controls(Session s) => Resources.FindObjectsOfTypeAll<JcBossCityLighting>()
            .Where(c=>c && c.gameObject.scene.IsValid() && c.OptimizationSource==s.source);
        static bool SameStrength(JcBossCityLodSettings a,JcBossCityLodSettings b) =>
            a!=null && b!=null && Mathf.Approximately(a.mediumSimplification,b.mediumSimplification) && Mathf.Approximately(a.lowSimplification,b.lowSimplification);
        static bool MatchesBaked(Session s) => Mathf.Approximately(s.draft.mediumSimplification,s.source.cityLodLibrary.bakedMediumSimplification) &&
            Mathf.Approximately(s.draft.lowSimplification,s.source.cityLodLibrary.bakedLowSimplification);
        static void Apply(Session s)
        {
            foreach(var c in Controls(s)){c.SetLodDraft(s.draft);c.SetLodMeshPreview(!s.Busy && SameStrength(s.generated,s.draft)?s.previews:null);}
            EditorApplication.QueuePlayerLoopUpdate();SceneView.RepaintAll();
        }
        static void Release(Session s)
        {
            foreach(var c in Controls(s))c.SetLodMeshPreview(null);
            foreach(var mesh in s.previews.Values)if(mesh)Object.DestroyImmediate(mesh);
            s.previews.Clear();s.generated=null;s.pending=null;s.save=false;
        }
        public static void Reload(JcBossBattleBackground source)
        {
            var s=Get(source);Release(s);s.draft=source.cityLod.Copy();s.status="저장값을 다시 적용했습니다.";Apply(s);
        }
        // JC | 2026-11-17 | 자동 검증과 인스펙터가 동일한 미리보기/저장 경로를 사용합니다.
        public static void Preview(JcBossBattleBackground source,JcBossCityLodSettings value,bool save)
        {
            var s=Get(source);s.draft=value.Copy();s.draft.Validate();
            if(!source.cityLodLibrary)throw new InvalidOperationException("LOD 라이브러리가 연결되지 않았습니다.");
            if(MatchesBaked(s)){Release(s);Apply(s);if(save)Save(s);return;}
            if(!s.Busy && SameStrength(s.generated,s.draft)){Apply(s);if(save)Save(s);return;}
            // JC | 2026-11-17 | 계산 중 저장을 눌러도 처음부터 다시 계산하지 않고 완료 후 저장합니다.
            if(s.Busy && SameStrength(s.generated,s.draft)){s.save|=save;Apply(s);return;}
            Release(s);s.save=save;s.generated=s.draft.Copy();s.next=0;s.due=EditorApplication.timeSinceStartup+.35;
            s.pending=new();
            foreach(var entry in source.cityLodLibrary.entries)foreach(var part in entry.parts) {
                if(!part.high || !part.medium || !part.low)throw new InvalidOperationException("LOD 메시 연결이 누락되었습니다.");
                s.pending.Add((part.high,part.medium,s.generated.mediumSimplification));
                s.pending.Add((part.high,part.low,s.generated.lowSimplification));
            }
            s.status="메시 미리보기 계산 대기";Apply(s);
        }
        public static bool IsBusy(JcBossBattleBackground source)=>sessions.TryGetValue(source,out var s)&&s.Busy;
        // JC | 2026-11-17 | 씬 저장으로 미리보기를 재생성해도 아직 저장하지 않은 LOD 초안을 유지합니다.
        public static void RestorePreview(JcBossCityLighting control)
        {
            if(control && control.OptimizationSource && sessions.TryGetValue(control.OptimizationSource,out var s))Apply(s);
        }
        public static string Status(JcBossBattleBackground source)=>Get(source).status;
        static void Tick()
        {
            if(EditorApplication.isCompiling || EditorApplication.isUpdating)return;
            foreach(var s in sessions.Values.ToArray())if(s.source && s.Busy && EditorApplication.timeSinceStartup>=s.due) {
                try {
                    // 한 메시씩 처리하므로 수백 개 메시 전체를 하나의 Hold On 작업으로 묶지 않습니다.
                    if(s.next<s.pending.Count) {
                        var item=s.pending[s.next++];var mesh=JcBossCityLodBuilder.Simplify(item.source,item.strength,item.target.name);
                        mesh.hideFlags=HideFlags.HideAndDontSave;s.previews.Add(item.target,mesh);
                        s.status=$"메시 미리보기 계산 {s.next}/{s.pending.Count}";
                    }
                    if(s.next==s.pending.Count){s.pending=null;Apply(s);s.status="메시 미리보기 적용 완료 — 아직 저장되지 않았습니다.";if(s.save)Save(s);}
                }catch(Exception error){Release(s);s.status="처리 실패: "+error.Message;Debug.LogException(error);}
            }
        }
        static void Save(Session s)
        {
            // JC | 2026-11-17 | 생성 메시의 GUID·경로를 유지하고 완성된 초안만 저장합니다. 씬은 저장하지 않습니다.
            if(s.Busy)throw new InvalidOperationException("메시 계산이 끝나지 않았습니다.");
            if(!MatchesBaked(s) && !SameStrength(s.generated,s.draft))throw new InvalidOperationException("현재 강도의 메시 미리보기가 필요합니다.");
            foreach(var c in Controls(s))c.SetLodMeshPreview(null);
            AssetDatabase.StartAssetEditing();
            try {
                foreach(var pair in s.previews){EditorUtility.CopySerialized(pair.Value,pair.Key);pair.Key.hideFlags=HideFlags.None;EditorUtility.SetDirty(pair.Key);AssetDatabase.SaveAssetIfDirty(pair.Key);}
                var lib=s.source.cityLodLibrary;
                lib.bakedMediumSimplification=s.draft.mediumSimplification;lib.bakedLowSimplification=s.draft.lowSimplification;
                EditorUtility.SetDirty(lib);AssetDatabase.SaveAssetIfDirty(lib);
                s.source.cityLod=s.draft.Copy();EditorUtility.SetDirty(s.source);AssetDatabase.SaveAssetIfDirty(s.source);
            }finally{AssetDatabase.StopAssetEditing();}
            Release(s);Apply(s);s.status="LOD 설정과 생성 메시를 저장했습니다.";
        }
        static void Clear(){foreach(var s in sessions.Values)Release(s);sessions.Clear();}
        public static void Draw(JcBossBattleBackground source)
        {
            if(!source)return;var s=Get(source);
            EditorGUILayout.LabelField("원경 LOD 조절 · 저장",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("전환 기준은 화면 높이 비율입니다. 높일수록 단순한 메시로 빨리 전환합니다. 단순화 강도는 정점 축약 폭이며 삭제 비율이 아닙니다. 값을 바꾸면 나눠 계산하여 미리보고, 현재 값 저장으로 본게임·테스트 공통 프로필에 보존합니다.",MessageType.Info);
            using(new EditorGUI.DisabledScope(true))EditorGUILayout.ObjectField("저장 프로필",source,typeof(JcBossBattleBackground),false);
            EditorGUI.BeginChangeCheck();
            s.draft.highScreenHeight=EditorGUILayout.Slider("LOD 0 → 1 화면 비율",s.draft.highScreenHeight,.02f,.95f);
            s.draft.mediumScreenHeight=EditorGUILayout.Slider("LOD 1 → 2 화면 비율",s.draft.mediumScreenHeight,.001f,s.draft.highScreenHeight-.001f);
            var old=s.draft.Copy();
            s.draft.mediumSimplification=EditorGUILayout.Slider("LOD 1 단순화 강도",s.draft.mediumSimplification,0,.1f);
            s.draft.lowSimplification=EditorGUILayout.Slider("LOD 2 단순화 강도",s.draft.lowSimplification,s.draft.mediumSimplification,.1f);
            if(EditorGUI.EndChangeCheck()) {s.draft.Validate();if(!SameStrength(old,s.draft))Preview(source,s.draft,false);else Apply(s);}
            using(new EditorGUI.DisabledScope(!source.cityLodLibrary)) {
                if(GUILayout.Button("현재 값 저장 · 메시 포함"))Preview(source,s.draft,true);
                if(GUILayout.Button("저장값 다시 적용"))Reload(source);
            }
            if(!string.IsNullOrEmpty(s.status))EditorGUILayout.HelpBox(s.status,MessageType.Info);
        }
    }
    // JC | 2026-11-17 | 에디트 미리보기 및 플레이 원경 제어기의 같은 위치에서 조절합니다.
    [CustomEditor(typeof(JcBossCityLighting))]
    public sealed class JcBossCityLightingEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI(){var c=(JcBossCityLighting)target;JcBossCityLodEditing.Draw(c.OptimizationSource);if(c.OptimizationSource&&JcBossCityLodEditing.IsBusy(c.OptimizationSource))Repaint();}
    }
    [CustomEditor(typeof(JcBossBattleBackground))]
    public sealed class JcBossBattleBackgroundEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();DrawPropertiesExcluding(serializedObject,"m_Script","cityLod");serializedObject.ApplyModifiedProperties();
            var source=(JcBossBattleBackground)target;JcBossCityLodEditing.Draw(source);if(JcBossCityLodEditing.IsBusy(source))Repaint();
        }
    }
}
