#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using JC.Tutorial;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Inspector/Scene 도구로 바꾼 튜토리얼 UI만 보관한다. 게임 진행 상태와 런타임 계층 이동은 저장하지 않는다.</summary>
[InitializeOnLoad]
public static class JcTutorialUiPlaySave
{
    const string Prefix = "JC.TutorialUiPlaySave.";
    public static string SavePath => Path.Combine(Path.GetDirectoryName(Application.dataPath), "UserSettings/JC_TutorialUiPlayEdits.json");
    public static bool Enabled { get => EditorPrefs.GetBool(Prefix + "Enabled", false); set => EditorPrefs.SetBool(Prefix + "Enabled", value); }
    public static bool SaveScenes { get => EditorPrefs.GetBool(Prefix + "SaveScenes", true); set => EditorPrefs.SetBool(Prefix + "SaveScenes", value); }
    public static string Status { get; private set; } = "편집 대기";
    [Serializable] public sealed class Value { public int type; public string text; public float[] numbers; }
    [Serializable] public sealed class Edit
    {
        public string id, scene, kind, property, message, json;
        public int panel;
        public Value value;
    }
    [Serializable] sealed class Document { public List<Edit> edits = new List<Edit>(); }
    [Serializable] sealed class Binding { public string key, id; }
    [Serializable] sealed class Bindings { public List<Binding> items = new List<Binding>(); }
    static readonly Dictionary<int, string> runtimeIds = new Dictionary<int, string>();
    static bool applying;
    static Document document;
    public static int PendingCount => Data.edits.Count;
    static Document Data
    {
        get
        {
            if (document == null)
            {
                try { document = File.Exists(SavePath) ? JsonUtility.FromJson<Document>(File.ReadAllText(SavePath)) : new Document(); }
                catch (Exception e) { Status = "저장 파일 읽기 실패: " + e.Message; document = new Document(); }
            }
            return document;
        }
    }
    static JcTutorialUiPlaySave()
    {
        Undo.postprocessModifications += OnModifications;
        Undo.undoRedoPerformed += RecaptureAfterUndo;
        EditorApplication.playModeStateChanged += OnPlayState;
        EditorApplication.delayCall += () => { if (!EditorApplication.isPlaying) CaptureBindings(); else BindRuntimeObjects(); };
    }
    static bool IsTutorial(Scene scene) => scene.IsValid() && !string.IsNullOrEmpty(scene.path) &&
        Path.GetFileNameWithoutExtension(scene.path).StartsWith("Tutorial", StringComparison.OrdinalIgnoreCase);
    static GameObject Owner(Object obj) => obj is GameObject go ? go : obj is Component c ? c.gameObject : null;
    static string Key(Object obj)
    {
        var go = Owner(obj); if (go == null) return null;
        string path = go.name;
        for (var p = go.transform.parent; p != null; p = p.parent) path = p.name + "/" + path;
        int index = obj is Component c ? Array.IndexOf(go.GetComponents(c.GetType()), c) : 0;
        return go.scene.path + "|" + path + "|" + obj.GetType().FullName + "|" + index;
    }
    static IEnumerable<Object> SceneObjects()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i); if (!IsTutorial(scene)) continue;
            foreach (var root in scene.GetRootGameObjects()) foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!(t is RectTransform))
                {
                    foreach (var c in t.GetComponents<Component>())
                        if (c is TutorialPublicityExplanationView || c is JcTutorialGuideView) yield return c;
                    continue;
                }
                yield return t.gameObject;
                foreach (var c in t.GetComponents<Component>()) if (c != null) yield return c;
            }
        }
    }
    static void CaptureBindings()
    {
        var bindings = new Bindings();
        foreach (var obj in SceneObjects())
        {
            var id = GlobalObjectId.GetGlobalObjectIdSlow(obj);
            if (id.targetObjectId != 0) bindings.items.Add(new Binding { key = Key(obj), id = id.ToString() });
        }
        SessionState.SetString(Prefix + "Bindings", JsonUtility.ToJson(bindings));
    }
    static void BindRuntimeObjects()
    {
        runtimeIds.Clear();
        var bindings = JsonUtility.FromJson<Bindings>(SessionState.GetString(Prefix + "Bindings", "{}"));
        var map = new Dictionary<string, string>();
        if (bindings?.items != null) foreach (var b in bindings.items) map[b.key] = b.id;
        foreach (var obj in SceneObjects()) if (map.TryGetValue(Key(obj), out var id)) runtimeIds[obj.GetInstanceID()] = id;
    }
    static string StableId(Object obj)
    {
        if (runtimeIds.TryGetValue(obj.GetInstanceID(), out var id)) return id;
        var global = GlobalObjectId.GetGlobalObjectIdSlow(obj);
        if (global.targetObjectId == 0) return null;
        id = global.ToString(); runtimeIds[obj.GetInstanceID()] = id; return id;
    }
    static bool Allowed(Object target, string path)
    {
        var go = Owner(target); if (go == null || !IsTutorial(go.scene) || !(go.transform is RectTransform)) return false;
        if (target is RectTransform)
            return new[] { "m_AnchorMin", "m_AnchorMax", "m_Pivot", "m_AnchoredPosition", "m_SizeDelta", "m_LocalRotation", "m_LocalScale", "m_LocalPosition" }.Any(p => path == p || path.StartsWith(p + ".", StringComparison.Ordinal));
        if (target is TMP_Text)
            return new[] { "m_text", "m_fontSize", "m_fontSizeBase", "m_fontSizeMin", "m_fontSizeMax", "m_enableAutoSizing", "m_enableWordWrapping", "m_fontStyle", "m_fontColor", "m_HorizontalAlignment", "m_VerticalAlignment", "m_characterSpacing", "m_wordSpacing", "m_lineSpacing", "m_paragraphSpacing", "m_margin" }.Any(p => path == p || path.StartsWith(p + ".", StringComparison.Ordinal));
        if (target is Image) return path == "m_Enabled" || path == "m_Color" || path.StartsWith("m_Color.", StringComparison.Ordinal);
        if (target is CanvasGroup) return path == "m_Alpha";
        return target is GameObject && path == "m_IsActive";
    }
    static UndoPropertyModification[] OnModifications(UndoPropertyModification[] modifications)
    {
        if (!Enabled || !EditorApplication.isPlaying || applying) return modifications;
        foreach (var modification in modifications)
        {
            var change = modification.currentValue;
            if (change.target == null || !Allowed(change.target, change.propertyPath)) continue;
            Capture(change.target, change.propertyPath, change.value);
        }
        return modifications;
    }
    static void Capture(Object obj, string path, string editedText = null)
    {
        var overlay = Object.FindObjectsByType<TutorialPublicityExplanationView>(FindObjectsSortMode.None).FirstOrDefault(v => v.IsOverlayElement(obj));
        if (overlay != null)
        {
            var json = overlay.CaptureOverlayPresentation(obj);
            if (json != null) Put(overlay, "overlay", "", json, null, overlay.CurrentPanelIndex);
            return;
        }
        var presentation = Object.FindObjectsByType<TutorialPublicityExplanationView>(FindObjectsSortMode.None).FirstOrDefault(v => v.IsMessageElement(obj));
        if (presentation != null)
        {
            // Undo의 변경 통지는 실제 필드 적용 후 호출된다. 다음 단계로 넘어가기 전에 바로 보관한다.
            if (obj is TMP_Text text && path == "m_text" && editedText != null) text.text = editedText;
            CapturePresentation(presentation);
            return;
        }
        if (Object.FindObjectsByType<TutorialPublicityExplanationView>(FindObjectsSortMode.None).Any(v => v.IsTemporarilyPromotedTarget(obj)))
        {
            Status = "실제 홍보 조작 UI는 강조 중 임시 계층에 있습니다. 안내 요소의 배치를 수정해 주세요."; return;
        }
        if (obj is TMP_Text tmp && path == "m_text")
        {
            var guide = Object.FindObjectsByType<JcTutorialGuideView>(FindObjectsSortMode.None).FirstOrDefault(v => v.IsContentText(tmp));
            if (guide != null)
            {
                Put(guide, "content", "", guide.CaptureContentEdit(tmp, editedText ?? tmp.text)); return;
            }
        }
        var so = new SerializedObject(obj); var property = so.FindProperty(path); if (property == null) return;
        var value = ReadValue(property); if (value == null) return;
        if (path == "m_text" && editedText != null) value.text = editedText;
        Put(obj, "property", path, null, value);
    }
    static void CapturePresentation(TutorialPublicityExplanationView view)
    {
        if (view == null || view.CurrentPanelIndex < 0) return;
        var json = view.CaptureMessagePresentation();
        if (json != null) Put(view, "presentation", "", json, null, view.CurrentPanelIndex, view.CurrentMessage.text);
    }
    static void RecaptureAfterUndo()
    {
        if (!Enabled || !EditorApplication.isPlaying || applying) return;
        foreach (var edit in Data.edits.ToArray())
        {
            var pair = runtimeIds.FirstOrDefault(p => p.Value == edit.id);
            var obj = EditorUtility.InstanceIDToObject(pair.Key);
            if (obj == null) continue;
            if (edit.kind == "property") Capture(obj, edit.property);
            else if (edit.kind == "presentation" && obj is TutorialPublicityExplanationView view && view.CurrentPanelIndex == edit.panel) CapturePresentation(view);
            else if (edit.kind == "overlay" && obj is TutorialPublicityExplanationView overlay && overlay.CurrentPanelIndex == edit.panel)
                Put(overlay, "overlay", "", overlay.CaptureOverlayPresentation(), null, overlay.CurrentPanelIndex);
            else if (edit.kind == "content" && obj is JcTutorialGuideView guide && guide.HasEditedCurrentContent) Put(guide, "content", "", guide.CaptureCurrentContentTexts());
        }
    }
    static void Put(Object target, string kind, string property, string json, Value value = null, int panel = 0, string message = null)
    {
        string id = StableId(target);
        if (string.IsNullOrEmpty(id)) { Status = "씬 원본이 없는 런타임 생성 UI는 저장하지 않습니다."; return; }
        var edits = Data.edits;
        edits.RemoveAll(e => e.id == id && e.kind == kind && e.property == property && e.panel == panel);
        edits.Add(new Edit { id = id, scene = Owner(target).scene.path, kind = kind, property = property, json = json, value = value, panel = panel, message = message });
        Write(); Status = "저장됨: " + target.name + " · " + edits.Count + "개 항목";
    }
    static void Write()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
        string temp = SavePath + ".tmp"; File.WriteAllText(temp, JsonUtility.ToJson(Data, true));
        if (File.Exists(SavePath)) File.Replace(temp, SavePath, null); else File.Move(temp, SavePath);
    }
    public static void SaveSelection()
    {
        if (!EditorApplication.isPlaying) { Status = "선택 UI 저장은 플레이 중 사용합니다."; return; }
        foreach (var go in Selection.gameObjects)
        {
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                var so = new SerializedObject(c); var it = so.GetIterator(); bool children = true;
                while (it.NextVisible(children)) { children = false; if (Allowed(c, it.propertyPath)) Capture(c, it.propertyPath); }
            }
        }
    }
    public static void SelectCurrentMessage()
    {
        var view = Object.FindObjectsByType<TutorialPublicityExplanationView>(FindObjectsSortMode.None).FirstOrDefault(v => v.CurrentMessage != null);
        if (view != null) { Selection.activeGameObject = view.CurrentMessage.gameObject; EditorGUIUtility.PingObject(Selection.activeGameObject); }
        else Status = "현재 표시 중인 홍보 안내가 없습니다.";
    }
    static void OnPlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) CaptureBindings();
        if (state == PlayModeStateChange.EnteredPlayMode) BindRuntimeObjects();
        if (state == PlayModeStateChange.EnteredEditMode && Enabled) EditorApplication.delayCall += ApplyPending;
    }
    public static void ApplyPending()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        applying = true;
        var applied = new List<Edit>(); var scenes = new HashSet<Scene>();
        try
        {
            foreach (var edit in Data.edits)
            {
                if (!GlobalObjectId.TryParse(edit.id, out var id)) continue;
                var obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id); var go = Owner(obj);
                if (obj == null || go == null || go.scene.path != edit.scene || !IsTutorial(go.scene)) continue;
                Undo.RecordObject(obj, "튜토리얼 플레이 UI 편집 적용");
                if (edit.kind == "presentation" && obj is TutorialPublicityExplanationView view) view.SetMessagePresentation(edit.panel, edit.message, edit.json);
                else if (edit.kind == "overlay" && obj is TutorialPublicityExplanationView overlay) overlay.SetOverlayPresentation(edit.panel, edit.json);
                else if (edit.kind == "content" && obj is JcTutorialGuideView guide) guide.SetContentEdits(edit.json);
                else if (edit.kind == "property")
                {
                    var so = new SerializedObject(obj); var p = so.FindProperty(edit.property);
                    if (p == null || !Allowed(obj, edit.property) || !ApplyValue(p, edit.value)) continue;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                else continue;
                EditorUtility.SetDirty(obj); PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
                EditorSceneManager.MarkSceneDirty(go.scene); scenes.Add(go.scene); applied.Add(edit);
            }
            if (SaveScenes)
            {
                var saved = new HashSet<string>(); foreach (var scene in scenes) if (EditorSceneManager.SaveScene(scene)) saved.Add(scene.path);
                applied.RemoveAll(e => !saved.Contains(e.scene));
            }
            foreach (var edit in applied) Data.edits.Remove(edit);
            Write(); Status = applied.Count + "개 항목 씬 적용" + (SaveScenes ? "·저장 완료" : " 완료 (Ctrl+S로 씬 저장)") + "; 대기 " + PendingCount;
        }
        catch (Exception e) { Status = "적용 중단·저장 기록 유지: " + e.Message; Debug.LogException(e); }
        finally { applying = false; }
    }
    public static Value ReadValue(SerializedProperty p)
    {
        var v = new Value { type = (int)p.propertyType };
        switch (p.propertyType)
        {
            case SerializedPropertyType.String: v.text = p.stringValue; break;
            case SerializedPropertyType.Boolean: v.text = p.boolValue ? "1" : "0"; break;
            case SerializedPropertyType.Integer: case SerializedPropertyType.Enum: v.text = p.intValue.ToString(CultureInfo.InvariantCulture); break;
            case SerializedPropertyType.Float: v.numbers = new[] { p.floatValue }; break;
            case SerializedPropertyType.Vector2: var a = p.vector2Value; v.numbers = new[] { a.x, a.y }; break;
            case SerializedPropertyType.Vector3: var b = p.vector3Value; v.numbers = new[] { b.x, b.y, b.z }; break;
            case SerializedPropertyType.Quaternion: var q = p.quaternionValue; v.numbers = new[] { q.x, q.y, q.z, q.w }; break;
            case SerializedPropertyType.Color: var c = p.colorValue; v.numbers = new[] { c.r, c.g, c.b, c.a }; break;
            case SerializedPropertyType.Vector4: var d = p.vector4Value; v.numbers = new[] { d.x, d.y, d.z, d.w }; break;
            default: return null;
        }
        return v;
    }
    public static bool ApplyValue(SerializedProperty p, Value v)
    {
        if (v == null || (int)p.propertyType != v.type) return false;
        var n = v.numbers;
        switch (p.propertyType)
        {
            case SerializedPropertyType.String: p.stringValue = v.text; break;
            case SerializedPropertyType.Boolean: p.boolValue = v.text == "1"; break;
            case SerializedPropertyType.Integer: case SerializedPropertyType.Enum: p.intValue = int.Parse(v.text, CultureInfo.InvariantCulture); break;
            case SerializedPropertyType.Float: p.floatValue = n[0]; break;
            case SerializedPropertyType.Vector2: p.vector2Value = new Vector2(n[0], n[1]); break;
            case SerializedPropertyType.Vector3: p.vector3Value = new Vector3(n[0], n[1], n[2]); break;
            case SerializedPropertyType.Quaternion: p.quaternionValue = new Quaternion(n[0], n[1], n[2], n[3]); break;
            case SerializedPropertyType.Color: p.colorValue = new Color(n[0], n[1], n[2], n[3]); break;
            case SerializedPropertyType.Vector4: p.vector4Value = new Vector4(n[0], n[1], n[2], n[3]); break;
            default: return false;
        }
        return true;
    }
}

