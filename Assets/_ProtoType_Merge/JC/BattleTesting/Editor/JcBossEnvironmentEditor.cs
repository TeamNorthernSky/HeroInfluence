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
            // JC | 2026-11-12 | 전용 IBL 추가 후에도 원경의 시간대와 실내 설정은 독립적임을 안내합니다.
            EditorGUILayout.HelpBox("테스트와 실제 BE490이 같은 프로필을 사용합니다. 편집/플레이 중 조절 후 저장해야 다음 실행에도 유지됩니다. 실내는 창문광·추가광과 선택한 실내 IBL을 받습니다. 원경은 월드의 4개 시간대 조명을 유지합니다.", MessageType.Info);
            var serialized = new SerializedObject(controller.Draft);
            serialized.Update();
            // JC | 2026-11-12 | 외부 도시 환경 잠금과 독립적으로 실내 IBL을 조절합니다.
            JcIndoorIblEditor.Draw(serialized,controller.Draft.ibl,false);
            // JC | 2026-11-18 | 네 시간대마다 대비/수광/반사를 독립 저장합니다. 공통 캐릭터 색상과 광원 자체 밝기는 변경하지 않습니다.
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("선택 시간대 · 율리아와 증폭기 몸체",EditorStyles.boldLabel);
            foreach(var field in new[]{"bossEmphasisEnabled","bossSpotReception","bossSpotReflection","bossContrast","bossContrastPivot"})
                EditorGUILayout.PropertyField(serialized.FindProperty(field));
            EditorGUILayout.HelpBox("현재 선택한 시간대의 실내 실제 조명에서 율리아·양옆 증폭기 몸체에만 적용됩니다. 수광 1 / 반사광 1은 기존 스팟 반응이며, 반사광 0에서도 확산광은 남습니다. 다른 광원·IBL·공통 채도·밝기·아군·배경·VFX는 그대로입니다. 사용을 끄면 세 보정을 함께 끄고 비교합니다.\nJC_IndoorEnvironment → 원하는 시간대 → 조명·환경에서 조절하고, 플레이 종료 전 ‘현재 값을 프로필에 저장’으로 보존하세요. 본게임과 테스트가 같은 시간대 프로필을 사용합니다. 자동 조준은 플레이 중 생성된 율리아 본체 중심을 사용합니다.",MessageType.Info);
            // JC | 2026-11-18 | 정면 조준 시 실제로 자동 계산되는 축과 프로필에서 조절하는 축을 구분해 안내합니다.
            EditorGUILayout.HelpBox("보스 정면 · 몸체 조준: 플레이 중 X는 율리아 몸통에 맞춰지고 회전은 자동 조준됩니다. 추가 라이트의 위치 Y로 높이, Z로 전방 거리를 조절하세요. 스폿 중심부 비율을 높이면 중심의 밝은 영역이 넓어지고 경계 전환 폭은 줄어듭니다.", MessageType.None);
            // JC | 2026-11-05 | 조명 차단과 창문광도 기존 즉시 반영/명시적 프로필 저장 흐름을 사용합니다.
            foreach (string field in new[] { "isolateInterior", "windows", "directionalIntensity", "directionalTint", "shadowStrength", "ambientIntensity", "ambientTint", "reflectionIntensity", "lights" })
                using(new EditorGUI.DisabledScope(worldCity && field!="isolateInterior" && field!="windows" && field!="lights"))
                    EditorGUILayout.PropertyField(serialized.FindProperty(field), true);
            if (serialized.ApplyModifiedProperties()) controller.ApplyDraft();

            // JC | 2026-11-09 | 도시 월드 조명 잠금과 독립된 보스 색 변화 설정도 같은 초안/저장 경로를 씁니다.
            serialized.Update();
            // JC | 2026-11-10 | 본게임/테스트 토글을 독립 저장하고 현재 전투에 적용되는 설정을 표시합니다.
            EditorGUILayout.HelpBox(controller.IsBossLightingTest
                ? "현재: 보스 테스트 설정 적용. 본게임 토글과 독립적으로 동작합니다."
                : "현재: 본게임 설정 적용. 행동 조명 효과를 꺼도 기본 조명은 유지됩니다.", MessageType.Info);
            foreach (string field in new[] { "battlePulseEnabled", "battlePulseInBossTestEnabled", "battlePulseColor", "battlePulseMix", "battlePulseSeconds", "battlePulseInterval" })
                EditorGUILayout.PropertyField(serialized.FindProperty(field));
            if (serialized.ApplyModifiedProperties()) controller.ApplyDraft();

            // JC | 2026-11-18 | 무대 재질 교체는 JC_Materials의 공통 표면 영역에서 조절합니다.
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
                // JC | 2026-11-17 | 시간대별 서브 에셋의 표시 이름을 저장 후에도 유지합니다.
                string sourceName=controller.Source.name;
                EditorUtility.CopySerialized(controller.Draft, controller.Source);
                controller.Source.name = sourceName;
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
