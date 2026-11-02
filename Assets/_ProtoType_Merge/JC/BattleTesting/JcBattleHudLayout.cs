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
        [Range(.5f, 1f), Tooltip("우상단 메뉴·자동·배속·도주 버튼 묶음의 크기입니다. 0.8이면 기존 대비 가로·세로 80%로 표시합니다.")]
        public float topRightButtonScale = .8f;

        public void ApplyTopRightButtons(Scene scene)
        {
            var group = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<RectTransform>(true))
                .FirstOrDefault(r => r.name == "TopRightButtons");
            if (group != null)
                group.localScale = Vector3.one * Mathf.Clamp(topRightButtonScale, .5f, 1f);
        }
        public void Apply(Scene scene)
        {
            ApplyTopRightButtons(scene);
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RectTransform>(true)).ToArray();
            string Path(Transform t)=>t.parent==null?t.name:Path(t.parent)+"/"+t.name;
            var presentation = BattlePresentationController.FindInScene(scene);
            var map=all.GroupBy(r=>Path(r)).ToDictionary(g=>g.Key,g=>g.First());
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
            var description=all.FirstOrDefault(r=>r.name=="SkillDescriptionPanel");
            if(description!=null)
            {
                var body=description.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="Body");
                if(body!=null)
                {
                    var existing = description.Find("DescriptionViewport");
                    var go=existing != null ? existing.gameObject : new GameObject("DescriptionViewport",typeof(RectTransform),typeof(RectMask2D),typeof(ScrollRect));
                    var vp=(RectTransform)go.transform;vp.SetParent(description,false);vp.anchorMin=Vector2.zero;vp.anchorMax=Vector2.one;vp.pivot=Vector2.zero;vp.anchoredPosition=new Vector2(20,12);vp.sizeDelta=new Vector2(-40,-116);
                    body.rectTransform.SetParent(vp,false);body.enableAutoSizing=false;body.fontSize=21;body.overflowMode=TextOverflowModes.Overflow;body.raycastTarget=true;
                    var fit=body.GetComponent<ContentSizeFitter>()??body.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
                    var scroll=go.GetComponent<ScrollRect>();scroll.viewport=vp;scroll.content=body.rectTransform;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=24;
                }
            }
            string[] panels={"Background","PortraitArea","StatsArea","CoreSkillPanel","HeroSkillPanel","SkillDescriptionPanel","Button_Skill","Button_Skill_2","HeroSkillSlot_2","HeroSkillSlot_3","HeroSkillSlot_4","SkillButtonFrame","BaseHoverOverlay","SelectedOverlay","SelectedSkillFrame","EffectTag","RangeTag","TargetTag"};
            foreach(var r in all.Where(r=>Path(r).Contains("/BottomPanel/")||r.name=="BottomPanel")) Style(r,panels);
            // 기존 씬에서 하단 패널 이름이 달라도 스킬 설명과 같은 HUD 루트만 스타일링합니다.
            if(description!=null&&description.parent!=null)
                foreach(var r in description.parent.GetComponentsInChildren<RectTransform>(true))Style(r,panels);
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
            Image Decoration(Transform parent, string name, Sprite sprite, Color tint)
            {
                var child = parent.Find(name);
                var image = child != null ? child.GetComponent<Image>() : null;
                if (image == null)
                {
                    var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(parent, false); image = go.GetComponent<Image>();
                }
                var r = image.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
                r.offsetMin = r.offsetMax = Vector2.zero;
                image.sprite = sprite; image.type = Image.Type.Sliced; image.preserveAspect = false;
                image.color = tint; image.raycastTarget = false;
                return image;
            }
            Place("ActorInfoPanel/PortraitArea", 16, -16, 132, 132);
            Place("ActorInfoPanel/PortraitArea/PortraitVisual", 0, 0, 132, 132);
            Place("ActorInfoPanel/StatsArea", 164, -16, -180, 148, true);
            Place("ActorInfoPanel/StatsArea/ActorName", 0, 0, 0, 36, true);
            Place("ActorInfoPanel/StatsArea/HpLabel", 0, -44, 32, 26);
            Place("ActorInfoPanel/StatsArea/HpValue", 36, -44, -36, 26, true);
            Place("ActorInfoPanel/StatsArea/HpBar", 0, -78, 0, 10, true);
            Place("ActorInfoPanel/StatsArea/IpIcon", 0, -101.8f, 32.2f, 32.2f);
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
                text.fontSize = 22; text.fontSizeMin = 17; text.fontSizeMax = 22; text.enableAutoSizing = true;
                // 배경이 글자를 덮지 않도록 같은 부모의 앞쪽 형제로 둡니다.
                var bg = Decoration(rank.parent, "RankBadgeBackground", surface, new Color(.025f,.055f,.13f,.98f));
                bg.rectTransform.anchorMin = rank.anchorMin; bg.rectTransform.anchorMax = rank.anchorMax;
                bg.rectTransform.pivot = rank.pivot; bg.rectTransform.anchoredPosition = rank.anchoredPosition; bg.rectTransform.sizeDelta = rank.sizeDelta;
                Decoration(bg.transform, "Border", border, new Color(.95f,.73f,.08f,1));
                bg.transform.SetSiblingIndex(rank.GetSiblingIndex()); rank.SetAsLastSibling();
            }
            const float height = 88, gap = 10;
            Place("CoreSkillPanel", 0, -204, 0, height, true);
            Place("CoreSkillPanel/Button_Skill_2", 16, 0, -32, height, true);
            Place("CoreSkillPanel/Title", 104, 0, -120, height, true);
            var title = Rect("CoreSkillPanel/Title").GetComponent<TMP_Text>();
            title.text = "젬 파워"; title.alignment = TextAlignmentOptions.MidlineLeft;
            Place("HeroSkillPanel", 0, -302, 0, height * 4 + gap * 3, true);
            string[] paths = { "CoreSkillPanel/Button_Skill_2", "HeroSkillPanel/Button_Skill", "HeroSkillPanel/HeroSkillSlot_2", "HeroSkillPanel/HeroSkillSlot_3", "HeroSkillPanel/HeroSkillSlot_4" };
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
                if(baseImage != null) { baseImage.overrideSprite = null; baseImage.material = null; baseImage.sprite = surface; baseImage.type = Image.Type.Sliced; baseImage.preserveAspect = false; baseImage.color = new Color(.035f,.065f,.13f,1f); }
                foreach (var toggle in row.GetComponents<ToggleButton>())
                    toggle.SetVisualSprites(surface, surface);
                var button = row.GetComponent<Button>();
                if (button != null && button.transition == Selectable.Transition.SpriteSwap)
                    button.spriteState = new SpriteState { highlightedSprite = surface, pressedSprite = surface, selectedSprite = surface, disabledSprite = surface };
                foreach(var image in row.GetComponentsInChildren<Image>(true))
                {
                    if(image.name == "InsetShadow" || image.name == "InsetHighlight") { image.enabled = false; continue; }
                    if(image.name == "LockedOverlay") image.color = new Color(.13f,.15f,.19f,.36f);
                    if(image.name == "BaseHoverOverlay")
                    {
                        image.overrideSprite = null; image.sprite = surface; image.material = null;
                        image.type = Image.Type.Sliced; image.preserveAspect = false;
                        image.color = new Color(.4f,.62f,.78f,.12f);
                    }
                    if(image.name == "AvailableSkillEffect" || image.name == "SelectedSkillFrame" || image.name == "SkillButtonFrame" || image.name == "SelectedOverlay")
                    {
                        // 스윕/선택 효과는 씬에 등록된 원본 스프라이트·재질·Image 타입을 유지합니다.
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
                var outline = Decoration(row, "HudButtonOutline", border, new Color(.12f,.68f,.93f,.85f));
                outline.transform.SetAsLastSibling();
                var padlock = Rect(path + "/LockedOverlay/Padlock");
                if (padlock != null && lockedSkillSprite != null)
                {
                    // 기존 잠금 묶음의 활성/비활성 판정은 그대로 사용합니다.
                    foreach (Transform part in padlock) part.gameObject.SetActive(false);
                    padlock.anchorMin = padlock.anchorMax = new Vector2(0, 1);
                    padlock.pivot = new Vector2(.5f, .5f);
                    padlock.anchoredPosition = new Vector2(42, -44);
                    padlock.sizeDelta = new Vector2(44, 54);
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
            if(desc != null) { desc.anchorMin = Vector2.zero; desc.anchorMax = Vector2.one; desc.pivot = Vector2.zero; desc.anchoredPosition = new Vector2(0,12); desc.sizeDelta = new Vector2(0,-724); }
        }

        void Style(RectTransform r,string[] names)
        {
            if(!names.Contains(r.name))return;var image=r.GetComponent<Image>();if(image==null)return;
            // 선택 테두리는 실제 선택 상태의 순환광 재질과 스프라이트를 유지합니다.
            if (r.name == "SelectedSkillFrame") return;
            bool outline=r.name=="SelectedOverlay"||r.name=="SelectedSkillFrame"||r.name=="SkillButtonFrame";
            image.sprite=outline?border:surface;image.type=Image.Type.Sliced;image.material=null;
            image.color=outline?new Color(.48f,.73f,.88f,.85f):r.name=="Background"?new Color(.045f,.065f,.095f,.92f):new Color(.1f,.14f,.19f,.92f);
            if(r.name=="BaseHoverOverlay")image.color=new Color(.4f,.62f,.78f,.16f);
        }
    }
}
