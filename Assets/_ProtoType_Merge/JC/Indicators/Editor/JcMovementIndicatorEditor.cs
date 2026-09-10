using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace JC.Indicators.EditorTools
{
    [CustomEditor(typeof(JcMovementIndicatorController))]
    public sealed class JcMovementIndicatorEditor : Editor
    {
        private static readonly string[][] Groups =
        {
            new[] { "상태 색상", "reachableColor|이동 가능 색상", "unreachableColor|이동력 초과 색상", "opacity|불투명도" },
            new[] { "도착 문양", "markerSize|셀 대비 크기", "borderWidth|테두리 두께", "cornerRadius|모서리 반지름", "ringRadius|중앙 링 반지름", "ringWidth|중앙 링 두께", "dotRadius|중앙 점 반지름" },
            new[] { "마커 입체 형태", "markerThickness|입체 두께", "bevelWidth|베벨 폭", "curveSegments|곡선 분할 수", "sideBrightness|옆면 밝기" },
            new[] { "중심 수렴 하이라이트", "highlightColor|하이라이트 색상", "highlightColorCyclePeriod|색상 왕복 주기 (초)", "highlightStrength|하이라이트 강도", "waveWidth|파동 띠 폭", "waveSpeed|파동 속도", "wavePeriod|파동 주기 (초)" },
            new[] { "부유감", "floatHeight|공통 부유 높이", "markerHeightOffset|도착 문양 높이 오프셋", "bobAmplitude|흔들림 진폭", "bobFrequency|흔들림 횟수 / 초" },
            new[] { "그림자", "shadowColor|그림자 색상", "shadowOpacity|그림자 농도", "shadowDistance|그림자 거리", "shadowAngle|그림자 방향 (도)", "shadowSoftness|그림자 번짐" },
            new[] { "경로 점선", "lineWidth|선 굵기", "dashLength|점선 길이", "dashGap|점선 간격", "flowSpeed|플로우 속도" },
            new[] { "점선 트레일", "dashTrailLength|꼬리 길이", "dashTrailOpacity|꼬리 불투명도", "dashTrailFalloff|불투명도 감쇠", "dashTrailWidthFalloff|굵기 감쇠" },
            new[] { "점선 글로우", "dashGlowWidth|번짐 폭", "dashGlowStrength|번짐 강도" },
            new[] { "점선 플리커링", "dashFlickerStrength|흰색 반짝임 강도", "dashFlickerSpeed|패턴 진행 속도", "dashFlickerRiseSpeed|흰색 전환 속도 (배)", "dashFlickerFallSpeed|원색 복귀 속도 (배)", "dashFlickerDirection|진행 방향" }
        };

        public override void OnInspectorGUI()
        {
            var controller = (JcMovementIndicatorController)target;
            serializedObject.Update();
            Draw(serializedObject.FindProperty("profile"), "저장 프로파일");
            EditorGUILayout.HelpBox("아래 값은 실시간 조절용 초안입니다. 저장 프로파일을 바꾸려면 ‘캡처’ 후 ‘저장’을 누르세요. 플레이 종료 후에는 마지막 플레이 초안을 가져올 수 있습니다.", MessageType.None);
            var settings = serializedObject.FindProperty("settings");
            foreach (var group in Groups)
            {
                string key = "JC.Movement.Fold." + group[0];
                bool open = SessionState.GetBool(key, true);
                open = EditorGUILayout.Foldout(open, new GUIContent(group[0], "이 설정 묶음을 펼치거나 접습니다. 조절값은 변하지 않습니다."), true);
                SessionState.SetBool(key, open);
                if (!open) continue;
                EditorGUI.indentLevel++;
                for (int i = 1; i < group.Length; i++)
                {
                    var pair = group[i].Split('|');
                    Draw(settings.FindPropertyRelative(pair[0]), pair[1]);
                }
                EditorGUI.indentLevel--;
            }
            bool changed = serializedObject.ApplyModifiedProperties();
            if (changed) { if (Application.isPlaying) JcMovementPlayDraft.Remember(controller); SceneView.RepaintAll(); }
            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(controller.Profile == null))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("▶ 적용", "프로파일의 값으로 현재 초안을 되돌립니다. 프로파일이나 씬을 저장하지 않습니다.")))
                    {
                        Undo.RecordObject(controller, "이동 인디케이터 프로파일 적용"); controller.LoadProfile(); Mark(controller);
                    }
                    if (GUILayout.Button(new GUIContent("● 캡처", "현재 초안을 프로파일의 메모리 값으로 복사합니다. 이때부터 Ctrl+S 등 Unity 저장 동작에도 포함될 수 있습니다.")))
                    {
                        Undo.RecordObject(controller.Profile, "이동 인디케이터 초안 캡처");
                        controller.Profile.settings = controller.Settings; EditorUtility.SetDirty(controller.Profile);
                    }
                    if (GUILayout.Button(new GUIContent("저장", "캡처된 프로파일만 디스크에 저장합니다. 미캡처 초안과 현재 씬, 다른 에셋은 저장하지 않습니다.")))
                        AssetDatabase.SaveAssetIfDirty(controller.Profile);
                }
                if (controller.Profile != null)
                    EditorGUILayout.LabelField(JsonUtility.ToJson(controller.Settings) == JsonUtility.ToJson(controller.Profile.settings.Sanitized())
                        ? "프로파일과 동일" : "● 프로파일과 다른 초안", EditorStyles.miniLabel);
            }
            if (!Application.isPlaying && JcMovementPlayDraft.TryRead(controller, out var draft)
                && GUILayout.Button(new GUIContent("마지막 플레이 초안 가져오기", "마지막 플레이에서 조절한 값을 초안으로 가져옵니다. 디스크에 남기려면 캡처·저장하세요. 임시 값은 Unity 종료 시 사라집니다.")))
            {
                Undo.RecordObject(controller, "이동 인디케이터 플레이 초안 가져오기"); controller.Settings = draft; Mark(controller);
            }
            EditorGUILayout.Space();
            bool wiring = SessionState.GetBool("JC.Movement.Wiring", false);
            wiring = EditorGUILayout.Foldout(wiring, new GUIContent("씬 연결", "현재 씬의 경로·도착 표식·그리드·셰이더 연결을 확인합니다."), true);
            SessionState.SetBool("JC.Movement.Wiring", wiring);
            if (wiring)
            {
                serializedObject.Update();
                Draw(serializedObject.FindProperty("pathRenderer"), "경로 렌더러");
                Draw(serializedObject.FindProperty("destinationMarker"), "기존 도착 표식");
                Draw(serializedObject.FindProperty("gridManager"), "그리드");
                Draw(serializedObject.FindProperty("indicatorShader"), "문양 셰이더");
                serializedObject.ApplyModifiedProperties();
            }
            if (!controller.IsReady) EditorGUILayout.HelpBox("씬 연결이 불완전합니다. JC/인디케이터/이동 인디케이터 설치·연결을 실행하세요.", MessageType.Warning);
        }
        private static void Draw(SerializedProperty p, string label) => EditorGUILayout.PropertyField(p, new GUIContent(label, p.tooltip));
        private static void Mark(JcMovementIndicatorController controller)
        {
            if (Application.isPlaying) JcMovementPlayDraft.Remember(controller);
            else { EditorUtility.SetDirty(controller); EditorSceneManager.MarkSceneDirty(controller.gameObject.scene); }
            SceneView.RepaintAll();
        }
    }

    [InitializeOnLoad]
    internal static class JcMovementPlayDraft
    {
        static JcMovementPlayDraft()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingPlayMode) return;
                foreach (var c in Object.FindObjectsByType<JcMovementIndicatorController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Remember(c);
            };
        }
        private static string Key(JcMovementIndicatorController c) => "JC.Movement.Draft:" + c.gameObject.scene.path + ":" + c.name;
        internal static void Remember(JcMovementIndicatorController c) => SessionState.SetString(Key(c), JsonUtility.ToJson(c.Settings));
        internal static bool TryRead(JcMovementIndicatorController c, out JcMovementIndicatorSettings settings)
        {
            var json = SessionState.GetString(Key(c), "");
            settings = string.IsNullOrEmpty(json) ? default : JsonUtility.FromJson<JcMovementIndicatorSettings>(json);
            return !string.IsNullOrEmpty(json);
        }
    }
}