public sealed class JcTutorialUiPlaySaveWindow : EditorWindow
{
    [MenuItem("JC/튜토리얼/UI 플레이 편집 저장")]
    public static void Open() => GetWindow<JcTutorialUiPlaySaveWindow>("튜토리얼 UI 저장");
    void OnEnable() { minSize = new Vector2(420, 420); JcTutorialUiPlaySave.Enabled = true; EditorApplication.update += Repaint; }
    void OnDisable() { EditorApplication.update -= Repaint; }
    void OnGUI()
    {
        EditorGUILayout.HelpBox("플레이 중 Inspector·Scene 도구로 직접 수정한 UI를 즉시 보관합니다. 플레이 종료 시 원래 튜토리얼 씬에 적용합니다. 게임 진행·런타임 계층 이동은 저장하지 않습니다.", MessageType.Info);
        JcTutorialUiPlaySave.Enabled = EditorGUILayout.Toggle("직접 편집 자동 보관", JcTutorialUiPlaySave.Enabled);
        JcTutorialUiPlaySave.SaveScenes = EditorGUILayout.Toggle("종료 후 씬 파일도 저장", JcTutorialUiPlaySave.SaveScenes);
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            if (GUILayout.Button("현재 홍보 안내 문구 선택")) JcTutorialUiPlaySave.SelectCurrentMessage();
            if (GUILayout.Button("선택한 UI 지금 보관")) JcTutorialUiPlaySave.SaveSelection();
        }
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            if (GUILayout.Button("보관한 편집값 씬에 적용")) JcTutorialUiPlaySave.ApplyPending();
        EditorGUILayout.LabelField("대기 항목", JcTutorialUiPlaySave.PendingCount.ToString());
        EditorGUILayout.HelpBox(JcTutorialUiPlaySave.Status, MessageType.None);
        EditorGUILayout.LabelField("보관 파일", JcTutorialUiPlaySave.SavePath, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.HelpBox("홍보 안내와 TutorialDimRegion·TutorialHighlightBorder(각 변 포함)의 배치·색상·활성 상태는 단계별로 저장됩니다. 전투 시트에서 가져오는 문구는 기존 TutorialUiSheet Inspector에서 원문을 수정하세요. Inspector에서 입력을 확정(Enter 또는 다른 항목 클릭)하면 자동 보관됩니다.", MessageType.None);
    }
}
#endif
