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
                EditorGUILayout.PropertyField(follow, new GUIContent("Basic 따름 (Alter 전용)","켜면 아래 트랜스폼 항목이 연결된 Basic을 따릅니다. 끄면 Alter 자신의 값을 독립적으로 사용합니다."));
                if (follow.boolValue)
                {
                    EditorGUILayout.PropertyField(basicRef, new GUIContent("따를 Basic 프리셋", "위치·형태 기준으로 사용할 Basic 프리셋입니다. 향후 연결을 변경하거나 따름을 해제할 수 있습니다."));
                    if (basicRef.objectReferenceValue == null)
                        EditorGUILayout.HelpBox("따름이 켜져 있는데 Basic 프리셋이 비어 있습니다 — 자기 값이 쓰입니다.", MessageType.Warning);
                }
                EditorGUILayout.Space(4);
            }

            bool locked = hasFollow && follow.boolValue && basicRef.objectReferenceValue != null;
            var set = new HashSet<string>(transformProps);

            bool fold = EditorPrefs.GetBool(foldKey, true);
            bool newFold = EditorGUILayout.Foldout(fold,
                new GUIContent(locked ? "트랜스폼 — Basic 따름 중 (잠김)" : "트랜스폼", "위치·형태 관련 항목입니다. Basic 따름을 해제하면 여기서 독립 조절할 수 있습니다."), true, EditorStyles.foldoutHeader);
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

        // ── 명시 디스크 저장(260807 공식 규격 — 자동저장 폐지·사용자 확정) ───────────────
        //   모델: 인스펙터 조절값 = 메모리 유지(플레이 출입·리컴파일 생존) /
        //         「프리팹에 적용」 = 재질·프리팹 디스크 확정 / 「💾」 = 이 에셋 파일 자체를 디스크 확정.
        //   모든 디스크 쓰기는 명시 클릭으로만 — 실험값이 파일에 스미지 않는 안전판.
        //   ★향후 저스티스·루미나·블랙불릿 부품 정리에도 이 규격을 쓴다(사용자 지정).

        /// <summary>💾 디스크 저장 버튼 — 이 에셋 파일만 저장. wide=단독 배치용 전폭.</summary>
        public static void DrawSaveButton(Object target, bool wide = false)
        {
            var label = wide ? "💾 디스크 저장 (조절값 파일 확정)" : "💾 저장";
            bool clicked = wide
                ? GUILayout.Button(new GUIContent(label,"현재 프리셋 에셋만 디스크에 저장합니다. 다른 미저장 에셋은 저장하지 않습니다."), GUILayout.Height(30))
                : GUILayout.Button(new GUIContent(label,"현재 프리셋 에셋만 디스크에 저장합니다. 다른 미저장 에셋은 저장하지 않습니다."), GUILayout.Height(30), GUILayout.Width(72));
            if (!clicked || target == null) return;
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
            Debug.Log($"[JC] 디스크 저장 완료: {target.name}");
        }

        /// <summary>
        /// ★소속 스킬 분기(260807) — 프리셋 자산이 LetsFightingLove 폴더 소속이면 true.
        /// 같은 프리셋 클래스를 힐·LFL 이 공유하므로, 에디터의 적용/캡처 대상(재질·프리팹)은
        /// 「프리셋이 어느 스킬 폴더에 사는가」로 가른다 — LFL 튜닝이 힐 자산을 덮지 않게(완전 절연).
        /// </summary>
        public static bool IsLfl(Object target)
            => target != null && AssetDatabase.GetAssetPath(target).Replace('\\', '/')
                                              .Contains("/LetsFightingLove/");
    }
}
