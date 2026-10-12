using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>기존 효과를 참조하는 카탈로그 씬을 최초 생성합니다. 기존 씬은 덮어쓰지 않습니다.</summary>
public static class CanvasButtonCatalogBuilder
{
    public const string ScenePath = "Assets/_ProtoType_Merge/JC/CanvasVFX/VFX_Canvas_Catalog.unity";
    private const string UiCode = "Assets/_ProtoType_Merge/JC/Scripts/UI/";
    private static TMP_FontAsset font;
    private static readonly Color Ink = new Color(.88f, .92f, .98f);
    private static readonly Color Muted = new Color(.58f, .67f, .79f);

    [MenuItem("JC/Canvas VFX/카탈로그 열기 또는 최초 생성")]
    public static void OpenOrCreate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Play 종료 후 카탈로그를 여세요.");
        if (File.Exists(ScenePath))
        {
            if (SceneManager.GetActiveScene().path == ScenePath) return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return;
        }
        Build();
    }

    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(ScenePath))
            throw new InvalidOperationException("Edit 모드의 최초 생성만 허용합니다. 기존 카탈로그는 직접 편집하세요.");
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_SharedAsset/Font/NotoSansKR-Regular SDF.asset");
            if (font == null) throw new InvalidOperationException("기존 한국어 폰트가 없습니다.");
            var canvasGo = new GameObject("VFX_Canvas_Catalog", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var background = Rect("Background", canvasGo.transform, 0, 0, 1920, 1080);
            background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one;
            background.offsetMin = Vector2.zero; background.offsetMax = Vector2.zero;
            background.gameObject.AddComponent<Image>().color = new Color(.028f, .044f, .075f);
            background.GetComponent<Image>().raycastTarget = false;
            Label(canvasGo.transform, "CANVAS VFX / BUTTONS", 56, 28, 1200, 30, 19, new Color(.35f, .7f, 1));
            Label(canvasGo.transform, "버튼 효과 카탈로그", 56, 64, 1350, 64, 42, Ink);
            Label(canvasGo.transform, "기존 효과를 직접 비교하고, 같은 코드를 참고해 다른 UI에 적용하세요.", 60, 134, 1300, 40, 22, Muted);
            var controller = canvasGo.AddComponent<CanvasButtonCatalog>();
            controller.status = Label(canvasGo.transform, "입력 가능 · 버튼에 마우스를 올리거나 클릭하세요", 60, 982, 1300, 36, 22, Muted);
            UnityEventTools.AddPersistentListener(Control(canvasGo.transform, "입력 가능 / 불가", 1390, 76, 220).onClick, controller.ToggleInputs);
            UnityEventTools.AddPersistentListener(Control(canvasGo.transform, "전체 초기화", 1630, 76, 230).onClick, controller.ResetAll);

            string[] ids = { "BTN-01", "BTN-02", "BTN-03", "BTN-04", "BTN-05", "BTN-06", "BTN-07" };
            string[] names = { "호버 강조", "호버 확대", "클릭 눌림", "클릭 흔들림", "사용 가능 스윕", "선택 ON 순환광", "조합 예시" };
            string[] descriptions = {
                "마우스를 올릴 때 글로우와 빛띠가 시작됩니다.", "마우스를 올리면 확대되고 벗어나면 복귀합니다.",
                "클릭하면 작아졌다 원래 크기로 돌아옵니다.", "클릭하면 짧게 흔들린 뒤 제자리로 돌아옵니다.",
                "사용 가능한 상태에서 빛띠와 글로우가 반복됩니다.", "클릭 ON: 외곽 순환광 + 내부 스윕. 다시 클릭 OFF.",
                "호버 확대 + 클릭 눌림 + 선택 ON 순환광." };
            string[] hints = { "HoverOverlayTint / ActiveToggle", "ButtonEffectScalePulse", "ButtonEffectScalePulse", "ButtonEffectShake", "BattleSkillSweepSettings", "BattleSkillSweepSettings", "ScalePulse + BattleSkillSweepSettings" };
            var samples = new CanvasButtonCatalogSample[7];
            for (int i = 0; i < samples.Length; i++)
            {
                float x = 60 + (i % 4) * 454, y = 218 + (i / 4) * 374;
                var card = Rect(ids[i] + "_" + names[i], canvasGo.transform, x, y, 438, 354);
                var panel = card.gameObject.AddComponent<Image>(); panel.color = new Color(.066f, .092f, .137f); panel.raycastTarget = false;
                Label(card, ids[i], 22, 16, 370, 28, 19, new Color(.35f, .7f, 1));
                Label(card, names[i], 22, 51, 390, 42, 29, Ink);
                Label(card, descriptions[i], 22, 104, 394, 66, 19, Muted);
                samples[i] = Sample(card, ids[i], names[i], descriptions[i], i);
                Label(card, "조절: " + hints[i], 22, 287, 394, 52, 17, Muted);
            }
            var guide = Rect("HowTo", canvasGo.transform, 1422, 592, 438, 354);
            Label(guide, "사용 방법", 22, 18, 394, 42, 28, Ink);
            Label(guide, "1  Play에서 호버 · 클릭 비교\n2  Hierarchy에서 BTN 예제 선택\n3  Inspector에서 효과 참조 복사\n4  Edit 모드에서 값 조절 후 씬 저장\n\nPlay 중 변경은 자동 저장되지 않습니다.\n기존 게임 버튼은 변경하지 않습니다.", 22, 84, 394, 252, 20, Muted);
            controller.samples = samples;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var backgroundCamera = new GameObject("CatalogCamera", typeof(Camera)).GetComponent<Camera>();
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.backgroundColor = new Color(.028f, .044f, .075f);
            backgroundCamera.cullingMask = 0; // Overlay Canvas가 UI를 그리며 카메라는 Display 1의 배경만 출력합니다.
            backgroundCamera.depth = -100;
            backgroundCamera.targetDisplay = 0;
            backgroundCamera.allowHDR = false;
            backgroundCamera.allowMSAA = false;
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static CanvasButtonCatalogSample Sample(Transform parent, string id, string title, string description, int index)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_SharedAsset/UIButtonBase.prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
        go.name = id + "_Sample";
        var rect = (RectTransform)go.transform;
        Place(rect, 74, 185, 290, 92);
        rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition += new Vector2(145, -46);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI_Sprite/UI_OptionModal/UI_button_confirm.png");
        go.GetComponent<Image>().sprite = sprite;
        var button = go.GetComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors; colors.normalColor = colors.highlightedColor = colors.pressedColor = colors.selectedColor = Color.white;
        colors.disabledColor = new Color(.42f, .42f, .42f, 1); button.colors = colors;
        var label = go.GetComponentInChildren<TMP_Text>(true);
        label.font = font; label.fontSize = 24; label.color = Color.white; label.text = "미리보기";
        var hover = go.transform.Find("BaseHoverOverlay").gameObject;
        var hoverImage = hover.GetComponent<Image>(); hoverImage.sprite = sprite; hoverImage.raycastTarget = false;
        if (index == 0)
        {
            const string materialPath = "Assets/_ProtoType_Merge/JC/CanvasVFX/CatalogHoverSweep.mat";
            var hoverMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (hoverMaterial == null)
            {
                hoverMaterial = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_SharedAsset/UIHoverGlowSweep.mat"));
                hoverMaterial.SetTexture("_GleamTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_SharedAsset/UIGleamSweep.png"));
                hoverMaterial.SetFloat("_GleamSoftness", .025f);
                AssetDatabase.CreateAsset(hoverMaterial, materialPath);
            }
            hoverImage.material = hoverMaterial;
            Set(go.GetComponent<HoverOverlayTint>(), "overlayMaterial", hoverMaterial);
        }
        Set(go.GetComponent<ButtonEffectActiveToggle>(), "toggleOnHover", index == 0);
        hover.SetActive(false);
        if (index == 1 || index == 2 || index == 6)
        {
            var scale = go.AddComponent<ButtonEffectScalePulse>();
            Set(scale, "pulseOnHover", index != 2); Set(scale, "pulseOnClick", index != 1);
        }
        if (index == 3) go.AddComponent<ButtonEffectShake>();
        var sample = go.AddComponent<CanvasButtonCatalogSample>();
        sample.effectId = id; sample.displayName = title; sample.description = description;
        sample.targetButton = button; sample.hoverOverlay = hover;
        sample.references = "씬: " + ScenePath + "\n예제: " + id + "_Sample\n기반: Assets/_SharedAsset/UIButtonBase.prefab\n" + UiCode + "ButtonEffectController.cs\n";
        if (index == 0) sample.references += UiCode + "HoverOverlayTint.cs\n" + UiCode + "ButtonEffectActiveToggle.cs\n" + UiCode + "SweepCooldownReset.cs\nAssets/_SharedAsset/UIHoverGlowSweep.mat\nAssets/_SharedAsset/UIHoverGlowSweep.shader";
        if (index == 1 || index == 2 || index == 6) sample.references += UiCode + "ButtonEffectScalePulse.cs\n";
        if (index == 3) sample.references += UiCode + "ButtonEffectShake.cs";
        if (index >= 4)
        {
            var settings = go.AddComponent<BattleSkillSweepSettings>();
            settings.gleamSoftness = .025f;
            settings.selectedOrbit = index != 4;
            settings.gleamTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_SharedAsset/UIGleamSweep.png");
            settings.showAvailableEffect = index != 5;
            var available = Overlay(go.transform, "AvailableEffect", sprite, "Assets/_SharedAsset/UIHoverGlowSweep.mat");
            var selected = Overlay(go.transform, "SelectedFrame", sprite, "Assets/_SharedAsset/UIHoverOutlineSelected.mat");
            var visual = go.AddComponent<BattleSkillButtonVisual>();
            Set(visual, "availableEffect", available); Set(visual, "selectedFrame", selected); Set(visual, "sharedSettings", settings);
            sample.stateVisual = visual; sample.toggleSelectionOnClick = index != 4;
            selected.gameObject.SetActive(false); available.gameObject.SetActive(index != 5);
            label.transform.SetAsLastSibling();
            sample.references += UiCode + "Battle/BattleSkillButtonVisual.cs\n" + UiCode + "Battle/BattleSkillSweepSettings.cs\nAssets/_SharedAsset/UIHoverGlowSweep.shader\nAssets/_SharedAsset/UIHoverOutlineSelected.shader\n카탈로그 상태 공급: CanvasButtonCatalogSample (실제 게임에서는 사용 가능/선택 상태를 전달)";
        }
        foreach (var graphic in go.GetComponentsInChildren<Graphic>(true)) if (graphic.gameObject != go) graphic.raycastTarget = false;
        // 추가 컴포넌트와 기존 프리팹의 모든 변경은 이 씬 인스턴스에만 기록합니다.
        foreach (var component in go.GetComponentsInChildren<Component>(true))
            if (component != null && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        PrefabUtility.RecordPrefabInstancePropertyModifications(go);
        return sample;
    }

    private static Image Overlay(Transform parent, string name, Sprite sprite, string materialPath)
    {
        var rt = Rect(name, parent, 0, 0, 0, 0);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var image = rt.gameObject.AddComponent<Image>(); image.sprite = sprite; image.material = AssetDatabase.LoadAssetAtPath<Material>(materialPath); image.raycastTarget = false;
        return image;
    }

    private static Button Control(Transform parent, string title, float x, float y, float width)
    {
        var rect = Rect(title, parent, x, y, width, 54);
        var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.13f, .23f, .36f);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var text = Label(rect, title, 0, 0, width, 54, 21, Ink); text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static TMP_Text Label(Transform parent, string text, float x, float y, float w, float h, float size, Color color)
    {
        var rect = Rect(text.Split('\n')[0], parent, x, y, w, h);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = font; label.text = text; label.fontSize = size; label.color = color;
        label.raycastTarget = false; label.enableWordWrapping = true; label.alignment = TextAlignmentOptions.TopLeft;
        return label;
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; rt.SetParent(parent, false); Place(rt, x, y, w, h); return rt;
    }

    private static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
    }

    private static void Set(UnityEngine.Object target, string name, object value)
    {
        var serialized = new SerializedObject(target); var p = serialized.FindProperty(name);
        if (value is bool b) p.boolValue = b;
        else if (value is Color c) p.colorValue = c;
        else p.objectReferenceValue = value as UnityEngine.Object;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
