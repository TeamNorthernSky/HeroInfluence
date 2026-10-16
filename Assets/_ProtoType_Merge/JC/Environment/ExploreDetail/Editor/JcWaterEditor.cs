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
            {"directionalWaveMix","방향성 물결 혼합"},{"waveElongation","물결 늘어짐"},{"waveBendStrength","굴곡 강도"},{"waveBendSize","굴곡 크기 (m)"},{"waveBendSpeed","형태 변화 속도 (m/s)"},{"directionalWaveAngle","흐름 대비 각도"},
            {"fakeHighlightStrength","페이크 하이라이트 강도"},{"fakeHighlightColor","페이크 하이라이트 색"},{"fakeHighlightWidth","페이크 하이라이트 폭"},{"fakeHighlightTilt","페이크 물결 선택 기울기"},{"fakeHighlightShadow","페이크 그림자 영향"},
            {"surfaceOpacity","수면 불투명도"},{"depthAbsorption","수심 흡수"},{"basinDepth","수중 공간 깊이 (m)"},{"basinColor","수중 바닥 색"},
            {"underwaterCapsule","수중 캡슐 표시"},{"capsuleDepth","캡슐 잠김 깊이 (m)"},{"capsuleSpeed","캡슐 속도 (m/s)"},
            {"broadNormal","큰 물결 노멀맵"},{"detailNormal","잔물결 노멀맵"},
            {"broadNormalStrength","큰 물결 기울기"},{"detailNormalStrength","잔물결 기울기"},
            {"detailNormalSize","잔물결 크기 (m)"},{"rippleSpeed","잔물결 자체 속도 (m/s)"},
            {"baseColor","중앙 물색"},{"edgeColor","물가 색"},{"edgeColorWidth","물가색 범위 (m)"},
            {"edgeVariation","물가색 불규칙성"},{"edgeVariationSize","물가색 변화 크기 (m)"},
            {"waveShading","물결 음영 강도"},{"waveShadeSize","큰 물결 크기 (m)"},{"waveShadeSoftness","물결 음영 부드러움"},
            {"highlightStrength","하이라이트 강도"},{"highlightRoughness","하이라이트 폭 / 거칠기"},{"highlightSoftness","하이라이트 경계 부드러움"},{"sparkleStrength","잔반짝임 강도"},
            {"followCanal","수로 따라 흐름"},{"direction","공통 방향 (X/Z)"},{"flowSpeed","흐름 속도 (m/s)"},
            {"shadowStrength","그림자 영향"},{"reflection","반사 강도"},
            {"reflectionClarity","반사 선명도"},{"reflectionFresnel","시선 각도 영향"},
            {"reflectionDistortion","반사 물결 왜곡"},
            {"planarReflection","주변 풍경 평면 반사"},{"reflectionResolution","반사 해상도"},
            {"reflectionUpdateInterval","반사 갱신 간격"},{"reflectionLayers","반사 대상 레이어"}
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
            EditorGUILayout.LabelField("반사 촬영",$"카메라·수면 높이 조합 {c.ReflectionViewCount}개");
            if(c.RendererCount==0)EditorGUILayout.HelpBox("적용 중인 카툰 수면이 없습니다. 드레싱 베이크 상태와 Cartoon Water 설정을 확인하세요.",MessageType.Warning);
            EditorGUI.BeginChangeCheck();
            var settings=serializedObject.FindProperty("settings"); var child=settings.Copy(); var end=settings.GetEndProperty();
            if(child.NextVisible(true)) do
            {
                if(SerializedProperty.EqualContents(child,end))break;
                bool disabled=(child.name=="direction" && settings.FindPropertyRelative("followCanal").boolValue) ||
                    ((child.name=="reflectionResolution" || child.name=="reflectionUpdateInterval" || child.name=="reflectionLayers") &&
                     (!settings.FindPropertyRelative("planarReflection").boolValue || settings.FindPropertyRelative("reflection").floatValue<=0));
                using(new EditorGUI.DisabledScope(disabled))
                    EditorGUILayout.PropertyField(child,new GUIContent(Labels.TryGetValue(child.name,out var label)?label:child.displayName,child.tooltip),true);
            }while(child.NextVisible(false));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("paused"),new GUIContent("애니메이션 일시정지","물의 이동·요철를 함께 멈춥니다. 설정 저장에는 포함되지 않습니다."));
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
