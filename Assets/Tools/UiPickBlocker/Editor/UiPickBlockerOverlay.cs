using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace Orora.UiPickBlocker
{
    /// <summary>
    /// [JC 260902] 씬 뷰 오버레이 "UI 피킹 차단기".
    /// 루트 Canvas 아래 계층을 트리로 펼쳐 오브젝트 단위로 씬 뷰 피킹만 껐다 켠다.
    /// 가시성(눈 아이콘)은 건드리지 않으므로 dim은 화면에 그대로 보이고 선택만 관통한다.
    /// 대상은 이름/경로로 찾으므로 게임 코드·프리팹 의존이 0이고 HQLobbyScene·DHScene_3 양쪽에서 동일하게 동작한다.
    /// [260902 v2] 세로 스크롤 + 캔버스 섹션 폴드 + 행별 하위 트리 전개 + 패널 최소화 추가.
    /// </summary>
    [Overlay(typeof(SceneView), OverlayId, "UI 피킹 차단기", true)]
    internal sealed class UiPickBlockerOverlay : Overlay
    {
        internal const string OverlayId = "orora-ui-pick-blocker";

        private const float PanelMaxHeight = 420f;
        private const float PanelMinWidth = 280f;
        private const float IndentPerDepth = 12f;
        private const float FoldWidth = 14f;

        public override VisualElement CreatePanelContent()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.maxHeight = PanelMaxHeight;
            scroll.style.minWidth = PanelMinWidth;
            scroll.Add(new IMGUIContainer(DrawGui));
            return scroll;
        }

        private void DrawGui()
        {
            DrawHeader();
            if (UiPickBlockerState.Minimized) return;

            var auto = EditorGUILayout.ToggleLeft("플레이 진입 시 자동 재적용", UiPickBlockerState.AutoReapply);
            if (auto != UiPickBlockerState.AutoReapply) UiPickBlockerState.AutoReapply = auto;

            var canvases = UiPickBlockerState.RootCanvases();
            if (canvases.Count == 0)
            {
                EditorGUILayout.HelpBox("씬에 루트 Canvas가 없습니다.", MessageType.Info);
                return;
            }

            foreach (var canvas in canvases)
                DrawCanvasSection(canvas);
        }

        private static void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var minimized = UiPickBlockerState.Minimized;
                if (GUILayout.Button(minimized ? "+" : "−", EditorStyles.miniButton, GUILayout.Width(22f)))
                    UiPickBlockerState.Minimized = !minimized;

                EditorGUILayout.LabelField($"차단 {UiPickBlockerState.BlockedCount}건", EditorStyles.miniLabel);

                if (GUILayout.Button("재적용", EditorStyles.miniButton, GUILayout.Width(56f)))
                {
                    var applied = UiPickBlockerState.Reapply();
                    Debug.Log($"[UiPickBlocker] 저장된 차단 목록 재적용: {applied}건");
                }
            }
        }

        private static void DrawCanvasSection(Canvas canvas)
        {
            EditorGUILayout.Space(2f);

            var root = canvas.transform;
            var total = root.childCount;
            var blocked = 0;
            for (int i = 0; i < total; i++)
            {
                if (SceneVisibilityManager.instance.IsPickingDisabled(root.GetChild(i).gameObject)) blocked++;
            }

            var sectionKey = UiPickBlockerState.SectionKey(canvas);
            var expanded = UiPickBlockerState.IsExpanded(sectionKey);
            var next = EditorGUILayout.Foldout(expanded, $"{canvas.name}  (차단 {blocked} / 전체 {total})", true, EditorStyles.foldoutHeader);
            if (next != expanded) UiPickBlockerState.SetExpanded(sectionKey, next);
            if (!next) return;

            DrawPresetRows(canvas);

            if (total == 0)
            {
                EditorGUILayout.LabelField("  (직속 자식 없음)", EditorStyles.miniLabel);
                return;
            }

            for (int i = 0; i < total; i++)
            {
                var child = root.GetChild(i);
                DrawNode(canvas, child, child.name, 0);
            }
        }

        /// <summary>한 오브젝트 행을 그리고, 펼쳐져 있으면 자식들을 재귀로 이어 그린다. 펼친 가지만 순회한다.</summary>
        private static void DrawNode(Canvas canvas, Transform node, string path, int depth)
        {
            var go = node.gameObject;
            var hasChildren = node.childCount > 0;
            var nodeKey = UiPickBlockerState.NodeKey(canvas, path);
            var expanded = hasChildren && UiPickBlockerState.IsExpanded(nodeKey);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(depth * IndentPerDepth);

                if (hasChildren)
                {
                    if (GUILayout.Button(expanded ? "▾" : "▸", EditorStyles.label, GUILayout.Width(FoldWidth)))
                    {
                        UiPickBlockerState.SetExpanded(nodeKey, !expanded);
                        expanded = !expanded;
                    }
                }
                else
                {
                    GUILayout.Space(FoldWidth);
                }

                var blocked = SceneVisibilityManager.instance.IsPickingDisabled(go);
                var label = hasChildren ? $"{go.name} ({node.childCount})" : go.name;
                var nextBlocked = EditorGUILayout.ToggleLeft(label, blocked);
                if (nextBlocked != blocked) UiPickBlockerState.SetBlocked(canvas, path, go, nextBlocked);
            }

            if (!expanded) return;

            for (int i = 0; i < node.childCount; i++)
            {
                var child = node.GetChild(i);
                DrawNode(canvas, child, path + "/" + child.name, depth + 1);
            }
        }

        private static void DrawPresetRows(Canvas canvas)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("모달·툴팁 차단"))
                    UiPickBlockerState.ApplyPreset(canvas, UiPickBlockerState.IsModalOrTooltip, true);

                if (GUILayout.Button("껍데기 차단"))
                    UiPickBlockerState.ApplyPreset(canvas, UiPickBlockerState.IsBundleHusk, true);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("전부 차단"))
                    UiPickBlockerState.ApplyPreset(canvas, _ => true, true);

                if (GUILayout.Button("전부 해제"))
                    UiPickBlockerState.ClearCanvas(canvas);
            }
        }
    }

    /// <summary>
    /// [JC 260902] 차단 목록·폴드 상태의 보관과 적용 담당.
    /// 차단 목록은 "캔버스이름/자식/손자" 전체 경로 키로 SessionState에 저장하므로
    /// 플레이 진입으로 오브젝트가 새로 생성돼도 같은 경로를 다시 찾아 재적용할 수 있다.
    /// </summary>
    internal static class UiPickBlockerState
    {
        private const string BlockedSessionKey = "Orora.UiPickBlocker.BlockedKeys";
        private const string ExpandedSessionKey = "Orora.UiPickBlocker.ExpandedKeys";
        private const string AutoReapplyKey = "Orora.UiPickBlocker.AutoReapply";
        private const string MinimizedKey = "Orora.UiPickBlocker.Minimized";
        private const char Separator = '\n';

        internal static bool AutoReapply
        {
            get => EditorPrefs.GetBool(AutoReapplyKey, true);
            set => EditorPrefs.SetBool(AutoReapplyKey, value);
        }

        internal static bool Minimized
        {
            get => EditorPrefs.GetBool(MinimizedKey, false);
            set => EditorPrefs.SetBool(MinimizedKey, value);
        }

        internal static int BlockedCount => Load(BlockedSessionKey).Count;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        // 컴포저(Awake)가 UI를 스폰한 뒤여야 하므로 EnteredPlayMode에서 한 틱 미뤄 적용한다.
        // (EditorApplication.update 폴링 훅은 쓰지 않는다)
        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            if (!AutoReapply) return;

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlaying) Reapply();
            };
        }

        internal static List<Canvas> RootCanvases()
        {
            var result = new List<Canvas>();
            var all = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var canvas in all)
            {
                if (canvas == null || !canvas.isRootCanvas) continue;
                result.Add(canvas);
            }
            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        internal static bool IsModalOrTooltip(GameObject go)
        {
            var n = go.name;
            return n.IndexOf("Modal", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Tooltip", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // 껍데기 = UIComposer가 스폰한 번들 루트(Place()로 자식을 레이어에 넘긴 뒤 남는 빈 루트).
        // 레이어 루트는 Layer_ 접두사를 쓰므로 그 외 직속 자식을 껍데기로 본다.
        internal static bool IsBundleHusk(GameObject go)
            => !go.name.StartsWith("Layer_", StringComparison.Ordinal);

        internal static void SetBlocked(Canvas canvas, string path, GameObject go, bool blocked)
        {
            if (canvas == null || go == null) return;

            var keys = Load(BlockedSessionKey);
            var key = NodeKey(canvas, path);

            if (blocked)
            {
                keys.Add(key);
                SceneVisibilityManager.instance.DisablePicking(go, true);
            }
            else
            {
                keys.Remove(key);
                SceneVisibilityManager.instance.EnablePicking(go, true);
            }

            Save(BlockedSessionKey, keys);
        }

        /// <summary>프리셋은 캔버스 직속 자식에만 건다(깊은 노드는 트리에서 개별 지정).</summary>
        internal static void ApplyPreset(Canvas canvas, Func<GameObject, bool> match, bool blocked)
        {
            if (canvas == null) return;

            var root = canvas.transform;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (!match(child.gameObject)) continue;
                SetBlocked(canvas, child.name, child.gameObject, blocked);
            }
        }

        /// <summary>이 캔버스에 걸린 차단을 깊은 노드까지 전부 해제하고 저장 목록에서도 지운다.</summary>
        internal static void ClearCanvas(Canvas canvas)
        {
            if (canvas == null) return;

            SceneVisibilityManager.instance.EnablePicking(canvas.gameObject, true);

            var prefix = canvas.name + "/";
            var keys = Load(BlockedSessionKey);
            keys.RemoveWhere(k => k.StartsWith(prefix, StringComparison.Ordinal));
            Save(BlockedSessionKey, keys);
        }

        /// <summary>저장된 차단 목록을 현재 씬 오브젝트에 다시 적용한다. 목록에 없는 대상은 건드리지 않는다.</summary>
        internal static int Reapply()
        {
            var keys = Load(BlockedSessionKey);
            if (keys.Count == 0) return 0;

            int applied = 0;
            foreach (var canvas in RootCanvases())
            {
                var prefix = canvas.name + "/";
                foreach (var key in keys)
                {
                    if (!key.StartsWith(prefix, StringComparison.Ordinal)) continue;

                    // Transform.Find는 "a/b/c" 경로를 받고 비활성 자식도 찾는다.
                    var found = canvas.transform.Find(key.Substring(prefix.Length));
                    if (found == null) continue;

                    SceneVisibilityManager.instance.DisablePicking(found.gameObject, true);
                    applied++;
                }
            }

            return applied;
        }

        internal static string NodeKey(Canvas canvas, string path) => canvas.name + "/" + path;

        internal static string SectionKey(Canvas canvas) => "#section:" + canvas.name;

        internal static bool IsExpanded(string key) => Load(ExpandedSessionKey).Contains(key);

        internal static void SetExpanded(string key, bool expanded)
        {
            var keys = Load(ExpandedSessionKey);
            if (expanded) keys.Add(key);
            else keys.Remove(key);
            Save(ExpandedSessionKey, keys);
        }

        private static HashSet<string> Load(string sessionKey)
        {
            var raw = SessionState.GetString(sessionKey, string.Empty);
            var set = new HashSet<string>();
            if (string.IsNullOrEmpty(raw)) return set;

            foreach (var entry in raw.Split(Separator))
            {
                if (!string.IsNullOrEmpty(entry)) set.Add(entry);
            }

            return set;
        }

        private static void Save(string sessionKey, HashSet<string> keys)
        {
            var buffer = new string[keys.Count];
            keys.CopyTo(buffer);
            SessionState.SetString(sessionKey, string.Join(Separator.ToString(), buffer));
        }
    }
}
