using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-05 | 런타임 초안의 즉시 반영과 명시적 저장을 분리하여 공용 에셋의 실수 수정을 방지합니다.
    [CustomEditor(typeof(JcBossEnvironment))]
    public sealed class JcBossEnvironmentEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var controller = (JcBossEnvironment)target;
            if (!controller.Draft) { EditorGUILayout.HelpBox("BE490 보스전 진입 후 초기화됩니다.", MessageType.Info); return; }
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("저장 대상", controller.Source, typeof(JcBossEnvironmentProfile), false);
                EditorGUILayout.ObjectField("보스 무대", controller.Stage, typeof(Transform), true);
            }
            // JC | 2026-11-05 | 원경은 월드 시간대가 소유하므로 이전 환경 보정 항목이 적용되는 것처럼 표시하지 않습니다.
            bool worldCity = JcBossCityLighting.IsActive(controller.gameObject.scene);
            EditorGUILayout.HelpBox("테스트와 실제 BE490이 같은 프로필을 사용합니다. 플레이 종료 전에 저장해야 유지됩니다. 실내·캐릭터는 창문광과 추가 라이트만 받습니다. 원경은 월드의 4개 시간대 조명을 사용합니다.", MessageType.Info);
            var serialized = new SerializedObject(controller.Draft);
            serialized.Update();
            // JC | 2026-11-05 | 조명 차단과 창문광도 기존 즉시 반영/명시적 프로필 저장 흐름을 사용합니다.
            foreach (string field in new[] { "isolateInterior", "windows", "directionalIntensity", "directionalTint", "shadowStrength", "ambientIntensity", "ambientTint", "reflectionIntensity", "lights", "materials" })
                using(new EditorGUI.DisabledScope(worldCity && field!="isolateInterior" && field!="windows" && field!="lights" && field!="materials"))
                    EditorGUILayout.PropertyField(serialized.FindProperty(field), true);
            if (serialized.ApplyModifiedProperties()) controller.ApplyDraft();

            // JC | 2026-11-05 | 씬 참조는 프로필에 저장하지 않고 무대 상대 경로로 바꿔 양쪽 진입 경로에서 재사용합니다.
            var renderer = (Renderer)EditorGUILayout.ObjectField("셰이딩 대상 추가", null, typeof(Renderer), true);
            if (renderer)
            {
                if (renderer.transform == controller.Stage || renderer.transform.IsChildOf(controller.Stage))
                {
                    controller.Draft.materials.Add(new JcBossEnvironmentProfile.MaterialSlot {
                        rendererPath = AnimationUtility.CalculateTransformPath(renderer.transform, controller.Stage),
                        material = renderer.sharedMaterial, enabled = false
                    });
                    controller.ApplyDraft();
                }
                else EditorGUILayout.HelpBox("보스 무대 안의 렌더러만 사용할 수 있습니다.", MessageType.Warning);
            }
            if (!string.IsNullOrEmpty(controller.ValidationMessage)) EditorGUILayout.HelpBox(controller.ValidationMessage, MessageType.Warning);
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline && (controller.Draft.lights.Exists(x => x != null && x.enabled) || controller.Draft.windows.Exists(x => x != null && x.enabled)))
            {
                if (pipeline.additionalLightsRenderingMode != LightRenderingMode.PerPixel)
                    EditorGUILayout.HelpBox("현재 품질 설정은 추가 조명을 픽셀 단위로 처리하지 않습니다. 추가 조명/그림자가 제한될 수 있습니다. 공용 품질 설정은 자동 변경하지 않습니다.", MessageType.Warning);
                if (!pipeline.supportsAdditionalLightShadows && controller.Draft.lights.Exists(x => x != null && x.enabled && x.shadows != LightShadows.None))
                    EditorGUILayout.HelpBox("현재 품질 설정에서 추가 라이트 그림자가 꺼져 있습니다. 공용 품질 설정은 자동 변경하지 않습니다.", MessageType.Warning);
            }
            if (GUILayout.Button("현재 값을 프로필에 저장"))
            {
                Undo.RecordObject(controller.Source, "보스전 환경 프로필 저장");
                EditorUtility.CopySerialized(controller.Draft, controller.Source);
                controller.Source.name = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(controller.Source));
                controller.Source.hideFlags = HideFlags.None;
                EditorUtility.SetDirty(controller.Source); AssetDatabase.SaveAssetIfDirty(controller.Source);
            }
            if (GUILayout.Button("저장값 다시 적용")) controller.ReloadProfile();
            // JC | 2026-11-05 | 별도 재질 에셋을 편집한 경우에도 현재 초안을 잃지 않고 재질 복제본을 갱신합니다.
            if (GUILayout.Button("현재 초안 다시 적용 · 재질 갱신")) controller.ApplyDraft();
            EditorGUILayout.HelpBox("추가 조명의 위치·회전은 이 인스펙터의 목록에서 편집하세요. 생성된 하위 Light를 직접 바꾼 값은 프로필에 저장되지 않습니다. 셰이딩 재질은 보스전 전용 에셋을 지정하세요.", MessageType.None);
        }
    }
}
