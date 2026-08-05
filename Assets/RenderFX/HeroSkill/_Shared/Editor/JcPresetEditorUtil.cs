using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 프리셋 에디터 공통 도우미 — 「Basic 따름」 잠금 UI (260805, 사용자 지정 규격).
    ///
    /// 규격: Alter 프리셋에는 followBasic 토글이 기본 ON.
    ///   토글 ON  → 트랜스폼 섹션이 폴드 안에서 <b>비활성(잠김)</b> — 값은 Basic 을 따른다.
    ///   토글 OFF → 자기 소유 트랜스폼을 직접 편집.
    /// followBasic/basicRef 필드가 없는 타입(공용 프리셋 등)은 잠금 없이 전체를 그린다.
    /// </summary>
    public static class JcPresetEditorUtil
    {
        /// <summary>
        /// followBasic·basicRef → 트랜스폼 폴드(잠금 가능) → 나머지 필드 순으로 그린다.
        /// <paramref name="transformProps"/> = 트랜스폼 섹션에 묶을 직렬화 프로퍼티 이름들.
        /// </summary>
        public static void DrawWithFollowLock(SerializedObject so, string foldKey, string[] transformProps)
        {
            so.Update();
            var follow = so.FindProperty("followBasic");
            var basicRef = so.FindProperty("basicRef");
            bool hasFollow = follow != null && basicRef != null;

            if (hasFollow)
            {
                EditorGUILayout.PropertyField(follow, new GUIContent("Basic 따름 (Alter 전용)"));
                if (follow.boolValue)
                {
                    EditorGUILayout.PropertyField(basicRef, new GUIContent("따를 Basic 프리셋"));
                    if (basicRef.objectReferenceValue == null)
                        EditorGUILayout.HelpBox("따름이 켜져 있는데 Basic 프리셋이 비어 있습니다 — 자기 값이 쓰입니다.", MessageType.Warning);
                }
                EditorGUILayout.Space(4);
            }

            bool locked = hasFollow && follow.boolValue && basicRef.objectReferenceValue != null;
            var set = new HashSet<string>(transformProps);

            bool fold = EditorPrefs.GetBool(foldKey, true);
            bool newFold = EditorGUILayout.Foldout(fold,
                locked ? "트랜스폼 — Basic 따름 중 (잠김)" : "트랜스폼", true, EditorStyles.foldoutHeader);
            if (newFold != fold) EditorPrefs.SetBool(foldKey, newFold);
            if (newFold)
                using (new EditorGUI.DisabledScope(locked))
                using (new EditorGUI.IndentLevelScope())
                    foreach (var name in transformProps)
                    {
                        var p = so.FindProperty(name);
                        if (p != null) EditorGUILayout.PropertyField(p, true);
                    }
            EditorGUILayout.Space(6);

            // 나머지(룩 등) — 스크립트·따름·트랜스폼 제외 전부
            var it = so.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.name == "m_Script" || it.name == "followBasic" || it.name == "basicRef" || set.Contains(it.name))
                    continue;
                EditorGUILayout.PropertyField(it, true);
            }
            so.ApplyModifiedProperties();
        }

        /// <summary>변종 접미 — _Basic/_Alter 프리셋은 해당 접미, 공용(무접미)은 null(=양쪽 대상).</summary>
        public static string VariantSuffix(Object target)
        {
            if (target == null) return null;
            if (target.name.EndsWith("_Basic")) return "_Basic";
            if (target.name.EndsWith("_Alter")) return "_Alter";
            return null;
        }
    }
}
