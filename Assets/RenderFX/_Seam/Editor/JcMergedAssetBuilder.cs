using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace JC.VFX.EditorTools
{
    /// <summary>
    /// 병합 에셋 생성기 — 「ASB 전부 + JC 것」을 한 에셋에 합친다.
    ///
    /// 왜 필요한가: 전투씬은 카탈로그·레지스트리를 하나씩만 참조한다. JC 것으로 통째로 갈아끼우면
    /// JC에 없는 스킬(다른 클래스)의 연출이 전부 사라진다. 그래서 합집합이 필요하다.
    ///
    /// 겹칠 때는 **JC가 이긴다** — 저스티스 1010/1020/1030은 JC 연출 데이터를 쓰겠다는 뜻이다.
    /// 병합본은 스냅샷이므로 ASB가 스킬·이펙트를 추가하면 다시 돌려야 한다(같은 파일에 덮어써 GUID 유지).
    /// </summary>
    public static class JcMergedAssetBuilder
    {
        const string ConfigPath = "Assets/RenderFX/_Seam/Data/Merged/JC_MergeSources.asset";

        [MenuItem("JC VFX/병합 에셋 재생성", priority = 100)]
        public static void Rebuild()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<JcMergedSourceConfig>(ConfigPath);
            if (cfg == null)
            {
                Debug.LogError("[JcMerge] 재료 지정 에셋을 찾지 못했습니다: " + ConfigPath);
                return;
            }

            var log = new StringBuilder("[JcMerge] 병합 결과\n");
            bool ok = true;

            ok &= MergeRegistry(cfg, log);
            ok &= MergeCatalog(cfg, log);

            // 재료 구성을 지문으로 남긴다 — 이후 재료가 바뀌면 낡음 검사가 잡아낸다.
            cfg.sourceFingerprint = Fingerprint(cfg);
            cfg.lastMergedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            EditorUtility.SetDirty(cfg);
            log.Append($"  지문 기록: {cfg.sourceFingerprint} ({cfg.lastMergedAt})\n");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (ok) Debug.Log(log.ToString(), cfg);
            else Debug.LogWarning(log.ToString(), cfg);
        }

        // ────────────────────────────────────────────────────────────────
        // 레지스트리: id → 프리팹
        // ────────────────────────────────────────────────────────────────
        private static bool MergeRegistry(JcMergedSourceConfig cfg, StringBuilder log)
        {
            if (cfg.asbRegistry == null || cfg.jcRegistry == null || cfg.mergedRegistry == null)
            {
                log.Append("  레지스트리: 재료 또는 산출물이 비어 건너뜀\n");
                return false;
            }

            var order = new List<int>();
            var map = new Dictionary<int, Object>();
            var overridden = new List<int>();

            Collect(cfg.asbRegistry, "_entries", "Id", "Prefab", order, map, null);
            Collect(cfg.jcRegistry, "_entries", "Id", "Prefab", order, map, overridden);
            if (cfg.extraRegistries != null)
            {
                for (int i = 0; i < cfg.extraRegistries.Length; i++)
                    Collect(cfg.extraRegistries[i], "_entries", "Id", "Prefab", order, map, overridden);
            }

            var so = new SerializedObject(cfg.mergedRegistry);
            SerializedProperty arr = so.FindProperty("_entries");
            arr.ClearArray();
            for (int i = 0; i < order.Count; i++)
            {
                arr.InsertArrayElementAtIndex(i);
                SerializedProperty e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("Id").intValue = order[i];
                e.FindPropertyRelative("Prefab").objectReferenceValue = map[order[i]];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cfg.mergedRegistry);

            log.Append($"  레지스트리: 총 {order.Count}개 (ASB 기반 + JC 추가)\n");
            log.Append(overridden.Count > 0
                ? $"    ※JC가 덮어쓴 id: {string.Join(", ", overridden)}\n"
                : "    id 충돌 없음(단순 합집합)\n");
            return true;
        }

        // ────────────────────────────────────────────────────────────────
        // 카탈로그: skillIndex → 연출 데이터
        // ────────────────────────────────────────────────────────────────
        private static bool MergeCatalog(JcMergedSourceConfig cfg, StringBuilder log)
        {
            if (cfg.asbCatalog == null || cfg.jcCatalog == null || cfg.mergedCatalog == null)
            {
                log.Append("  카탈로그: 재료 또는 산출물이 비어 건너뜀\n");
                return false;
            }

            var order = new List<int>();
            var map = new Dictionary<int, Object>();
            var overridden = new List<int>();

            Collect(cfg.asbCatalog, "_bindings", "SkillIndex", "Presentation", order, map, null);
            Collect(cfg.jcCatalog, "_bindings", "SkillIndex", "Presentation", order, map, overridden);
            if (cfg.extraCatalogs != null)
            {
                for (int i = 0; i < cfg.extraCatalogs.Length; i++)
                    Collect(cfg.extraCatalogs[i], "_bindings", "SkillIndex", "Presentation", order, map, overridden);
            }

            var so = new SerializedObject(cfg.mergedCatalog);
            SerializedProperty arr = so.FindProperty("_bindings");
            arr.ClearArray();
            for (int i = 0; i < order.Count; i++)
            {
                arr.InsertArrayElementAtIndex(i);
                SerializedProperty b = arr.GetArrayElementAtIndex(i);
                b.FindPropertyRelative("SkillIndex").intValue = order[i];
                b.FindPropertyRelative("Presentation").objectReferenceValue = map[order[i]];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(cfg.mergedCatalog);

            log.Append($"  카탈로그: 총 {order.Count}개 스킬\n");
            log.Append(overridden.Count > 0
                ? $"    ※JC가 덮어쓴 스킬: {string.Join(", ", overridden)}\n"
                : "    덮어쓴 스킬 없음 — JC 카탈로그가 비었는지 확인 필요\n");
            return true;
        }

        /// <summary>
        /// 배열형 SO에서 (키, 값) 쌍을 순서를 지키며 모은다.
        /// 이미 있는 키를 다시 만나면 값을 갈아끼우되 **원래 자리는 유지**한다(순서 안정).
        /// </summary>
        private static void Collect(Object source, string arrayName, string keyName, string valueName,
            List<int> order, Dictionary<int, Object> map, List<int> overriddenOut)
        {
            var so = new SerializedObject(source);
            SerializedProperty arr = so.FindProperty(arrayName);
            if (arr == null || !arr.isArray) return;

            for (int i = 0; i < arr.arraySize; i++)
            {
                SerializedProperty e = arr.GetArrayElementAtIndex(i);
                int key = e.FindPropertyRelative(keyName).intValue;
                Object value = e.FindPropertyRelative(valueName).objectReferenceValue;
                if (value == null) continue;   // 빈 슬롯은 옮기지 않는다

                if (map.ContainsKey(key))
                {
                    overriddenOut?.Add(key);
                    map[key] = value;
                }
                else
                {
                    order.Add(key);
                    map[key] = value;
                }
            }
        }

        // ────────────────────────────────────────────────────────────────
        // 점검: 병합본이 실제로 쓸 수 있는 상태인지
        // ────────────────────────────────────────────────────────────────
        [MenuItem("JC VFX/병합 에셋 점검", priority = 101)]
        public static void Verify()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<JcMergedSourceConfig>(ConfigPath);
            if (cfg == null || cfg.mergedCatalog == null || cfg.mergedRegistry == null)
            {
                Debug.LogError("[JcMerge] 재료 지정 또는 산출물이 비었습니다.");
                return;
            }

            var log = new StringBuilder("[JcMerge] 점검\n");
            int problems = 0;

            // 카탈로그의 각 연출 데이터가 참조하는 EffectId가 병합 레지스트리에 있는지
            var catSo = new SerializedObject(cfg.mergedCatalog);
            SerializedProperty bindings = catSo.FindProperty("_bindings");
            var missing = new SortedDictionary<int, List<int>>();   // effectId → 참조한 스킬들

            for (int i = 0; i < bindings.arraySize; i++)
            {
                SerializedProperty b = bindings.GetArrayElementAtIndex(i);
                int skill = b.FindPropertyRelative("SkillIndex").intValue;
                var data = b.FindPropertyRelative("Presentation").objectReferenceValue as SkillPresentationData;
                if (data == null) continue;

                foreach (int id in CollectEffectIds(data))
                {
                    if (id == 0) continue;
                    if (cfg.mergedRegistry.Get(id) != null) continue;
                    if (!missing.TryGetValue(id, out List<int> users)) { users = new List<int>(); missing[id] = users; }
                    if (!users.Contains(skill)) users.Add(skill);
                }
            }

            if (missing.Count > 0)
            {
                problems += missing.Count;
                log.Append("  ★레지스트리에 없는 이펙트 id를 가리키는 Cue가 있습니다(그 Cue는 아무것도 안 뜹니다):\n");
                foreach (var kv in missing)
                {
                    log.Append($"     id {kv.Key} ← 스킬 {string.Join(", ", kv.Value)}\n");
                }
            }

            // 낡음 여부도 같이 본다.
            string nowPrint = Fingerprint(cfg);
            if (string.IsNullOrEmpty(cfg.sourceFingerprint))
            {
                log.Append("  지문 기록이 없습니다 — 한 번 재생성하세요.\n");
                problems++;
            }
            else if (nowPrint != cfg.sourceFingerprint)
            {
                log.Append($"  ★병합본이 낡았습니다. 기록 {cfg.sourceFingerprint} ≠ 현재 {nowPrint}\n" +
                           "     재료에 항목이 추가·변경되었습니다. [JC VFX/병합 에셋 재생성] 필요.\n");
                problems++;
            }
            else
            {
                log.Append($"  최신 상태 (지문 {nowPrint}, 병합 {cfg.lastMergedAt})\n");
            }

            log.Append($"  카탈로그 {bindings.arraySize}개 스킬 점검 완료.\n");
            log.Append(problems == 0 ? "  문제 없음." : $"  문제 {problems}건.");
            if (problems == 0) Debug.Log(log.ToString(), cfg);
            else Debug.LogWarning(log.ToString(), cfg);
        }

        // ────────────────────────────────────────────────────────────────
        // 낡음 감지 — 병합 이후 재료가 바뀌었는지
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 재료 네 에셋의 구성(키와 대상 에셋)을 한 문자열로 눌러 담은 지문.
        /// 항목이 늘거나 줄거나 대상이 바뀌면 값이 달라진다. 값 자체의 편집(예: Cue 시각)은 잡지 못한다 —
        /// 잡으려는 것은 「병합본에 아예 없는 항목이 생긴 상황」이다.
        /// </summary>
        private static string Fingerprint(JcMergedSourceConfig cfg)
        {
            var sb = new StringBuilder();
            Append(cfg.asbCatalog, "_bindings", "SkillIndex", "Presentation", sb);
            Append(cfg.jcCatalog, "_bindings", "SkillIndex", "Presentation", sb);
            Append(cfg.asbRegistry, "_entries", "Id", "Prefab", sb);
            Append(cfg.jcRegistry, "_entries", "Id", "Prefab", sb);
            if (cfg.extraCatalogs != null)
                for (int i = 0; i < cfg.extraCatalogs.Length; i++)
                    Append(cfg.extraCatalogs[i], "_bindings", "SkillIndex", "Presentation", sb);
            if (cfg.extraRegistries != null)
                for (int i = 0; i < cfg.extraRegistries.Length; i++)
                    Append(cfg.extraRegistries[i], "_entries", "Id", "Prefab", sb);

            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
                return System.BitConverter.ToString(hash).Replace("-", "").Substring(0, 12);
            }

            void Append(Object src, string arrayName, string keyName, string valueName, StringBuilder into)
            {
                if (src == null) { into.Append("null;"); return; }
                var so = new SerializedObject(src);
                SerializedProperty arr = so.FindProperty(arrayName);
                if (arr == null || !arr.isArray) { into.Append("noarr;"); return; }

                for (int i = 0; i < arr.arraySize; i++)
                {
                    SerializedProperty e = arr.GetArrayElementAtIndex(i);

                    // ★참조를 objectReferenceValue로 읽으면 대상 에셋이 통째로 로드되고, 그 순간 OnValidate가 돈다.
                    //   연출 데이터 26개를 다 깨우면 ASB 검증 경고가 무더기로 쏟아진다(내용은 기존 그대로).
                    //   instanceID → 경로 조회는 로드 없이 끝나므로 조용하다.
                    int instanceId = e.FindPropertyRelative(valueName).objectReferenceInstanceIDValue;
                    string guid = instanceId != 0
                        ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(instanceId))
                        : "0";

                    into.Append(e.FindPropertyRelative(keyName).intValue).Append('=').Append(guid).Append(',');
                }
                into.Append(';');
            }
        }

        /// <summary>플레이 진입 시 한 번 검사한다. 자동 갱신은 하지 않는다 — 조용한 변경이 더 위험하다.</summary>
        [InitializeOnLoadMethod]
        private static void HookStaleCheck()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode) return;

                var cfg = AssetDatabase.LoadAssetAtPath<JcMergedSourceConfig>(ConfigPath);
                if (cfg == null || !cfg.warnWhenStale || string.IsNullOrEmpty(cfg.sourceFingerprint)) return;

                string now = Fingerprint(cfg);
                if (now == cfg.sourceFingerprint) return;

                Debug.LogWarning(
                    "[JcMerge] ★병합 에셋이 낡았습니다.\n" +
                    $"  마지막 병합: {cfg.lastMergedAt} (지문 {cfg.sourceFingerprint})\n" +
                    $"  현재 재료 지문: {now}\n" +
                    "  ASB 또는 JC의 연출 카탈로그·이펙트 레지스트리에 항목이 추가·변경된 것으로 보입니다.\n" +
                    "  그 항목은 병합본에 없으므로 게임에서 나오지 않습니다.\n" +
                    "  → 메뉴 [JC VFX/병합 에셋 재생성]을 실행하세요.", cfg);
            };
        }

        /// <summary>연출 데이터의 전 페이즈 Cue에서 EffectId를 긁어모은다.</summary>
        private static IEnumerable<int> CollectEffectIds(SkillPresentationData data)
        {
            var so = new SerializedObject(data);
            SerializedProperty it = so.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = true;
                if (it.name != "EffectIds" || !it.isArray) continue;
                for (int i = 0; i < it.arraySize; i++)
                {
                    yield return it.GetArrayElementAtIndex(i).intValue;
                }
            }
        }
    }
}
