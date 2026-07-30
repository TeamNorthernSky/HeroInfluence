using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace JC.VFX.EditorTools
{
    /// <summary>
    /// 클립 이벤트 대행 표 인스펙터 — 표가 실제 연출 데이터·클립과 어긋나지 않았는지 대조한다.
    /// 이 표는 「원래 클립이 할 일」을 임시로 대신하는 것이라, 원본과의 동기화가 유일한 위험 요소다.
    /// </summary>
    [CustomEditor(typeof(JcCueTimingTable))]
    public class JcCueTimingTableEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var t = (JcCueTimingTable)target;

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "이 표는 임시 장치다. 애니메이션 클립에 AniEvent_PresentationCue 이벤트가 심기면\n" +
                "드라이버가 자동으로 비켜서므로, 그때 이 파일은 지워도 된다.\n" +
                "표가 Resources 폴더에 없으면 드라이버 자체가 생성되지 않는다.",
                MessageType.Info);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("표 점검 — 중복·빈 줄·클립 이벤트 중복 확인", GUILayout.Height(26)))
            {
                Verify(t);
            }
        }

        private static void Verify(JcCueTimingTable table)
        {
            var sb = new StringBuilder("[JcCueTiming] 표 점검\n");
            int problems = 0;

            var seen = new HashSet<string>();
            for (int i = 0; i < table.entries.Length; i++)
            {
                JcCueTimingTable.Entry e = table.entries[i];
                if (e == null) continue;

                if (string.IsNullOrWhiteSpace(e.stateName) || string.IsNullOrWhiteSpace(e.cueName))
                {
                    sb.Append($"  [{i}] 상태 또는 Cue 이름이 비었다.\n");
                    problems++;
                    continue;
                }

                string key = e.stateName + "|" + e.cueName;
                if (!seen.Add(key))
                {
                    sb.Append($"  [{i}] 같은 상태·Cue 조합이 중복이다: {key}\n");
                    problems++;
                }

                if (!e.stateName.Contains("."))
                {
                    sb.Append($"  [{i}] 상태 이름에 레이어 경로가 없다(예: Base Layer.{e.stateName}).\n");
                    problems++;
                }
            }

            // 프로젝트의 클립 중 같은 Cue 이름을 자체 이벤트로 가진 것 — 드라이버가 자동 회피할 대상.
            var owned = new Dictionary<string, List<string>>();
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) continue;

                foreach (AnimationEvent ev in clip.events)
                {
                    if (ev.functionName != "AniEvent_PresentationCue" || string.IsNullOrEmpty(ev.stringParameter)) continue;
                    if (!owned.TryGetValue(ev.stringParameter, out List<string> list))
                    {
                        list = new List<string>();
                        owned[ev.stringParameter] = list;
                    }
                    if (!list.Contains(clip.name)) list.Add(clip.name);
                }
            }

            var overlap = new HashSet<string>();
            for (int i = 0; i < table.entries.Length; i++)
            {
                JcCueTimingTable.Entry e = table.entries[i];
                if (e != null && !string.IsNullOrEmpty(e.cueName) && owned.ContainsKey(e.cueName))
                {
                    overlap.Add(e.cueName);
                }
            }

            if (overlap.Count > 0)
            {
                sb.Append("\n  ※자체 이벤트를 가진 클립이 있는 Cue(해당 클립 재생 시 드라이버가 자동으로 비켜선다):\n");
                foreach (string cue in overlap)
                {
                    sb.Append($"     '{cue}' → {string.Join(", ", owned[cue])}\n");
                }
            }

            sb.Append(problems == 0 ? "\n  문제 없음." : $"\n  문제 {problems}건.");
            if (problems == 0) Debug.Log(sb.ToString(), table);
            else Debug.LogWarning(sb.ToString(), table);
        }
    }
}
