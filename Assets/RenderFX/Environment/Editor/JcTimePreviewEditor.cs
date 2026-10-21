using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace JC.Env.EditorTools
{
    // 미리보기 상태가 씬 저장·도메인 리로드·플레이 전환에 섞이지 않도록 원복합니다.
    [InitializeOnLoad]
    internal static class JcTimePreviewEditor
    {
        static JcEnvironmentController owner;
        static bool animating;
        static double started;
        static float from,duration;
        static JcTimePreviewEditor()
        {
            EditorApplication.update+=Tick;
            AssemblyReloadEvents.beforeAssemblyReload+=Restore;
            EditorApplication.playModeStateChanged+=_=>Restore();
            EditorSceneManager.sceneSaving+=OnSaving;
            EditorApplication.quitting+=Restore;
        }
        static void OnSaving(Scene scene,string path){if(owner!=null&&owner.gameObject.scene==scene)Restore();}
        static void Restore(){animating=false;if(owner!=null)owner.StopTimePreview();owner=null;}
        static void Tick()
        {
            if(owner==null||!owner.IsTimePreviewActive){animating=false;return;}
            if(!animating)return;
            float t=Mathf.Clamp01((float)(EditorApplication.timeSinceStartup-started)/duration);
            owner.SetPreviewHour(from+6*Mathf.SmoothStep(0,1,t),t>=1);
            if(t>=1)animating=false;
            EditorApplication.QueuePlayerLoopUpdate();SceneView.RepaintAll();
        }
        static void Set(JcEnvironmentController c,float h)
        {
            if(owner!=null&&owner!=c)Restore();owner=c;animating=false;c.SetPreviewHour(h);EditorApplication.QueuePlayerLoopUpdate();SceneView.RepaintAll();
        }
        internal static bool Draw(JcEnvironmentController c)
        {
            EditorGUILayout.Space();EditorGUILayout.LabelField("시간대 수동 미리보기 — 턴 미연결",EditorStyles.boldLabel);
            if(c.TimePreviewSettings==null){EditorGUILayout.HelpBox("시간대 미리보기 설정을 연결하면 사용 가능합니다.",MessageType.Info);return false;}
            if(!c.IsTimePreviewActive)
            {
                if(GUILayout.Button(c.TimePreviewSettings.startHour.ToString("00")+":00 미리보기 시작"))Set(c,c.TimePreviewSettings.startHour);
                return c.IsTimePreviewActive;
            }
            EditorGUILayout.HelpBox("임시 재질과 조명을 사용 중입니다. 종료하면 시작 전 상태로 복원합니다. 씬 저장·컴파일·플레이 전환 시에도 자동 종료합니다.",MessageType.Info);
            using(new EditorGUI.DisabledScope(animating))
            {
                EditorGUI.BeginChangeCheck();float h=EditorGUILayout.Slider(new GUIContent("시각 (24시간)","시간을 이동해 하늘·그림자·라이팅을 함께 비교합니다."),c.PreviewHour,0,24);
                if(EditorGUI.EndChangeCheck())Set(c,h);
                using(new EditorGUILayout.HorizontalScope())foreach(int hour in new[]{6,12,18,0})if(GUILayout.Button(hour.ToString("00")+":00"))Set(c,hour);
                if(GUILayout.Button("다음 6시간 재생")){owner=c;from=c.PreviewHour;started=EditorApplication.timeSinceStartup;duration=Mathf.Max(.1f,c.TimePreviewSettings.transitionSeconds);animating=true;}
            }
            if(animating&&GUILayout.Button("재생 일시정지"))animating=false;
            if(GUILayout.Button("미리보기 종료 — 원래 라이팅 복원")){Restore();EditorApplication.QueuePlayerLoopUpdate();SceneView.RepaintAll();}
            return c.IsTimePreviewActive;
        }
    }
}
