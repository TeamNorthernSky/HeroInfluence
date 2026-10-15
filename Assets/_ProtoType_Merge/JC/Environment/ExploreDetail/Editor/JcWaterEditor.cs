using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace JC.EnvironmentDetail.Editor
{
    [CustomEditor(typeof(JcWaterController))]
    public sealed class JcWaterEditor : UnityEditor.Editor
    {
        static readonly Dictionary<string,string> Labels = new Dictionary<string,string>
        {
            {"baseColor","중앙 물색"},{"edgeColor","물가 색"},{"edgeColorWidth","물가색 범위 (m)"},
            {"waveShading","물결 음영 강도"},{"waveShadeSize","물결 음영 크기 (m)"},{"waveShadeSoftness","물결 음영 부드러움"},
            {"foamCurvature","포말 곡선 변형"},
            {"foamOpacity","포말 불투명도"},{"foamFade","포말 사라짐 부드러움"},
            {"foamColor","포말 색"},{"foamSize","포말 간격 (m)"},{"foamDensity","포말 밀도"},
            {"foamThickness","포말 두께"},{"foamLength","포말 길이 비율"},{"followCanal","수로 따라 흐름"},
            {"direction","공통 방향 (X/Z)"},{"flowSpeed","흐름 속도 (m/s)"},{"morphSpeed","형태 변화 속도"},
            {"morphAmount","형태 변화량"},{"shoreFoam","물가 포말 사용"},{"shoreWidth","물가 포말 폭 (m)"},
            {"shoreBreakup","물가 포말 끊김"},{"shoreSpeed","물가 포말 변화 속도"},
            {"shadowStrength","그림자 영향"},{"reflection","보조 반사"}
        };
        double nextRepaint;
        void OnEnable()=>EditorApplication.update+=AnimatePreview;
        void OnDisable()=>EditorApplication.update-=AnimatePreview;
        void AnimatePreview()
        {
            var c=target as JcWaterController;
            if(c==null || !c.isActiveAndEnabled || c.Paused || Application.isPlaying || EditorApplication.timeSinceStartup<nextRepaint)return;
            nextRepaint=EditorApplication.timeSinceStartup+1.0/30;
            EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
        }
        public override void OnInspectorGUI()
        {
            var c=(JcWaterController)target;
            serializedObject.Update();
            EditorGUILayout.HelpBox("변경 즉시 수면에 반영됩니다. 조절값은 임시이며 ‘현재 값 저장’을 눌러야 씬 재로드·플레이 종료 후에도 유지됩니다. 물 높이·범위·호안·하류 기준은 기존 드레싱에서 재베이크합니다.",MessageType.Info);
            using(new EditorGUI.DisabledScope(true))EditorGUILayout.ObjectField(new GUIContent("저장 프로필","씬 간 공유되는 물 설정 저장 에셋입니다."),c.Profile,typeof(JcWaterProfile),false);
            EditorGUILayout.LabelField("연결 상태",$"수면 렌더러 {c.RendererCount}개 / 재질 {c.MaterialCount}개");
            if(c.RendererCount==0)EditorGUILayout.HelpBox("적용 중인 카툰 수면이 없습니다. 드레싱 베이크 상태와 Cartoon Water 설정을 확인하세요.",MessageType.Warning);
            EditorGUI.BeginChangeCheck();
            var settings=serializedObject.FindProperty("settings"); var child=settings.Copy(); var end=settings.GetEndProperty();
            if(child.NextVisible(true)) do
            {
                if(SerializedProperty.EqualContents(child,end))break;
                bool disabled=(child.name=="direction" && settings.FindPropertyRelative("followCanal").boolValue) ||
                    (child.name.StartsWith("shore") && child.name!="shoreFoam" && !settings.FindPropertyRelative("shoreFoam").boolValue);
                using(new EditorGUI.DisabledScope(disabled))
                    EditorGUILayout.PropertyField(child,new GUIContent(Labels.TryGetValue(child.name,out var label)?label:child.displayName,child.tooltip),true);
            }while(child.NextVisible(false));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("paused"),new GUIContent("애니메이션 일시정지","물의 이동·형태 변화·물가 포말을 함께 멈춥니다. 설정 저장에는 포함되지 않습니다."));
            bool changed=EditorGUI.EndChangeCheck(); serializedObject.ApplyModifiedProperties();
            if(changed){c.Apply();EditorApplication.QueuePlayerLoopUpdate();SceneView.RepaintAll();}
            using(new EditorGUI.DisabledScope(c.Profile==null))
            {
                if(GUILayout.Button(new GUIContent("현재 값 저장","현재 물 조절값을 프로필에 저장합니다. 플레이 중에도 저장한 값은 종료 후 유지됩니다. 씬과 베이크는 저장하지 않습니다.")))
                {
                    Undo.RecordObject(c.Profile,"물 설정 저장"); c.Profile.settings=c.Settings.Copy();
                    EditorUtility.SetDirty(c.Profile);AssetDatabase.SaveAssetIfDirty(c.Profile);
                }
                if(GUILayout.Button(new GUIContent("저장값 불러오기","현재 임시 조절을 저장 프로필 값으로 되돌리고 즉시 적용합니다.")))
                {Undo.RecordObject(c,"물 설정 불러오기");c.LoadSaved();}
            }
        }
    }
}
