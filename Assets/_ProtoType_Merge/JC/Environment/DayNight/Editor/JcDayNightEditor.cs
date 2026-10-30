using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace JC.Environment.Editor {
 [InitializeOnLoad] static class JcDayNightEditorSetup {
  static JcDayNightEditorSetup(){EditorApplication.delayCall+=Ensure;EditorSceneManager.sceneOpened+=(s,m)=>EditorApplication.delayCall+=Ensure;}
  static void Ensure(){if(!EditorApplication.isPlayingOrWillChangePlaymode){var c=JcDayNightController.Ensure(SceneManager.GetActiveScene());if(c!=null)c.EnsureSegmentObjects();}}
 }
 [CustomEditor(typeof(JcDayNightController))] sealed class JcDayNightInspector : UnityEditor.Editor {
  bool common;
  public override void OnInspectorGUI(){
   DrawDefaultInspector();var c=(JcDayNightController)target;
   EditorGUILayout.HelpBox("하위 구간 1~4를 선택해 조명·색감·반사를 편집합니다. 프로필 에셋에 저장되므로 플레이 종료 후에도 값이 유지됩니다.",MessageType.Info);
   if(c.profile==null)return;
   if(GUILayout.Button("전체 구간 초기화")){Undo.RecordObject(c.profile,"전체 구간 초기화");c.profile.ResetAllSegments();EditorUtility.SetDirty(c.profile);}
   common=EditorGUILayout.Foldout(common,"공통 전환·가독성·창문 설정");
   if(common){var so=new SerializedObject(c.profile);so.Update();foreach(var name in new[]{"transitionSeconds","sunriseHour","sunsetHour","twilightHalfWidth","twilightDurationMultiplier","cloudSpeedMultiplier","disabledUIAlpha","nightObjectAmbientBoost","indoorNightObjectAmbientBoost","windowOnHour","windowOffHour","windowFadeSeconds","windowTint","windowIntensity"})EditorGUILayout.PropertyField(so.FindProperty(name),true);if(so.ApplyModifiedProperties())c.profile.NotifyChanged();}
   if(!Application.isPlaying&&GUILayout.Button("미리보기 복원")){c.preview=false;c.Cancel();}
  }
 }
 [CustomEditor(typeof(JcDayNightSegment))] sealed class JcDayNightSegmentInspector : UnityEditor.Editor {
  public override void OnInspectorGUI(){
   var slot=(JcDayNightSegment)target;var c=slot.controller;var p=c!=null?c.profile:null;
   if(p==null){EditorGUILayout.HelpBox("상위 JC_DayNight에 프로필을 연결하세요.",MessageType.Warning);return;}
   p.EnsureSegments();EditorGUILayout.LabelField("구간 "+(slot.index+1),EditorStyles.boldLabel);
   EditorGUILayout.HelpBox("다음 구간까지 실제 시간 간격으로 보간합니다. 프로필 변경은 즉시 반영되며 플레이 종료 후에도 유지됩니다.",MessageType.Info);
   var so=new SerializedObject(p);so.Update();var item=so.FindProperty("segments").GetArrayElementAtIndex(slot.index);
   string[] fields={"hour","lightColor","lightIntensity","shadowMultiplier","sky","horizon","ground","ambientMultiplier","reflectionTint","reflectionMultiplier","waterMultiplier","partyFillEnabled","characterFill","npcFillEnabled","npcFill","enemyFillEnabled","enemyFill","screenColorEnabled","screenColorStrength","screenColorFilter","screenSaturation","screenContrast","screenExposure"};
   string[] labels={"기준 시각","방향광 색상","방향광 강도","그림자 강도 배율","하늘 상부 색상","수평선·안개 색상","지면 방향 색상","물체 환경광 배율","환경 반사 색상","환경 반사 강도 배율","수면 밝기 배율","파티 밝기 보정 사용","파티 보조 밝기","NPC 밝기 보정 사용","NPC 보조 밝기","필드 적 밝기 보정 사용","필드 적 보조 밝기","화면 색보정 사용","색보정 적용 강도","화면 색상 필터","화면 채도","화면 대비","화면 노출"};
   for(int i=0;i<fields.Length;i++)EditorGUILayout.PropertyField(item.FindPropertyRelative(fields[i]),new GUIContent(labels[i],item.FindPropertyRelative(fields[i]).tooltip));
   if(so.ApplyModifiedProperties())p.NotifyChanged();
   if(!p.ValidSegmentTimes())EditorGUILayout.HelpBox("구간 1→2→3→4→1이 중복 없이 24시간을 한 번 순환하도록 설정하세요. 유효하지 않은 동안에는 기본 시각 순서를 사용합니다.",MessageType.Error);
   if(GUILayout.Button("이 구간 초기화")){Undo.RecordObject(p,"구간 초기화");p.ResetSegment(slot.index);EditorUtility.SetDirty(p);}
   if(!Application.isPlaying&&GUILayout.Button("이 구간 미리보기")){c.preview=true;c.previewHour=p.segments[slot.index].hour;c.ApplyHour(c.previewHour);}
  }
 }
 [CustomEditor(typeof(JcDayNightProfile))] sealed class JcDayNightProfileInspector : UnityEditor.Editor {
  public override void OnInspectorGUI(){EditorGUILayout.HelpBox("JC_DayNight의 하위 구간 오브젝트에서 설정하세요. 이 에셋은 네 구간의 저장소입니다.",MessageType.Info);}
 }
}
