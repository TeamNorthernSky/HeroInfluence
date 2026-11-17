using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using JC.Environment;
namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-17 | 테스트 씬의 실내 조절기를 에디트 모드에도 제공하되 임시 환경은 저장/Play 전에 반드시 복원합니다.
    [InitializeOnLoad]
    public static class JcIndoorEditPreview
    {
        public static JcIndoorEnvironment Control { get; private set; }
        static GameObject root;
        static JcBossCityLighting city;
        static JcDayNightController oldClock, previewClock;
        static string oldClockJson;
        static double oldHour;
        static Scene scene;
        static string draftScene;
        static Dictionary<Object,Object> snapshots;
        static readonly Dictionary<JcBossCityTime,bool> cityModes=new Dictionary<JcBossCityTime,bool>();
        static readonly Dictionary<Renderer,MaterialPropertyBlock[]> blocks=new Dictionary<Renderer,MaterialPropertyBlock[]>();
        static bool busy,saving,failed;
        static double nextUpdate;
        static JcIndoorEditPreview()
        {
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting += Shutdown;
            EditorApplication.playModeStateChanged += state => {
                if(state==PlayModeStateChange.ExitingEditMode) Shutdown();
                if(state==PlayModeStateChange.EnteredEditMode) { failed=false;nextUpdate=EditorApplication.timeSinceStartup+.3; }
            };
            EditorSceneManager.sceneSaving += (target,path) => { if(target==scene) { saving=true;Stop(true); } };
            // JC | 2026-11-17 | 별도 프리팹 저장 이벤트가 실내 씬의 미리보기 중지 상태를 해제하지 않게 합니다.
            EditorSceneManager.sceneSaved += target => { if(target==scene) { saving=false;failed=false; } };
            EditorSceneManager.sceneClosing += (target,removing) => { if(target==scene) Shutdown(); };
            // JC | 2026-11-17 | LoadPrefabContents의 임시 프리팹 씬은 현재 실내 미리보기 수명과 분리합니다.
            EditorSceneManager.activeSceneChangedInEditMode += (before,after) => {
                if(EditorSceneManager.IsPreviewScene(before)||EditorSceneManager.IsPreviewScene(after))return;
                Stop(true); failed=false;
            };
        }
        static void Tick()
        {
            if(busy||saving||failed||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(EditorApplication.timeSinceStartup<nextUpdate)return;
            nextUpdate=EditorApplication.timeSinceStartup+.3;
            if(Control) { MarkTemporary(); if(Control.Bank)Control.Bank.TickEditorPreview(); return; }
            var active=SceneManager.GetActiveScene();
            if(!active.IsValid()||!active.isLoaded||EditorSceneManager.IsPreviewScene(active))return;
            var test=JcStandaloneBattleTest.ForScene(active);
            if(!test||!test.stage||(test.scenario!=JcStandaloneBattleTest.Scenario.Boss&&test.scenario!=JcStandaloneBattleTest.Scenario.Bank))return;
            try { Start(test); }
            catch(System.Exception error) { failed=true;Stop(false);Debug.LogException(error); }
        }
        public static void Start(JcStandaloneBattleTest test)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Control||busy||!test||!test.stage)return;
            busy=true;
            try {
                scene=test.gameObject.scene;
                if(draftScene!=scene.path) { ClearSnapshots();draftScene=scene.path; }
                bool boss=test.scenario==JcStandaloneBattleTest.Scenario.Boss;
                var bossSettings=boss?JcBossBattleBackground.Load():null;
                var bankSettings=boss?null:JcBankBattleBackground.Load();
                int period=Mathf.Clamp(SessionState.GetInt("JC.IndoorEditPeriod."+scene.path,test.period),0,3);
                var clockProfile=boss?bossSettings.cityTimeProfile:JcDayNightController.CurrentProfile;
                clockProfile.EnsureSegments();
                root=new GameObject("JC_IndoorEnvironment (에디트)"){hideFlags=HideFlags.DontSave};
                SceneManager.MoveGameObjectToScene(root,scene);
                foreach(var time in test.stage.GetComponentsInChildren<JcBossCityTime>(true))cityModes[time]=time.worldLighting;
                foreach(var r in test.stage.GetComponentsInChildren<Renderer>(true)) {
                    var values=new MaterialPropertyBlock[r.sharedMaterials.Length+1];
                    values[0]=new MaterialPropertyBlock();r.GetPropertyBlock(values[0]);
                    for(int i=1;i<values.Length;i++){values[i]=new MaterialPropertyBlock();r.GetPropertyBlock(values[i],i-1);} blocks[r]=values;
                }
                oldClock=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<JcDayNightController>(true)).FirstOrDefault();
                oldHour=JcDayNightController.CurrentHours;oldClockJson=oldClock?JsonUtility.ToJson(oldClock):null;
                if(boss) {
                    city=root.AddComponent<JcBossCityLighting>();city.hideFlags=HideFlags.HideInInspector;
                    city.Initialize(test.stage.transform,bossSettings,clockProfile.segments[period].hour);
                    previewClock=city.Clock;
                    if(previewClock&&!oldClock)previewClock.gameObject.hideFlags=HideFlags.DontSave;
                }
                var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).FirstOrDefault(c=>c.CompareTag("MainCamera"));
                Control=root.AddComponent<JcIndoorEnvironment>();Control.Initialize(test.stage,camera,bankSettings,bossSettings);
                Control.SelectPeriod(period);Control.FollowTime=false;
                Control.RestoreEditorDrafts(snapshots);
                MarkTemporary();SceneView.RepaintAll();
            } finally { busy=false; }
        }
        public static void SelectPeriod(JcIndoorEnvironment control,int period)
        {
            control.SelectPeriod(period);
            if(control!=Control)return;
            if(city)city.SelectPeriod(period);
            control.ApplyDraft();MarkTemporary();
            SessionState.SetInt("JC.IndoorEditPeriod."+scene.path,period);
            EditorApplication.QueuePlayerLoopUpdate();SceneView.RepaintAll();
        }
        static void MarkTemporary()
        {
            // JC | 2026-11-17 | 초기화·광원 목록 변경으로 생성된 하위 오브젝트도 씬 에셋에 저장하지 않습니다.
            if(root) foreach(var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags|=HideFlags.DontSave;
        }
        public static void Stop(bool preserveDrafts)
        {
            if(Control&&preserveDrafts) { ClearSnapshots();snapshots=Control.CaptureEditorDrafts(); }
            try { if(Control)Control.ReleaseEditorPreview(); }
            finally {
                Control=null;
                try { if(city)JcIndoorEnvironment.EditorCleanup(city,"OnDestroy"); }
                finally {
                    city=null;
                    if(previewClock&&!oldClock)Object.DestroyImmediate(previewClock.gameObject);
                    if(oldClock) { JsonUtility.FromJsonOverwrite(oldClockJson,oldClock);oldClock.ApplyHour(oldHour); }
                    oldClock=null;previewClock=null;
                    if(root)Object.DestroyImmediate(root);root=null;
                    foreach(var p in cityModes)if(p.Key)p.Key.worldLighting=p.Value;cityModes.Clear();
                    foreach(var p in blocks)if(p.Key){p.Key.SetPropertyBlock(p.Value[0]);for(int i=1;i<p.Value.Length;i++)p.Key.SetPropertyBlock(p.Value[i],i-1);}blocks.Clear();
                }
            }
        }
        static void Shutdown(){Stop(false);ClearSnapshots();}
        static void ClearSnapshots(){if(snapshots!=null)foreach(var value in snapshots.Values)if(value)Object.DestroyImmediate(value);snapshots=null;}
    }
}
