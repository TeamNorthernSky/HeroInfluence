using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace JC.Environment.Editor {
 [InitializeOnLoad] static class JcDayNightEditorSetup {
  static JcDayNightEditorSetup(){EditorApplication.delayCall+=Ensure;EditorSceneManager.sceneOpened+=(s,m)=>EditorApplication.delayCall+=Ensure;}
  static void Ensure(){if(!EditorApplication.isPlayingOrWillChangePlaymode)JcDayNightController.Ensure(SceneManager.GetActiveScene());}
 }
 [CustomEditor(typeof(JcDayNightController))] sealed class JcDayNightInspector : UnityEditor.Editor {
  UnityEditor.Editor profileEditor;
  public override void OnInspectorGUI(){
   DrawDefaultInspector();var c=(JcDayNightController)target;
   if(c.profile!=null){EditorGUILayout.Space();EditorGUILayout.LabelField("시간대 설정 (저장되는 에셋)",EditorStyles.boldLabel);CreateCachedEditor(c.profile,null,ref profileEditor);profileEditor.OnInspectorGUI();}
   if(!Application.isPlaying){EditorGUILayout.HelpBox("미리보기를 끄면 원래 조명과 반사가 복원됩니다. 게임에서는 현재 턴의 시각을 자동 적용합니다.",MessageType.Info);if(GUILayout.Button("미리보기 복원")){c.preview=false;c.Cancel();}}
  }
  void OnDisable(){if(profileEditor!=null)DestroyImmediate(profileEditor);}
 }
}
