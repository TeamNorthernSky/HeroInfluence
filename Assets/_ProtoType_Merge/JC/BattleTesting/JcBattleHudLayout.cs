using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace JC.BattleTesting
{
    // 테스트씬에서 확정한 표시값만 공유합니다. 실제 전투의 컴포넌트/이벤트 참조는 유지합니다.
    public sealed class JcBattleHudLayout : ScriptableObject
    {
        [Serializable] public sealed class Item
        {
            public string path;
            public Vector2 anchorMin, anchorMax, pivot, position, size;
            public bool rect;
            public float fontSize;
        }
        [Serializable] public sealed class Data { public Item[] items; }
        [Tooltip("테스트씬에서 추출한 기존 UI의 경로별 레이아웃입니다.")]
        [TextArea] public string layoutJson;
        [Tooltip("기존 좌측 HUD의 반투명 패널 스프라이트입니다.")]
        public Sprite surface;
        [Tooltip("기존 좌측 HUD의 선택 테두리 스프라이트입니다.")]
        public Sprite border;
        [Tooltip("미습득 스킬의 아이콘 중앙에 표시할 자물쇠입니다.")]
        public Sprite lockedSkillSprite;
        public Sprite skillButtonFace;
        [Tooltip("미습득 스킬 행에 표시하는 회청색 배경입니다.")] public Sprite lockedFace;
        [Range(.5f, 1.5f), Tooltip("우상단 메뉴·자동·배속·도주 버튼 묶음의 크기입니다. 0.8이면 기존 대비 가로·세로 80%로 표시합니다.")]
        public float topRightButtonScale = 1.04f;
        [Range(.5f, 1f), Tooltip("버튼 높이는 유지하면서 가로 폭만 조절합니다. 0.9이면 현재 크기의 90% 너비로 표시합니다.")]
        public float topRightButtonWidthRatio = .9f;
        [Min(0f), Tooltip("우상단 버튼 묶음의 오른쪽 여백입니다. 턴슬롯 본체의 오른쪽 테두리에 맞춥니다.")]
        public float topRightButtonRightInset = 16f;

        public void ApplyTopRightButtons(Scene scene)
        {
            var group = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<RectTransform>(true))
                .FirstOrDefault(r => r.name == "TopRightButtons");
            if (group != null)
            {
                float scale = Mathf.Clamp(topRightButtonScale, .5f, 1.5f);
                group.localScale = new Vector3(scale * Mathf.Clamp(topRightButtonWidthRatio, .5f, 1f), scale, scale);
                group.anchoredPosition = new Vector2(-Mathf.Max(0f, topRightButtonRightInset), group.anchoredPosition.y);
            }
        }
        public void Apply(Scene scene)
        {
            ApplyTopRightButtons(scene);
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RectTransform>(true)).ToArray();
            string Path(Transform t)=>t.parent==null?t.name:Path(t.parent)+"/"+t.name;
            var presentation = BattlePresentationController.FindInScene(scene);
            var map=all.GroupBy(r=>Path(r)).ToDictionary(g=>g.Key,g=>g.First());
            bool hasStoredHud = map.ContainsKey("BattleSceneCanvas/BottomPanel/SkillDescriptionPanel/DescriptionTab");
            foreach(var item in JsonUtility.FromJson<Data>(layoutJson).items)
            {
                // 프레임 분리 후에도 저장된 레이아웃은 이미지가 아닌 시각 묶음에 적용합니다.
                string visualPath = item.path.EndsWith("/SkillIcon", StringComparison.Ordinal)
                    ? item.path.Substring(0, item.path.Length - "SkillIcon".Length) + "IconVisual"
                    : item.path.EndsWith("/PortraitArea/Portrait", StringComparison.Ordinal)
                        ? item.path.Substring(0, item.path.Length - "Portrait".Length) + "PortraitVisual"
                        : item.path;
                if(!map.TryGetValue(visualPath,out var r) && !map.TryGetValue(item.path,out r))continue;
                // 턴 순서는 각 전투씬에서 확정한 세로 배치를 사용합니다.
                // 과거 HUD JSON의 상단 가로형 위치/크기로 루트와 슬롯을 덮어쓰지 않습니다.
                if (r.GetComponentInParent<TurnOrderUI>(true) != null) continue;
                // 새 HUD는 각 씬에 배치한 버튼 크기와 패널 안쪽 여백을 사용합니다.
                if (hasStoredHud && (item.path.StartsWith("BattleSceneCanvas/TopRightButtons/", StringComparison.Ordinal) ||
                    item.path.StartsWith("BattleSceneCanvas/BottomPanel/", StringComparison.Ordinal))) continue;
                if(item.rect)
                {
                    r.anchorMin=item.anchorMin;r.anchorMax=item.anchorMax;r.pivot=item.pivot;r.sizeDelta=item.size;
                    if (presentation != null && presentation.skillPanel == r)
                        presentation.ApplySkillPanelLayoutPosition(item.position);
                    else r.anchoredPosition=item.position;
                }
                var text=r.GetComponent<TMP_Text>();
                if(text!=null&&item.fontSize>0){text.fontSize=item.fontSize;text.enableAutoSizing=true;text.fontSizeMax=item.fontSize;text.fontSizeMin=Mathf.Max(15,item.fontSize-4);}
            }
            ApplyBattlePanel(scene);
        }

        // 모든 진입 경로에서 동일한 시각 규격을 사용합니다. 기존 버튼과 이벤트 참조는 유지합니다.
        public void ApplyBattlePanel(Scene scene)
        {
            var panel = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RectTransform>(true))
                .FirstOrDefault(r => r.name == "BottomPanel" && r.Find("HeroSkillPanel") != null);
            if (panel == null) return;
            // 선택 순환광의 색/주기/두 갈래 연출은 씬의 원본 설정을 유지합니다.
            RectTransform Rect(string path) => panel.Find(path) as RectTransform;
            void Place(string path, float x, float y, float width, float height, bool stretch = false)
            {
                var r = Rect(path); if (r == null) return;
                r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(stretch ? 1 : 0, 1);
                r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(width, height);
            }
            Place("ActorInfoPanel/PortraitArea", 16, -16, 132, 132);
            Place("ActorInfoPanel/PortraitArea/PortraitVisual", 0, 0, 132, 132);
            Place("ActorInfoPanel/StatsArea", 164, -16, -180, 148, true);
            Place("ActorInfoPanel/StatsArea/ActorName", 0, 0, 0, 36, true);
            var hpLabel = Rect("ActorInfoPanel/StatsArea/HpLabel");
            bool inlineHp = hpLabel != null && !hpLabel.gameObject.activeSelf;
            Place("ActorInfoPanel/StatsArea/HpValue", inlineHp ? 0 : 36, -42, inlineHp ? 0 : -36, 32, true);
            Place("ActorInfoPanel/StatsArea/HpBar", 0, -76, 0, 14, true);
            Place("ActorInfoPanel/StatsArea/IpIcon", 0, -102, 32, 32);
            Place("ActorInfoPanel/StatsArea/IpValue", 38.2f, -101.8f, 55.2f, 32.2f);
            var ipText = Rect("ActorInfoPanel/StatsArea/IpValue")?.GetComponent<TMP_Text>();
            if (ipText != null)
            {
                ipText.fontSize = ipText.fontSizeMax = 19.3f * 1.15f;
                ipText.fontSizeMin = 15.3f * 1.15f;
                ipText.enableAutoSizing = true;
                ipText.alignment = TextAlignmentOptions.MidlineLeft;
                ipText.margin = Vector4.zero;
            }
            var rank = Rect("ActorInfoPanel/StatsArea/Rank");
            if (rank != null)
            {
                rank.anchorMin = rank.anchorMax = new Vector2(1, 1); rank.pivot = new Vector2(1, 1);
                // Keep the lower edge fixed while growing upward by 15%.
                rank.anchoredPosition = new Vector2(0, -98.9f); rank.sizeDelta = new Vector2(112, 39.1f);
                var text = rank.GetComponent<TMP_Text>(); text.alignment = TextAlignmentOptions.Midline; text.margin = Vector4.zero;
                text.fontSize = 20; text.fontSizeMin = 16; text.fontSizeMax = 20; text.enableAutoSizing = true;
                // 배경이 글자를 덮지 않도록 같은 부모의 앞쪽 형제로 둡니다.
                var bg = Rect("ActorInfoPanel/StatsArea/RankBadgeBackground");
                if (bg != null)
                {
                    bg.anchorMin = rank.anchorMin; bg.anchorMax = rank.anchorMax;
                    bg.pivot = rank.pivot; bg.anchoredPosition = rank.anchoredPosition; bg.sizeDelta = rank.sizeDelta;
                }
            }
            const float height = 88, gap = 10;
            Place("CoreSkillPanel", 0, -204, 0, height, true);
            Place("CoreSkillPanel/Button_Skill_2", 16, 0, -32, height, true);
            Place("CoreSkillPanel/Title", 104, 0, -120, height, true);
            var title = Rect("CoreSkillPanel/Title").GetComponent<TMP_Text>();
            title.alignment = TextAlignmentOptions.MidlineLeft;
            Place("HeroSkillPanel", 0, -302, 0, height * 4 + gap * 3, true);
            string[] paths = { "CoreSkillPanel/Button_Skill_2", "HeroSkillPanel/Button_Skill", "HeroSkillPanel/HeroSkillSlot_2", "HeroSkillPanel/HeroSkillSlot_3", "HeroSkillPanel/HeroSkillSlot_4" };
            var face = skillButtonFace ? skillButtonFace : surface;
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i]; var row = Rect(path); if (row == null) continue;
                if(i > 0) Place(path, 16, -(height + gap) * (i - 1), -32, height, true);
                Place(path + "/IconVisual", 8, -10, 68, 68);
                if (i > 0)
                {
                    Place(path + "/SkillName", 88, 0, -130, height, true);
                    var text = Rect(path + "/SkillName").GetComponent<TMP_Text>();
                    text.alignment = TextAlignmentOptions.MidlineLeft; text.enableWordWrapping = false;
                    text.enableAutoSizing = true; text.fontSize = text.fontSizeMax = 22; text.fontSizeMin = 16;
                }
                var baseImage = row.GetComponent<Image>();
                if(baseImage != null) { baseImage.overrideSprite = null; baseImage.material = null; baseImage.sprite = face; baseImage.type = Image.Type.Sliced; baseImage.preserveAspect = false; baseImage.color = skillButtonFace ? Color.white : new Color(.035f,.065f,.13f,1f); }
                foreach (var toggle in row.GetComponents<ToggleButton>())
                    toggle.SetVisualSprites(face, face);
                var button = row.GetComponent<Button>();
                if (button != null && button.transition == Selectable.Transition.SpriteSwap)
                    button.spriteState = new SpriteState { highlightedSprite = face, pressedSprite = face, selectedSprite = face, disabledSprite = face };
                foreach(var image in row.GetComponentsInChildren<Image>(true))
                {
                    if(image.name == "InsetShadow" || image.name == "InsetHighlight") { image.enabled = false; continue; }
                    if (image.name == "LockedOverlay" && lockedFace != null)
                    {
                        // 회색 행은 아이콘 아래의 LockedFace, 자물쇠만 아이콘 위에 표시합니다.
                        image.enabled = row.Find("LockedFace") == null;
                        if (image.enabled) image.color = new Color(.13f, .15f, .19f, .36f);
                    }
                    if(image.name == "BaseHoverOverlay")
                    {
                        image.overrideSprite = null; image.sprite = surface; image.material = null;
                        image.type = Image.Type.Sliced; image.preserveAspect = false;
                        image.color = new Color(.4f,.62f,.78f,.12f);
                    }
                    if(image.name == "AvailableSkillEffect" || image.name == "SelectedSkillFrame" || image.name == "SkillButtonFrame" || image.name == "SelectedOverlay")
                    {
                        // 사용 가능 스윕은 버튼 바탕의 실루엣을 따라 흐르게 합니다.
                        if (image.name == "AvailableSkillEffect")
                        {
                            image.overrideSprite = null;
                            image.sprite = face;
                            image.type = Image.Type.Simple;
                        }
                        if (image.name != "AvailableSkillEffect" && image.name != "SelectedSkillFrame")
                        {
                            image.overrideSprite = null;
                            image.sprite = border;
                            image.type = Image.Type.Sliced;
                        }
                        image.preserveAspect = false;
                        var r = image.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
                    }
                }
                var padlock = Rect(path + "/LockedOverlay/Padlock");
                if (padlock != null && lockedSkillSprite != null)
                {
                    // 기존 잠금 묶음의 활성/비활성 판정은 그대로 사용합니다.
                    foreach (Transform part in padlock) part.gameObject.SetActive(false);
                    padlock.anchorMin = padlock.anchorMax = new Vector2(0, 1);
                    padlock.pivot = new Vector2(.5f, .5f);
                    padlock.anchoredPosition = new Vector2(42, -44);
                    padlock.sizeDelta = new Vector2(46.8f, 57.2f);
                    var lockImage = padlock.GetComponent<Image>() ?? padlock.gameObject.AddComponent<Image>();
                    lockImage.overrideSprite = null; lockImage.sprite = lockedSkillSprite;
                    lockImage.material = null; lockImage.color = Color.white;
                    lockImage.type = Image.Type.Simple; lockImage.preserveAspect = true;
                    lockImage.raycastTarget = false;
                    // 원본 은색 스프라이트는 유지하고 알파 실루엣에만 검은 외곽선을 더합니다.
                    var lockOutline = padlock.GetComponent<Outline>() ?? padlock.gameObject.AddComponent<Outline>();
                    lockOutline.effectColor = Color.black;
                    lockOutline.effectDistance = new Vector2(1.5f, -1.5f);
                    lockOutline.useGraphicAlpha = true;

                }
            }
            var desc = Rect("SkillDescriptionPanel");
            ConfigureDescriptionScroll(desc);
            if(desc != null) { desc.anchorMin = Vector2.zero; desc.anchorMax = Vector2.one; desc.pivot = Vector2.zero; desc.anchoredPosition = new Vector2(0,12); desc.sizeDelta = new Vector2(0,-724); }
            var descriptionTitle = Rect("SkillDescriptionPanel/Title")?.GetComponent<TMP_Text>();
            if (descriptionTitle != null && Rect("SkillDescriptionPanel/DescriptionTab") != null)
            {
                LayoutDescriptionTitle(descriptionTitle);
                ConfigureTags(desc);
            }
        }

        public static void LayoutDescriptionTitle(TMP_Text title)
        {
            if (title == null || !(title.rectTransform.parent is RectTransform parent) ||
                !(parent.Find("DescriptionTab") is RectTransform tab)) return;
            // 탭의 오른쪽 3슬라이스는 사선 몰딩 전용이며 글자 여백에 포함하지 않습니다.
            const float padding = 18f, bevel = 32f, cornerClearance = 10f;
            var face = parent.Find("DescriptionFace") as RectTransform;
            float faceRight = face != null ? face.offsetMax.x + parent.rect.width : parent.rect.width;
            float max = Mathf.Max(1f, faceRight - tab.anchoredPosition.x - cornerClearance);
            title.enableWordWrapping = false;
            title.enableAutoSizing = true;
            title.fontSizeMax = 26;
            title.fontSizeMin = 15;
            title.fontSize = 26;
            float width = Mathf.Clamp(Mathf.Ceil(title.GetPreferredValues(title.text, Mathf.Infinity, Mathf.Infinity).x)
                + 2 * padding + bevel, Mathf.Min(155f, max), max);
            tab.sizeDelta = new Vector2(width, 44);
            var r = title.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = tab.anchoredPosition + new Vector2(padding, -3);
            r.sizeDelta = new Vector2(Mathf.Max(1f, width - 2 * padding - bevel), 38);
            title.overflowMode = TextOverflowModes.Ellipsis;
            title.margin = Vector4.zero;
            title.alignment = TextAlignmentOptions.Midline;
        }

        private static void ConfigureTags(RectTransform parent)
        {
            string[] names = { "EffectTag", "RangeTag", "TargetTag" };
            const float margin = 24, gap = 6;
            for (int i = 0; i < names.Length; i++)
            {
                if (!(parent.Find(names[i]) is RectTransform tag)) continue;
                // 폭에서 양끝 여백과 두 칸의 간격을 뺀 뒤 3등분합니다.
                tag.anchorMin = new Vector2(i / 3f, 1);
                tag.anchorMax = new Vector2((i + 1) / 3f, 1);
                tag.pivot = new Vector2(0, 1);
                tag.sizeDelta = new Vector2(-(2 * margin + 2 * gap) / 3, 30);
                tag.anchoredPosition = new Vector2(margin + i * (gap + tag.sizeDelta.x), -52);
                var label = tag.GetComponentInChildren<TMP_Text>(true);
                if (label == null) continue;
                var r = label.rectTransform;
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
                r.offsetMin = new Vector2(4, 1); r.offsetMax = new Vector2(-4, -1);
                label.alignment = TextAlignmentOptions.Midline;
                label.enableAutoSizing = true; label.fontSizeMax = 19; label.fontSizeMin = 13;
            }
        }

        public static void FitMaskedArt(Image image)
        {
            if (image == null || image.sprite == null || !(image.transform.parent is RectTransform mask) ||
                mask.GetComponent<Mask>() == null || !image.TryGetComponent<AspectRatioFitter>(out var fit)) return;
            image.preserveAspect = false;
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = image.sprite.rect.width / Mathf.Max(1, image.sprite.rect.height);
        }

        private static void ConfigureDescriptionScroll(RectTransform description)
        {
            if (description == null) return;
            var body = description.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == "Body");
            if (body == null) return;
            var viewport = description.Find("DescriptionViewport") as RectTransform;
            if (viewport == null)
            {
                viewport = new GameObject("DescriptionViewport", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect)).GetComponent<RectTransform>();
                viewport.SetParent(description, false);
            }
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.pivot = Vector2.zero;
            bool hasEscape = description.Find("RunButton") != null;
            viewport.offsetMin = new Vector2(24, hasEscape ? 70 : 12);
            viewport.offsetMax = new Vector2(-24, -90);
            if (body.rectTransform.parent != viewport) body.rectTransform.SetParent(viewport, false);
            body.rectTransform.anchorMin = new Vector2(0, 1);
            body.rectTransform.anchorMax = new Vector2(1, 1);
            body.rectTransform.pivot = new Vector2(0, 1);
            body.rectTransform.sizeDelta = new Vector2(0, body.rectTransform.sizeDelta.y);
            body.rectTransform.anchoredPosition = Vector2.zero;
            body.margin = new Vector4(2, 2, 2, 2);
            body.enableWordWrapping = true;
            body.enableAutoSizing = false; body.fontSize = 21; body.overflowMode = TextOverflowModes.Overflow; body.raycastTarget = true;
            var fitter = body.GetComponent<ContentSizeFitter>() ?? body.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.GetComponent<ScrollRect>() ?? viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = body.rectTransform; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24;
        }
    }
}
