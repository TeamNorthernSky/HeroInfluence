using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class HeroEffectLibraryWindow : EditorWindow
{
    private const string EditorRoot = "Assets/RenderFX/_Support/Editor/";
    [SerializeField] private int selectedHero;
    private ScrollView content;
    private readonly System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<HeroEffectUsage.Binding>> heroBindings = new();
    private readonly System.Collections.Generic.HashSet<GameObject> commonPrefabs = new();
    private string[] commonDependencies = new string[0];

    [MenuItem("RenderFX/히어로 이펙트 관리")]
    public static void Open()
    {
        var window = GetWindow<HeroEffectLibraryWindow>();
        window.titleContent = new GUIContent("히어로 이펙트");
        window.minSize = new Vector2(820f, 560f);
        window.Show();
    }

    public void CreateGUI()
    {
        rootVisualElement.Clear();
        var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(EditorRoot + "HeroEffectLibraryWindow.uxml");
        if (layout == null) return;
        layout.CloneTree(rootVisualElement);
        var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(EditorRoot + "HeroEffectLibraryWindow.uss");
        if (style != null) rootVisualElement.styleSheets.Add(style);
        content = rootVisualElement.Q<ScrollView>("content");
        var tabs = rootVisualElement.Q<VisualElement>("heroTabs");
        for (int i = 0; i < HeroEffectUsage.Heroes.Length; i++)
        {
            int index = i;
            var tab = new Button(() => { selectedHero = index; Rebuild(); }) { text = HeroEffectUsage.HeroLabels[i], name = "hero" + i };
            tabs.Add(tab);
        }
        rootVisualElement.Q<Button>("refresh").clicked += () => { HeroEffectUsage.Invalidate(); Rebuild(); };
        rootVisualElement.Q<Button>("archive").clicked += () => SelectFolder("Assets/RenderFX/HeroSkill/Legacy");
        Rebuild();
    }

    private void OnProjectChange()
    {
        if (content == null || EditorApplication.isCompiling) return;
        HeroEffectUsage.Invalidate();
        Rebuild();
    }

    private void Rebuild()
    {
        if (content == null) return;
        selectedHero = Mathf.Clamp(selectedHero, 0, 3);
        for (int i = 0; i < 4; i++)
            rootVisualElement.Q<Button>("hero" + i).EnableInClassList("selected", i == selectedHero);
        content.Clear();
        heroBindings.Clear();
        commonPrefabs.Clear();
        for (int slot = 1; slot <= 4; slot++)
            for (int variant = 0; variant <= 1; variant++)
            {
                int id = (selectedHero + 1) * 1000 + slot * 10 + variant;
                heroBindings[id] = HeroEffectUsage.GetBindings(id);
            }
        foreach (var group in heroBindings.SelectMany(pair => pair.Value.Select(binding =>
                     new { Family = pair.Key / 10, binding.Prefab })).GroupBy(item => item.Prefab))
            if (group.Select(item => item.Family).Distinct().Count() > 1) commonPrefabs.Add(group.Key);
        commonDependencies = AssetDatabase.GetDependencies(commonPrefabs.Select(AssetDatabase.GetAssetPath).ToArray(), true);
        if (commonPrefabs.Count > 0) BuildCommon();
        for (int slot = 0; slot < 4; slot++)
        {
            int family = (selectedHero + 1) * 1000 + (slot + 1) * 10;
            string folder = "Assets/RenderFX/HeroSkill/" + HeroEffectUsage.Heroes[selectedHero] + "/" + HeroEffectUsage.Skills[selectedHero][slot];
            var card = new VisualElement();
            card.AddToClassList("skill-card");
            var header = new VisualElement();
            header.AddToClassList("skill-header");
            header.Add(new Label(HeroEffectUsage.SkillLabels[selectedHero][slot] + "  ·  " + family));
            header.Add(new Button(() => SelectSkillFolder(family, folder)) { text = "현재 스킬 폴더" });
            card.Add(header);
            var variants = new VisualElement();
            variants.AddToClassList("variants");
            variants.Add(BuildVariant(family, "일반"));
            variants.Add(BuildVariant(family + 1, "강화"));
            card.Add(variants);
            content.Add(card);
        }
    }

    private VisualElement BuildVariant(int skill, string label)
    {
        var column = new VisualElement();
        column.AddToClassList("variant");
        var title = new Label(label);
        title.AddToClassList("variant-title");
        column.Add(title);
        var bindings = heroBindings[skill];
        var otherVariant = heroBindings[skill % 10 == 1 ? skill - 1 : skill + 1];
        var visible = bindings.Where(item => !commonPrefabs.Contains(item.Prefab)).ToArray();
        foreach (var binding in visible.OrderByDescending(item => IsVariantDifference(item, otherVariant)))
        {
            bool differs = IsVariantDifference(binding, otherVariant);
            string caption = string.Join(" / ", binding.Role.Split('/').Select(HeroEffectUsage.Caption).Distinct());
            AddAsset(column, caption + (differs ? " · 일반/강화 차이" : ""), binding.Prefab, differs);
        }
        if (skill / 10 >= 202 && skill / 10 <= 204)
            AddLuminaParts(column, visible);
        if (visible.Length == 0)
            column.Add(new HelpBox(bindings.Count > 0 ? "전용 이펙트 없음 · 위 공통 효과 사용" : "현재 연결된 이펙트가 없습니다.", HelpBoxMessageType.None));

        var paths = AssetDatabase.GetDependencies(visible.Select(item => AssetDatabase.GetAssetPath(item.Prefab)).ToArray(), true)
            .Except(commonDependencies).ToArray();
        var colors = new Foldout { text = "색상·크기 조절 대상", value = skill / 10 == 201 };
        foreach (string path in paths.OrderBy(path => path))
        {
            if (!path.StartsWith("Assets/RenderFX/HeroSkill/")) continue;
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset is ScriptableObject && asset.GetType().Name.Contains("Preset")) AddAsset(colors, "프리셋", asset);
            else if (asset is GameObject prefab && prefab.GetComponent<ParticleSystem>() != null)
                AddAsset(colors, "파티클 색상", asset);
        }
        if (colors.contentContainer.childCount > 0) column.Add(colors);
        var materials = new Foldout { text = "연결된 재질", value = false };
        foreach (string path in paths.Where(path => path.StartsWith("Assets/RenderFX/HeroSkill/") && path.EndsWith(".mat")).OrderBy(path => path))
            AddAsset(materials, "재질", AssetDatabase.LoadAssetAtPath<Material>(path));
        if (materials.contentContainer.childCount > 0) column.Add(materials);
        return column;
    }

    private static void AddLuminaParts(VisualElement column, HeroEffectUsage.Binding[] bindings)
    {
        var found = new System.Collections.Generic.HashSet<string>();
        var visited = new System.Collections.Generic.HashSet<string>();
        void Visit(GameObject root)
        {
            string rootPath = AssetDatabase.GetAssetPath(root);
            if (string.IsNullOrEmpty(rootPath) || !visited.Add(rootPath)) return;
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null) continue;
                string type = component.GetType().Name;
                string[] fields = type == "ChainLightningVfx" ? new[] { "mainPartPrefab", "chainPartPrefab" } :
                    type == "ChainLightningVisual" ? new[] { "visualPrefab" } :
                    type == "SolarPrismVfx" || type == "PrismExplosionVfx" ? new[] { "flightPartPrefab", "impactPartPrefab" } :
                    type == "SolarPrismVisual" ? new[] { "flashPrefab", "meteorFlightPrefab" } :
                    type == "PrismExplosionVisual" ? new[] { "summonPrefab", "flightPrefab", "chargeStarsPrefab" } : new string[0];
                var so = new SerializedObject(component);
                foreach (string field in fields)
                {
                    var value = so.FindProperty(field)?.objectReferenceValue;
                    var prefab = value as GameObject ?? (value as Component)?.gameObject;
                    if (prefab == null) continue;
                    string path = AssetDatabase.GetAssetPath(prefab);
                    if (string.IsNullOrEmpty(path) || path == rootPath) continue;
                    string label = field == "impactPartPrefab" ? "폭발·피격" : field == "flightPartPrefab" || field == "flightPrefab" || field == "meteorFlightPrefab" ? "비행·투사체" : field == "flashPrefab" || field == "summonPrefab" || field == "chargeStarsPrefab" ? "차징·빛" : field == "chainPartPrefab" ? "연쇄 번개" : field == "mainPartPrefab" ? "첫 번개·피격" : "번개 시각 효과";
                    if (type == "ChainLightningVisual")
                    {
                        int kind = so.FindProperty("kind")?.enumValueIndex ?? 0;
                        label = kind == 2 ? "피격 · 실행 중 색상 적용" : kind == 1 ? "번개 · 실행 중 색상 적용" : "발사 · 실행 중 색상 적용";
                    }
                    if (found.Add(label + "|" + path)) AddAsset(column, label, prefab);
                    Visit(prefab);
                }
                if (type == "ChainLightningVisual")
                    AddAsset(column, "색상 설정 · 이 프리팹의 Chain Lightning Visual 컴포넌트", root);
            }
        }
        foreach (var binding in bindings) Visit(binding.Prefab);
    }

    private static bool IsVariantDifference(HeroEffectUsage.Binding binding, System.Collections.Generic.List<HeroEffectUsage.Binding> other)
        => !other.Any(item => item.Prefab == binding.Prefab && item.Role == binding.Role);

    private void BuildCommon()
    {
        var card = new VisualElement();
        card.AddToClassList("skill-card");
        var header = new VisualElement();
        header.AddToClassList("skill-header");
        header.Add(new Label("공통 효과 · " + HeroEffectUsage.HeroLabels[selectedHero]));
        card.Add(header);
        foreach (var prefab in commonPrefabs.OrderBy(AssetDatabase.GetAssetPath))
        {
            var uses = heroBindings.Where(pair => pair.Value.Any(item => item.Prefab == prefab)).ToArray();
            string caption = string.Join(" / ", uses.SelectMany(pair => pair.Value.Where(item => item.Prefab == prefab))
                .SelectMany(item => item.Role.Split('/')).Select(HeroEffectUsage.Caption).Distinct());
            bool basic = uses.Any(pair => pair.Key % 10 == 0), enhanced = uses.Any(pair => pair.Key % 10 == 1);
            caption += basic && enhanced ? " · 일반·강화 공용" : basic ? " · 일반" : " · 강화";
            var slots = uses.Select(pair => (pair.Key % 1000) / 10 - 1).Distinct().OrderBy(slot => slot).ToArray();
            caption += " (" + (slots.Length == 4 ? "모든 스킬" : string.Join(", ", slots.Select(slot => HeroEffectUsage.SkillLabels[selectedHero][slot]))) + ")";
            AddAsset(card, caption, prefab);
        }
        var settings = new Foldout { text = "공통 효과 색상·크기 조절 대상", value = false };
        foreach (string path in commonDependencies.OrderBy(path => path))
        {
            if (!path.StartsWith("Assets/RenderFX/HeroSkill/")) continue;
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset is ScriptableObject && asset.GetType().Name.Contains("Preset")) AddAsset(settings, "프리셋", asset);
            else if (asset is GameObject prefab && prefab.GetComponent<ParticleSystem>() != null) AddAsset(settings, "파티클 색상", asset);
            else if (asset is Material) AddAsset(settings, "재질", asset);
        }
        if (settings.contentContainer.childCount > 0) card.Add(settings);
        content.Add(card);
    }

    private static void AddAsset(VisualElement parent, string caption, UnityEngine.Object asset, bool differs = false)
    {
        if (asset == null) return;
        var row = new VisualElement();
        row.AddToClassList("asset-row");
        row.EnableInClassList("variant-difference", differs);
        row.Add(new Label(caption) { tooltip = caption });
        var controls = new VisualElement();
        controls.AddToClassList("asset-controls");
        var field = new ObjectField { objectType = asset.GetType(), allowSceneObjects = false, value = asset };
        field.tooltip = AssetDatabase.GetAssetPath(asset);
        field.RegisterValueChangedCallback(_ => field.SetValueWithoutNotify(asset));
        controls.Add(field);
        controls.Add(new Button(() => { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }) { text = "선택" });
        controls.Add(new Button(() => AssetDatabase.OpenAsset(asset)) { text = "열기" });
        row.Add(controls);
        parent.Add(row);
    }

    private static void SelectFolder(string path)
    {
        var folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
        if (folder == null) return;
        Selection.activeObject = folder;
        EditorGUIUtility.PingObject(folder);
    }

    private static void SelectSkillFolder(int family, string preferred)
    {
        if (AssetDatabase.IsValidFolder(preferred)) { SelectFolder(preferred); return; }
        var shared = HeroEffectUsage.GetBindings(family).FirstOrDefault(item =>
            AssetDatabase.GetAssetPath(item.Prefab).StartsWith("Assets/RenderFX/HeroSkill/"));
        if (shared != null)
            SelectFolder(System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(shared.Prefab)).Replace('\\', '/'));
    }
}
