using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class HeroEffectLibraryWindow : EditorWindow
{
    private const string EditorRoot = "Assets/RenderFX/_Support/Editor/";
    [SerializeField] private int selectedHero;
    [SerializeField] private bool showJusticeCommon;
    private ScrollView content;

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
        rootVisualElement.Q<Button>("archive").clicked += () => SelectFolder("Assets/RenderFX/_Archive/HeroSkill");
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
        if (selectedHero == 0)
        {
            var tabs = new VisualElement();
            tabs.AddToClassList("section-tabs");
            var skills = new Button(() => { showJusticeCommon = false; Rebuild(); }) { text = "스킬별 효과" };
            var common = new Button(() => { showJusticeCommon = true; Rebuild(); }) { text = "공통 이동·복귀" };
            skills.EnableInClassList("selected", !showJusticeCommon);
            common.EnableInClassList("selected", showJusticeCommon);
            tabs.Add(skills);
            tabs.Add(common);
            content.Add(tabs);
            if (showJusticeCommon) { BuildJusticeCommon(); return; }
        }
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
        var bindings = HeroEffectUsage.GetBindings(skill);
        var otherVariant = HeroEffectUsage.GetBindings(skill % 10 == 1 ? skill - 1 : skill + 1);
        var visible = bindings.Where(item => !item.Role.Contains("common_hit") &&
            !(selectedHero == 0 && IsJusticeMovement(item))).ToArray();
        foreach (var binding in visible.OrderByDescending(item => IsVariantDifference(item, otherVariant)))
        {
            bool differs = IsVariantDifference(binding, otherVariant);
            string caption = string.Join(" / ", binding.Role.Split('/').Select(HeroEffectUsage.Caption).Distinct());
            AddAsset(column, caption + (differs ? " · 일반/강화 차이" : ""), binding.Prefab, differs);
        }
        if (visible.Length == 0 && selectedHero == 0)
            column.Add(new HelpBox("이 스킬은 공통 이동·복귀 효과를 사용합니다.", HelpBoxMessageType.None));
        if (bindings.Count == 0) column.Add(new HelpBox("현재 연결된 이펙트가 없습니다.", HelpBoxMessageType.Info));

        var paths = AssetDatabase.GetDependencies(visible.Select(item => AssetDatabase.GetAssetPath(item.Prefab)).ToArray(), true);
        var colors = new Foldout { text = "색상·크기 조절 대상", value = skill / 10 == 201 };
        foreach (string path in paths.OrderBy(path => path))
        {
            if (!path.StartsWith("Assets/RenderFX/HeroSkill/")) continue;
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset is ScriptableObject && asset.GetType().Name.Contains("Preset")) AddAsset(colors, "프리셋", asset);
            else if (asset is GameObject prefab && prefab.GetComponent<ParticleSystem>() != null)
                AddAsset(colors, "파티클 색상", asset);
        }
        column.Add(colors);
        var materials = new Foldout { text = "연결된 재질", value = false };
        foreach (string path in paths.Where(path => path.StartsWith("Assets/RenderFX/HeroSkill/") && path.EndsWith(".mat")).OrderBy(path => path))
            AddAsset(materials, "재질", AssetDatabase.LoadAssetAtPath<Material>(path));
        column.Add(materials);
        var hits = new Foldout { text = "공통 피격", value = false };
        foreach (var binding in bindings.Where(item => item.Role.Contains("common_hit")))
            AddAsset(hits, HeroEffectUsage.Caption(binding.Role), binding.Prefab);
        column.Add(hits);
        return column;
    }

    private static bool IsJusticeMovement(HeroEffectUsage.Binding binding)
    {
        string path = AssetDatabase.GetAssetPath(binding.Prefab);
        return path.Contains("FX_Justice_Common_MoveTrail_") || path.Contains("FX_Justice_Common_ReturnDust_");
    }

    private static bool IsVariantDifference(HeroEffectUsage.Binding binding, System.Collections.Generic.List<HeroEffectUsage.Binding> other)
        => !other.Any(item => item.Prefab == binding.Prefab && item.Role == binding.Role);

    private void BuildJusticeCommon()
    {
        var card = new VisualElement();
        card.AddToClassList("skill-card");
        card.Add(new Label("저스티스 · 모든 스킬 공통 이동·복귀"));
        var bindings = Enumerable.Range(1, 4).SelectMany(slot => HeroEffectUsage.GetBindings(1000 + slot * 10))
            .Where(IsJusticeMovement).GroupBy(item => item.Prefab).Select(group => group.First()).ToArray();
        foreach (var binding in bindings)
            AddAsset(card, AssetDatabase.GetAssetPath(binding.Prefab).Contains("ReturnDust") ? "복귀 먼지" : "이동 궤적", binding.Prefab);
        var paths = AssetDatabase.GetDependencies(bindings.Select(item => AssetDatabase.GetAssetPath(item.Prefab)).ToArray(), true);
        var settings = new Foldout { text = "공통 이동·복귀 색상·크기 조절 대상", value = false };
        foreach (string path in paths.OrderBy(path => path))
        {
            if (!path.StartsWith("Assets/RenderFX/HeroSkill/")) continue;
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset is ScriptableObject && asset.GetType().Name.Contains("Preset")) AddAsset(settings, "프리셋", asset);
            else if (asset is GameObject prefab && prefab.GetComponent<ParticleSystem>() != null) AddAsset(settings, "파티클 색상", asset);
            else if (asset is Material) AddAsset(settings, "재질", asset);
        }
        card.Add(settings);
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
