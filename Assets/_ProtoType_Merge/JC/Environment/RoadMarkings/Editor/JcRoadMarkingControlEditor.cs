using UnityEditor;
using UnityEngine;
namespace JC.EnvironmentDetail.Editor
{
    [CustomEditor(typeof(JcRoadMarkingControl),true)]
    public sealed class JcRoadMarkingControlEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var control=(JcRoadMarkingControl)target;
            EditorGUILayout.HelpBox(control.IsBattle?"전투 월드 단위 · 횡단보도 최소 여백 = 빈 간격":"탐사 원본 대비 배율 · 크기 모두 1 · 기본 색상이면 원본 외형 유지",MessageType.Info);
            var previous=control.CurrentProfile;
            DrawDefaultInspector();
            if(previous!=control.CurrentProfile){Undo.RecordObject(control,"차선 프로필 적용");control.ApplyProfile();}
            EditorGUILayout.LabelField("적용 상태",control.Status);
            using(new EditorGUI.DisabledScope(control.CurrentProfile==null))
            {
                if(GUILayout.Button("프로필 적용")){Undo.RecordObject(control,"차선 프로필 적용");control.ApplyProfile();if(!Application.isPlaying)EditorUtility.SetDirty(control);}
                if(GUILayout.Button("현재 값을 프로필에 저장"))SaveCurrentProfile(control);
            }
            if(GUILayout.Button("새 프로필로 저장"))
            {
                string path=AssetDatabase.GenerateUniqueAssetPath("Assets/_ProtoType_Merge/JC/Environment/RoadMarkings/Profiles/"+control.gameObject.scene.name+(control.IsBattle?"_Battle":"_Explore")+".asset");
                SaveNewProfile(control,path);
            }
            EditorGUILayout.HelpBox("프로필 저장은 플레이 종료 후에도 유지됩니다. 새 프로필의 씬 연결은 편집 모드에서 지정하고 씬을 저장해 주세요. 씬은 자동 저장하지 않습니다.",MessageType.None);
        }
        public static void SaveCurrentProfile(JcRoadMarkingControl control)
        {
            var profile=control.CurrentProfile;
            if(profile==null||!EditorUtility.IsPersistent(profile))throw new System.InvalidOperationException("프로젝트에 저장된 프로필을 연결해 주세요.");
            Undo.RecordObject(profile,"차선 설정 프로필 저장");profile.Values=control.Values;profile.lineColor=control.lineColor;
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
            Debug.Log("[JC 차선] 현재 값을 프로필에 저장했습니다: "+AssetDatabase.GetAssetPath(profile),profile);
        }
        public static JcRoadMarkingProfile SaveNewProfile(JcRoadMarkingControl control,string path)
        {
            var profile=control.IsBattle?(JcRoadMarkingProfile)ScriptableObject.CreateInstance<JcBattleRoadProfile>():ScriptableObject.CreateInstance<JcExploreRoadProfile>();
            profile.Values=control.Values;profile.lineColor=control.lineColor;AssetDatabase.CreateAsset(profile,path);AssetDatabase.SaveAssetIfDirty(profile);
            Undo.RecordObject(control,"새 차선 프로필 연결");control.CurrentProfile=profile;
            if(!Application.isPlaying)EditorUtility.SetDirty(control);
            return profile;
        }
    }
}
