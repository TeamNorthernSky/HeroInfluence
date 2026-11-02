using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Local cache metadata only; no runtime assets or changes to async compilation settings.
[FilePath("Library/JC/EditorVfxPreparation.asset", FilePathAttribute.Location.ProjectFolder)]
public sealed class JcEditorVfxPreparationState : ScriptableSingleton<JcEditorVfxPreparationState>
{
    public string fingerprint, completedAt;
    public void Store(string value)
    { fingerprint = value; completedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"); Save(true); }
}

public sealed class JcEditorVfxPreparation : EditorWindow
{
    sealed class Pass { public Material material; public int index; public string label; }
    readonly List<Material> copies = new List<Material>();
    readonly List<Pass> passes = new List<Pass>();
    readonly List<string> failures = new List<string>();
    string fingerprint, status = "상태 확인이 필요합니다.";
    int cursor, verificationRetries;
    bool running, requested, stale = true;
    double requestedAt;
    Vector2 scroll;
    public bool IsPreparing => running;
    public string Status => status;
    public int PassCount => passes.Count;
    public int FailureCount => failures.Count;

    [MenuItem("Tools/JC/에디터 VFX 사전 준비")]
    public static void Open() => GetWindow<JcEditorVfxPreparation>("VFX 사전 준비");
    void OnEnable()
    {
        EditorApplication.projectChanged += MarkStale;
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += Cancel;
    }
    void OnDisable()
    {
        Cancel();
        EditorApplication.projectChanged -= MarkStale;
        EditorApplication.update -= Tick;
        AssemblyReloadEvents.beforeAssemblyReload -= Cancel;
        ReleaseCopies();
    }
    void MarkStale() { stale = true; status = "변경 감지 — 갱신 확인 필요"; Repaint(); }
    void OnFocus() { if (!running) MarkStale(); }
    void ReleaseCopies()
    {
        foreach (var material in copies) if (material != null) DestroyImmediate(material);
        copies.Clear(); passes.Clear();
    }
    static string[] Sources()
    {
        var roots = new HashSet<string>(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));
        foreach (string type in new[] { "t:SkillPresentationCatalog", "t:EffectRegistry" })
            foreach (string guid in AssetDatabase.FindAssets(type)) roots.Add(AssetDatabase.GUIDToAssetPath(guid));
        foreach (string folder in new[] { "Assets/RenderFX", "Assets/_ProtoType_Merge/JC/CanvasVFX" })
            if (AssetDatabase.IsValidFolder(folder))
                foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder })) roots.Add(AssetDatabase.GUIDToAssetPath(guid));
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("/Resources/BattleEffects/")) roots.Add(path);
        }
        roots.Add("ProjectSettings/GraphicsSettings.asset");
        roots.Add("ProjectSettings/QualitySettings.asset");
        roots.Add("ProjectSettings/EditorBuildSettings.asset");
        return roots.OrderBy(p => p, StringComparer.Ordinal).ToArray();
    }
    static string Fingerprint(string[] roots)
    {
        var text = new StringBuilder(Application.unityVersion).Append('|').Append(SystemInfo.graphicsDeviceType)
            .Append('|').Append(EditorUserBuildSettings.activeBuildTarget).Append('|').Append(QualitySettings.GetQualityLevel());
        foreach (var keyword in Shader.enabledGlobalKeywords.OrderBy(k => k.name)) text.Append('|').Append(keyword.name);
        foreach (string path in roots)
        {
            text.Append('|').Append(path).Append(':').Append(AssetDatabase.GetAssetDependencyHash(path));
            if (path.StartsWith("ProjectSettings/", StringComparison.Ordinal) && System.IO.File.Exists(path))
                text.Append(System.IO.File.ReadAllText(path));
        }
        return Hash128.Compute(text.ToString()).ToString();
    }
    public void CheckStatus()
    {
        if (running || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        ReleaseCopies(); failures.Clear(); cursor = 0;
        var roots = Sources(); fingerprint = Fingerprint(roots);
        foreach (string path in AssetDatabase.GetDependencies(roots.Where(p => p.StartsWith("Assets/", StringComparison.Ordinal)).ToArray(), true)
            .Where(p => AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(Material) ||
                (!p.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) && AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(GameObject))).OrderBy(p => p))
            foreach (var source in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
            {
                if (source.shader == null || !source.shader.isSupported) { failures.Add(path + ": 셰이더 없음/미지원"); continue; }
                var copy = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
                copies.Add(copy);
                if (copy.passCount == 0) failures.Add(path + ": 렌더 패스 없음");
                for (int i = 0; i < copy.passCount; i++)
                    passes.Add(new Pass { material = copy, index = i, label = path + " / " + source.name + " / " + i });
            }
        int missing = passes.Count(p => !ShaderUtil.IsPassCompiled(p.material, p.index));
        stale = false;
        bool matches = fingerprint == JcEditorVfxPreparationState.instance.fingerprint;
        status = failures.Count > 0 ? "확인 필요: " + failures.Count + "개 오류" :
            passes.Count == 0 ? "준비 대상 없음" :
            matches && missing == 0 ? "준비 완료 — 수집한 머티리얼/현재 키워드 조합" :
            "갱신 필요 — 미컴파일 " + missing + "/" + passes.Count + " 패스";
        Repaint();
    }
    public void BeginPreparation()
    {
        if (running || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
        CheckStatus();
        if (passes.Count == 0) return;
        cursor = 0; verificationRetries = 0; requested = false; running = true;
        status = "사전 준비 중";
    }
    public void Cancel()
    {
        if (!running) return;
        running = false; requested = false; stale = true;
        status = "취소됨 — 미준비 조합은 기존 비동기 방식으로 처리됩니다.";
        Repaint();
    }
    void Tick()
    {
        double until = EditorApplication.timeSinceStartup + .008;
        for (int i = 0; running && i < 32 && EditorApplication.timeSinceStartup < until; i++)
        {
            int before = cursor;
            ProcessPass();
            if (cursor == before) break;
        }
    }
    void ProcessPass()
    {
        if (!running) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) { Cancel(); return; }
        try
        {
            // Cached passes need no render-frame delay. Bound the scan to keep the editor responsive.
            double budgetEnd = EditorApplication.timeSinceStartup + .008;
            while (cursor < passes.Count && ShaderUtil.IsPassCompiled(passes[cursor].material, passes[cursor].index)
                && EditorApplication.timeSinceStartup < budgetEnd) { cursor++; requested = false; }
            if (cursor >= passes.Count)
            {
                var missing = passes.Where(p => !ShaderUtil.IsPassCompiled(p.material, p.index)).ToArray();
                if (missing.Length > 0 && verificationRetries++ < 2)
                { cursor = 0; requested = false; status = "미준비 패스 재확인: " + missing.Length; return; }
                running = false;
                foreach (var missingPass in missing) failures.Add(missingPass.label + ": 최종 컴파일 확인 실패");
                foreach (var shader in copies.Select(m => m.shader).Distinct())
                    foreach (var message in ShaderUtil.GetShaderMessages(shader))
                        if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                            failures.Add(shader.name + ": " + message.message);
                bool unchanged = Fingerprint(Sources()) == fingerprint;
                if (failures.Count == 0 && unchanged && passes.All(p => ShaderUtil.IsPassCompiled(p.material, p.index)))
                {
                    JcEditorVfxPreparationState.instance.Store(fingerprint);
                    status = "준비 완료 — " + passes.Count + " 패스 (수집한 머티리얼/현재 키워드 조합)";
                    stale = false;
                }
                else { stale = true; status = unchanged ? "일부 준비 실패 — 상세 내용을 확인하세요." : "준비 중 변경 발생 — 갱신 필요"; }
                Repaint(); return;
            }
            var pass = passes[cursor];
            if (!requested)
            {
                if (ShaderUtil.IsPassCompiled(pass.material, pass.index)) { cursor++; return; }
                ShaderUtil.CompilePass(pass.material, pass.index, false);
                requested = true; requestedAt = EditorApplication.timeSinceStartup;
            }
            if (ShaderUtil.IsPassCompiled(pass.material, pass.index)) { cursor++; requested = false; }
            else if (EditorApplication.timeSinceStartup - requestedAt > 120)
            { failures.Add(pass.label + ": 120초 대기 초과"); cursor++; requested = false; }
            Repaint();
        }
        catch (Exception ex)
        {
            failures.Add((cursor < passes.Count ? passes[cursor].label : "완료 검사") + ": " + ex.Message);
            if (running) { cursor++; requested = false; }
            else { status = "준비 실패 — 상세 내용을 확인하세요."; stale = true; }
            Repaint();
        }
    }
    void OnGUI()
    {
        EditorGUILayout.LabelField("에디터 VFX 사전 준비", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("선택 실행 도구입니다. 미준비/변경된 효과는 기존 비동기 방식으로 실행됩니다. 게임 실행과 빌드 설정을 바꾸지 않습니다.", MessageType.Info);
        EditorGUILayout.HelpBox("준비 범위: 빌드 씬·VFX 카탈로그·머티리얼 의존성의 저장된 조합과 현재 전역 키워드. 실행 중 생성되는 조합 및 다른 라이팅/렌더 상태는 포함을 보장하지 않습니다.", MessageType.None);
        EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
        if (stale && !running) EditorGUILayout.LabelField("변경 및 컴파일 상태를 다시 확인하세요.");
        EditorGUILayout.LabelField("마지막 성공", JcEditorVfxPreparationState.instance.completedAt ?? "없음");
        using (new EditorGUI.DisabledScope(running || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("변경 및 컴파일 상태 확인")) CheckStatus();
            if (GUILayout.Button("에디터 VFX 사전 준비")) BeginPreparation();
        }
        if (running)
        {
            EditorGUILayout.LabelField(cursor + " / " + passes.Count + " 패스");
            if (GUILayout.Button("취소")) Cancel();
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (string failure in failures) EditorGUILayout.HelpBox(failure, MessageType.Warning);
        EditorGUILayout.EndScrollView();
    }
}
