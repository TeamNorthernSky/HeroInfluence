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
    /// 단, 이미 Timeline 레일인 연출을 구 Animator 연출로 덮지는 않는다(ASB Timeline 최우선).
    /// 병합본은 스냅샷이므로 ASB가 스킬·이펙트를 추가하면 다시 돌려야 한다(같은 파일에 덮어써 GUID 유지).
    /// ★병합본에 직접 줄을 넣지 말 것 — 재생성하면 사라진다. 재료(ASB 카탈로그 등)에 넣고 재생성한다.
    /// </summary>
    public static class JcMergedAssetBuilder
    {
        const string ConfigPath = "Assets/RenderFX/_Seam/Data/Merged/JC_MergeSources.asset";
        const int MaxListedDiffs = 20;

        [MenuItem("JC VFX/병합 에셋 재생성", priority = 100)]
        public static void Rebuild()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<JcMergedSourceConfig>(ConfigPath);
            if (cfg == null)
            {
                Debug.LogError("[JcMerge] 재료 지정 에셋을 찾지 못했습니다: " + ConfigPath);
                return;
            }

            MergePlan registryPlan = PlanRegistry(cfg);
            MergePlan catalogPlan = PlanCatalog(cfg);

            // 재생성 전에 무엇이 바뀌는지 먼저 본다 — 병합본을 손으로 고친 줄이 조용히 되돌아가는 것을 막는다.
            List<MergeDiff> registryDiffs = registryPlan != null ? Diff(registryPlan, cfg.mergedRegistry, "_entries", "Id", "Prefab") : null;
            List<MergeDiff> catalogDiffs = catalogPlan != null ? Diff(catalogPlan, cfg.mergedCatalog, "_bindings", "SkillIndex", "Presentation") : null;
            if (!ConfirmRiskyDiffs(registryDiffs, catalogDiffs))
            {
                Debug.LogWarning("[JcMerge] 재생성을 취소했습니다. 병합본은 그대로입니다.\n" +
                                 DescribeDiffs("레지스트리", registryDiffs) + DescribeDiffs("카탈로그", catalogDiffs), cfg);
                return;
            }

            var log = new StringBuilder("[JcMerge] 병합 결과\n");
            bool ok = true;

            ok &= WriteRegistry(cfg, registryPlan, log);
            ok &= WriteCatalog(cfg, catalogPlan, log);
            log.Append(DescribeDiffs("레지스트리", registryDiffs)).Append(DescribeDiffs("카탈로그", catalogDiffs));

            // 재료 구성을 지문으로 남긴다 — 이후 재료가 바뀌면 낡음 검사가 잡아낸다.
            cfg.sourceFingerprint = Fingerprint(cfg);
            cfg.lastMergedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            EditorUtility.SetDirty(cfg);
            log.Append($"  지문 기록: {cfg.sourceFingerprint} ({cfg.lastMergedAt})\n");

            // SaveAssets는 프로젝트의 다른 dirty 에셋까지 저장하므로 산출물만 저장한다.
            AssetDatabase.SaveAssetIfDirty(cfg);
            if (cfg.mergedRegistry != null) AssetDatabase.SaveAssetIfDirty(cfg.mergedRegistry);
            if (cfg.mergedCatalog != null) AssetDatabase.SaveAssetIfDirty(cfg.mergedCatalog);

            if (ok) Debug.Log(log.ToString(), cfg);
            else Debug.LogWarning(log.ToString(), cfg);
        }

        // ────────────────────────────────────────────────────────────────
        // 병합 계획: 재료를 읽어 (키 → 대상) 결과만 계산한다. 쓰기 없음.
        // ────────────────────────────────────────────────────────────────
        private sealed class MergePlan
        {
            public readonly List<int> Order = new List<int>();
            public readonly Dictionary<int, Object> Map = new Dictionary<int, Object>();
            public readonly List<int> Overridden = new List<int>();
        }

        // 레지스트리: id → 프리팹
        private static MergePlan PlanRegistry(JcMergedSourceConfig cfg)
        {
            if (cfg.asbRegistry == null || cfg.jcRegistry == null || cfg.mergedRegistry == null)
                return null;

            var plan = new MergePlan();
            Collect(cfg.asbRegistry, "_entries", "Id", "Prefab", plan, false);
            Collect(cfg.jcRegistry, "_entries", "Id", "Prefab", plan, true);
            if (cfg.extraRegistries != null)
            {
                for (int i = 0; i < cfg.extraRegistries.Length; i++)
                    Collect(cfg.extraRegistries[i], "_entries", "Id", "Prefab", plan, true);
            }
            return plan;
        }

        // 카탈로그: skillIndex → 연출 데이터
        private static MergePlan PlanCatalog(JcMergedSourceConfig cfg)
        {
            if (cfg.asbCatalog == null || cfg.jcCatalog == null || cfg.mergedCatalog == null)
                return null;

            var plan = new MergePlan();
            Collect(cfg.asbCatalog, "_bindings", "SkillIndex", "Presentation", plan, false);
            // JC 카탈로그도 Timeline 연출은 구 Animator 연출로 덮지 않는다. JC Justice 카탈로그에는 1030/1031의
            // 구 Animator 데이터가 남아 있어, 보호 없이 덮으면 실전투 1030/1031이 Timeline에서 되돌아간다.
            Collect(cfg.jcCatalog, "_bindings", "SkillIndex", "Presentation", plan, true, preserveTimelineRail: true);
            if (cfg.extraCatalogs != null)
            {
                for (int i = 0; i < cfg.extraCatalogs.Length; i++)
                    Collect(cfg.extraCatalogs[i], "_bindings", "SkillIndex", "Presentation", plan, true,
                        preserveTimelineRail: true);
            }
            return plan;
        }

        /// <summary>
        /// 배열형 SO에서 (키, 값) 쌍을 순서를 지키며 모은다.
        /// 이미 있는 키를 다시 만나면 값을 갈아끼우되 **원래 자리는 유지**한다(순서 안정).
        /// </summary>
        private static void Collect(Object source, string arrayName, string keyName, string valueName,
            MergePlan plan, bool recordOverride, bool preserveTimelineRail = false)
        {
            if (source == null) return;
            var so = new SerializedObject(source);
            SerializedProperty arr = so.FindProperty(arrayName);
            if (arr == null || !arr.isArray) return;

            for (int i = 0; i < arr.arraySize; i++)
            {
                SerializedProperty e = arr.GetArrayElementAtIndex(i);
                int key = e.FindPropertyRelative(keyName).intValue;
                Object value = e.FindPropertyRelative(valueName).objectReferenceValue;
                if (value == null) continue;   // 빈 슬롯은 옮기지 않는다

                if (plan.Map.ContainsKey(key))
                {
                    // 구 Animator 연출로 이미 베이크된 Timeline 연출을 덮으면 캐릭터별 Signal Cue가 사라진다.
                    if (preserveTimelineRail && IsTimeline(plan.Map[key]) && IsAnimator(value))
                        continue;

                    if (recordOverride) plan.Overridden.Add(key);
                    plan.Map[key] = value;
                }
                else
                {
                    plan.Order.Add(key);
                    plan.Map[key] = value;
                }
            }
        }

        private static bool IsTimeline(Object value) => value is SkillPresentationData data && data.IsTimelineRail;
        private static bool IsAnimator(Object value) => value is SkillPresentationData data && !data.IsTimelineRail;

        // ────────────────────────────────────────────────────────────────
        // 쓰기
        // ────────────────────────────────────────────────────────────────
        private static bool WriteRegistry(JcMergedSourceConfig cfg, MergePlan plan, StringBuilder log)
        {
            if (plan == null)
            {
                log.Append("  레지스트리: 재료 또는 산출물이 비어 건너뜀\n");
                return false;
            }

            Write(cfg.mergedRegistry, "_entries", "Id", "Prefab", plan);
            log.Append($"  레지스트리: 총 {plan.Order.Count}개 (ASB 기반 + JC 추가)\n");
            log.Append(plan.Overridden.Count > 0
                ? $"    ※JC가 덮어쓴 id: {string.Join(", ", plan.Overridden)}\n"
                : "    id 충돌 없음(단순 합집합)\n");
            return true;
        }

        private static bool WriteCatalog(JcMergedSourceConfig cfg, MergePlan plan, StringBuilder log)
        {
            if (plan == null)
            {
                log.Append("  카탈로그: 재료 또는 산출물이 비어 건너뜀\n");
                return false;
            }

            Write(cfg.mergedCatalog, "_bindings", "SkillIndex", "Presentation", plan);
            log.Append($"  카탈로그: 총 {plan.Order.Count}개 스킬\n");
            log.Append(plan.Overridden.Count > 0
                ? $"    ※JC가 덮어쓴 스킬: {string.Join(", ", plan.Overridden)}\n"
                : "    덮어쓴 스킬 없음 — JC 카탈로그가 비었는지 확인 필요\n");
            return true;
        }

        private static void Write(Object target, string arrayName, string keyName, string valueName, MergePlan plan)
        {
            var so = new SerializedObject(target);
            SerializedProperty arr = so.FindProperty(arrayName);
            arr.ClearArray();
            for (int i = 0; i < plan.Order.Count; i++)
            {
                arr.InsertArrayElementAtIndex(i);
                SerializedProperty e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative(keyName).intValue = plan.Order[i];
                e.FindPropertyRelative(valueName).objectReferenceValue = plan.Map[plan.Order[i]];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        // ────────────────────────────────────────────────────────────────
        // 차이: 지금 병합본과 재생성 결과를 키 기준으로 비교한다(순서는 보지 않는다).
        // ────────────────────────────────────────────────────────────────
        private enum DiffKind
        {
            Missing,      // 재료에 있는데 병합본에 없음 → 재생성하면 추가
            Changed,      // 대상이 다름 → 재생성하면 교체
            Downgrade,    // Timeline → 구 Animator로 교체(위험)
            MergedOnly    // 병합본에만 있음 → 재생성하면 삭제(위험)
        }

        private readonly struct MergeDiff
        {
            public readonly int Key;
            public readonly Object Current;
            public readonly Object Next;
            public readonly DiffKind Kind;

            public MergeDiff(int key, Object current, Object next, DiffKind kind)
            {
                Key = key; Current = current; Next = next; Kind = kind;
            }

            public bool IsRisky => Kind == DiffKind.Downgrade || Kind == DiffKind.MergedOnly;
        }

        private static List<MergeDiff> Diff(MergePlan plan, Object merged, string arrayName, string keyName, string valueName)
        {
            var current = new Dictionary<int, Object>();
            var currentOrder = new List<int>();
            if (merged != null)
            {
                var so = new SerializedObject(merged);
                SerializedProperty arr = so.FindProperty(arrayName);
                for (int i = 0; arr != null && i < arr.arraySize; i++)
                {
                    SerializedProperty e = arr.GetArrayElementAtIndex(i);
                    int key = e.FindPropertyRelative(keyName).intValue;
                    if (!current.ContainsKey(key)) currentOrder.Add(key);
                    current[key] = e.FindPropertyRelative(valueName).objectReferenceValue;
                }
            }

            var diffs = new List<MergeDiff>();
            foreach (int key in plan.Order)
            {
                Object next = plan.Map[key];
                if (!current.TryGetValue(key, out Object cur) || cur == null)
                    diffs.Add(new MergeDiff(key, null, next, DiffKind.Missing));
                else if (cur != next)
                    diffs.Add(new MergeDiff(key, cur, next, IsTimeline(cur) && IsAnimator(next) ? DiffKind.Downgrade : DiffKind.Changed));
            }
            foreach (int key in currentOrder)
            {
                if (!plan.Map.ContainsKey(key) && current[key] != null)
                    diffs.Add(new MergeDiff(key, current[key], null, DiffKind.MergedOnly));
            }
            return diffs;
        }

        // Timeline→Animator 강등이나 삭제가 있으면 사람이 확인한다. 대화창을 띄울 수 없는 배치 모드에서는 진행하지 않는다.
        private static bool ConfirmRiskyDiffs(List<MergeDiff> registryDiffs, List<MergeDiff> catalogDiffs)
        {
            var risky = new StringBuilder();
            AppendRisky(risky, "레지스트리", registryDiffs);
            AppendRisky(risky, "카탈로그", catalogDiffs);
            if (risky.Length == 0) return true;
            if (Application.isBatchMode) return false;

            return EditorUtility.DisplayDialog(
                "병합 에셋 재생성 확인",
                "재생성하면 아래 항목이 되돌아가거나 사라집니다.\n" +
                "병합본에 직접 넣은 줄이면 먼저 재료(ASB 카탈로그 등)에 옮기세요.\n\n" + risky,
                "그래도 재생성", "취소");
        }

        private static void AppendRisky(StringBuilder into, string label, List<MergeDiff> diffs)
        {
            if (diffs == null) return;
            int listed = 0;
            foreach (MergeDiff d in diffs)
            {
                if (!d.IsRisky) continue;
                if (listed++ >= MaxListedDiffs) { into.Append("  …\n"); break; }
                into.Append($"[{label}] {Line(d)}\n");
            }
        }

        private static string DescribeDiffs(string label, List<MergeDiff> diffs)
        {
            if (diffs == null) return string.Empty;
            if (diffs.Count == 0) return $"  {label}: 병합본과 재료가 같음(차이 0)\n";

            var sb = new StringBuilder($"  {label}: 차이 {diffs.Count}건\n");
            for (int i = 0; i < diffs.Count && i < MaxListedDiffs; i++)
                sb.Append("    ").Append(Line(diffs[i])).Append('\n');
            if (diffs.Count > MaxListedDiffs) sb.Append("    …\n");
            return sb.ToString();
        }

        private static string Line(MergeDiff d)
        {
            switch (d.Kind)
            {
                case DiffKind.Missing: return $"{d.Key}: (병합본에 없음) → {Name(d.Next)}";
                case DiffKind.Downgrade: return $"{d.Key}: {Name(d.Current)} → {Name(d.Next)} ★Timeline→Animator";
                case DiffKind.MergedOnly: return $"{d.Key}: {Name(d.Current)} → (삭제) ★병합본에만 있음";
                default: return $"{d.Key}: {Name(d.Current)} → {Name(d.Next)}";
            }
        }

        private static string Name(Object o)
        {
            if (o == null) return "(없음)";
            string rail = o is SkillPresentationData data ? (data.IsTimelineRail ? " [Timeline]" : " [Animator]") : string.Empty;
            return o.name + rail;
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

            // 낡음 여부: 재생성 결과와 지금 병합본의 내용을 비교한다.
            List<MergeDiff> registryDiffs = ContentDiffs(cfg, out List<MergeDiff> catalogDiffs);
            int diffCount = (registryDiffs?.Count ?? 0) + (catalogDiffs?.Count ?? 0);
            if (diffCount > 0)
            {
                problems += diffCount;
                log.Append("  ★병합본과 재료의 내용이 다릅니다.\n")
                   .Append(DescribeDiffs("레지스트리", registryDiffs))
                   .Append(DescribeDiffs("카탈로그", catalogDiffs));
            }
            else
            {
                string nowPrint = Fingerprint(cfg);
                log.Append(nowPrint == cfg.sourceFingerprint
                    ? $"  최신 상태 (지문 {nowPrint}, 병합 {cfg.lastMergedAt})\n"
                    : $"  내용은 최신이나 지문만 다름 (기록 {cfg.sourceFingerprint} ≠ 현재 {nowPrint}) — 재생성하면 지문이 갱신됩니다.\n");
            }

            log.Append($"  카탈로그 {bindings.arraySize}개 스킬 점검 완료.\n");
            log.Append(problems == 0 ? "  문제 없음." : $"  문제 {problems}건.");
            if (problems == 0) Debug.Log(log.ToString(), cfg);
            else Debug.LogWarning(log.ToString(), cfg);
        }

        private static List<MergeDiff> ContentDiffs(JcMergedSourceConfig cfg, out List<MergeDiff> catalogDiffs)
        {
            MergePlan catalogPlan = PlanCatalog(cfg);
            MergePlan registryPlan = PlanRegistry(cfg);
            catalogDiffs = catalogPlan != null ? Diff(catalogPlan, cfg.mergedCatalog, "_bindings", "SkillIndex", "Presentation") : null;
            return registryPlan != null ? Diff(registryPlan, cfg.mergedRegistry, "_entries", "Id", "Prefab") : null;
        }

        // ────────────────────────────────────────────────────────────────
        // 낡음 감지 — 병합 이후 재료가 바뀌었는지
        // ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 재료 네 에셋의 구성(키와 대상 에셋)을 한 문자열로 눌러 담은 지문.
        /// 항목이 늘거나 줄거나 대상이 바뀌면 값이 달라진다. 값 자체의 편집(예: Cue 시각)은 잡지 못한다 —
        /// 잡으려는 것은 「병합본에 아예 없는 항목이 생긴 상황」이다.
        /// 지문은 빠른 1차 판정용이고, 어긋나면 내용 비교(ContentDiffs)로 실제 차이가 있는지 확인한다.
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

        /// <summary>
        /// 플레이 진입 시 한 번 검사한다. 자동 갱신은 하지 않는다 — 조용한 변경이 더 위험하다.
        /// 지문이 같으면 바로 끝내고, 어긋날 때만 내용을 비교해 실제로 다른 줄이 있을 때 경고한다.
        /// </summary>
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

                List<MergeDiff> registryDiffs = ContentDiffs(cfg, out List<MergeDiff> catalogDiffs);
                if ((registryDiffs?.Count ?? 0) + (catalogDiffs?.Count ?? 0) == 0) return;

                Debug.LogWarning(
                    "[JcMerge] ★병합 에셋이 재료와 다릅니다.\n" +
                    $"  마지막 병합: {cfg.lastMergedAt} (지문 {cfg.sourceFingerprint}, 현재 {now})\n" +
                    DescribeDiffs("레지스트리", registryDiffs) + DescribeDiffs("카탈로그", catalogDiffs) +
                    "  병합본에 없음/교체 → 메뉴 [JC VFX/병합 에셋 재생성]을 실행하세요.\n" +
                    "  ★병합본에만 있음·Timeline→Animator → 재생성하면 사라지거나 되돌아갑니다. 재료(ASB 카탈로그 등)에 먼저 옮기세요.", cfg);
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
