using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JC.Tutorial
{
    public sealed partial class JcTutorialGuideView
    {
        public enum ActionHint { None, ScrollRight, HoverEnemy, HoverBadge, ClickSkill }
        private ActionHint actionHint;
        private RectTransform hintTarget, hintRoot, hintCard, hintIcon, hintFrame;
        private RectTransform[] hintLines;
        private TMP_Text hintText, hintKey;
        private JcTutorialHintIcon iconGraphic;
        private CanvasGroup hintGroup;
        private float hintStarted;
        // JC | 2026-11-09 | 오클릭 교정은 기존 수렴 Graphic 두 개를 안내 Canvas로 잠시 옮겨 재사용합니다.
        // 종료 시 원래 계층/Rect를 복구하므로 이동력 강조와 씬 참조는 바뀌지 않습니다.
        private RectTransform redirectedSkill;
        private float redirectStarted;
        private bool redirectWhenHidden;
        private bool redirectShowCard;
        private GlowPlacement[] redirectedPlacements;
        private sealed class GlowPlacement
        {
            public RectTransform rect;
            public Transform parent;
            public int sibling;
            public Vector2 min, max, pivot, size;
            public Vector3 position, scale;
            public Quaternion rotation;
            public GlowPlacement(RectTransform value)
            {
                rect=value; parent=value.parent; sibling=value.GetSiblingIndex(); min=value.anchorMin; max=value.anchorMax;
                pivot=value.pivot; size=value.sizeDelta; position=value.localPosition; scale=value.localScale; rotation=value.localRotation;
            }
            public void Restore()
            {
                if(rect==null)return;
                rect.gameObject.SetActive(false); rect.SetParent(parent,false); rect.SetSiblingIndex(sibling);
                rect.anchorMin=min; rect.anchorMax=max; rect.pivot=pivot; rect.sizeDelta=size;
                rect.localPosition=position; rect.localScale=scale; rect.localRotation=rotation;
            }
        }
        public void RestartSkillRedirect(RectTransform target, bool allowWhenHidden = false, bool showHintWhenHidden = false)
        {
            if(target==null || convergingOutlines==null)return;
            SetActionHint(ActionHint.ClickSkill,target);
            if(hintRoot==null)return;
            if(redirectedPlacements==null)
            {
                redirectedPlacements=new GlowPlacement[convergingOutlines.Length];
                for(int i=0;i<convergingOutlines.Length;i++)
                    if(convergingOutlines[i]!=null)redirectedPlacements[i]=new GlowPlacement(convergingOutlines[i].rectTransform);
                foreach(var placement in redirectedPlacements)if(placement!=null)
                {
                    var rect=placement.rect; rect.SetParent(hintRoot,false);
                    rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f); rect.localScale=Vector3.one; rect.localRotation=Quaternion.identity;
                }
            }
            redirectedSkill=target; redirectStarted=Time.unscaledTime;
            redirectWhenHidden=allowWhenHidden;
            redirectShowCard=showHintWhenHidden;
        }
        private void StopSkillRedirect()
        {
            redirectedSkill=null;
            redirectWhenHidden=false;
            redirectShowCard=false;
            if(redirectedPlacements!=null)foreach(var placement in redirectedPlacements)placement?.Restore();
            redirectedPlacements=null;
        }
        // JC | 2026-11-04 | 턴 배지 외곽을 기준으로 튜토리얼 안내와 강조 윤곽을 정렬합니다.
        private Image badgeOutline;
        private Sprite badgeCircle, badgeBox;
        // 초상화 기준으로 파란 슬롯 프레임 바깥에 간격을 두고 두 안내 원을 맞춥니다.
        internal const float BadgeInnerScale = 1.28f;
        internal const float BadgeOuterScale = 1.38f;

        // 안내 캔버스 좌표로 실제 배지 테두리를 따라간다. 같은 사각형으로 디밍 구멍도 계산한다.
        public RectTransform ShowBadgeOutline(TurnSlotUI slot, bool circle)
        {
            if (hintRoot == null || slot == null)
            {
                if (badgeOutline != null) badgeOutline.enabled = false;
                return null;
            }
            var target = slot.transform.Find("PortraitWhiteOutline") as RectTransform;
            if (target == null) target = slot.transform as RectTransform;
            var bounds = LocalBounds(target, hintRoot);
            if (badgeOutline == null)
            {
                badgeOutline = HintRect("GuidedBadgeOutline", hintRoot, Vector2.one).gameObject.AddComponent<Image>();
                badgeOutline.raycastTarget = false;
                badgeCircle = Resources.Load<Sprite>("BattleEntry/GuideBadgeRing");
                badgeBox = Resources.Load<Sprite>("BattleEntry/GuideBadgeBox");
            }
            float diameter = Mathf.Max(bounds.width, bounds.height) * BadgeOuterScale;
            var size = Vector2.one * diameter;
            var center = bounds.center;
            // 사각 강조도 원형 강조와 같은 초상화 경계에 맞추고, 왼쪽 호버 화살표 공간은 포함하지 않습니다.
            badgeOutline.enabled = true;
            badgeOutline.type = circle ? Image.Type.Simple : Image.Type.Sliced;
            badgeOutline.sprite = circle ? badgeCircle : badgeBox;
            badgeOutline.rectTransform.localPosition = center;
            badgeOutline.rectTransform.sizeDelta = size;
            return badgeOutline.rectTransform;
        }
        private const float HintPeriod = 1.8f;

        public void SetActionHint(ActionHint value, RectTransform target = null)
        {
            // JC | 2026-11-09 | 다른 안내/대상으로 넘어가면 교정 글로우를 남기지 않습니다. 같은 대상 재클릭은 시작 시각만 갱신합니다.
            if(value!=ActionHint.ClickSkill || target!=hintTarget)StopSkillRedirect();
            if (actionHint != value) hintStarted = Time.unscaledTime;
            actionHint = value; hintTarget = target;
            if (value == ActionHint.None) { if (hintRoot != null) hintRoot.gameObject.SetActive(false); return; }
            if (panel == null) return;
            EnsureActionHint();
            hintRoot.gameObject.SetActive(true);
            hintText.text = value == ActionHint.ScrollRight ? JcTutorialText.Get("guideview.hints.001", "오른쪽으로 화면 이동\n<size=85%>마우스 스크롤 또는 D 키</size>") :
                value == ActionHint.ClickSkill ? JcTutorialText.Get("guideview.hints.002", "클릭하여 스킬을 사용") : JcTutorialText.Get("guideview.hints.003", "마우스 커서를\n올려보세요");
            hintKey.transform.parent.gameObject.SetActive(value == ActionHint.ScrollRight);
            iconGraphic.mouse = value == ActionHint.ClickSkill;
        }

        private RectTransform HintRect(string name, Transform parent, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = panel.gameObject.layer;
            var r = (RectTransform)go.transform; r.SetParent(parent, false); r.sizeDelta = size; return r;
        }
        private void EnsureActionHint()
        {
            if (hintRoot != null) return;
            hintRoot = HintRect("JC_ActionHint", panel.transform.parent, Vector2.zero);
            hintRoot.anchorMin = Vector2.zero; hintRoot.anchorMax = Vector2.one;
            hintRoot.offsetMin = hintRoot.offsetMax = Vector2.zero;
            // 전투 디밍은 HUD와 다른 루트 Canvas로 그려집니다.
            // 형제 순서만으로는 그 위에 올라가지 않으므로 안내만 독립 정렬합니다.
            var parentCanvas = panel.GetComponentInParent<Canvas>();
            var hintCanvas = hintRoot.gameObject.AddComponent<Canvas>();
            hintCanvas.overrideSorting = true;
            hintCanvas.sortingLayerID = parentCanvas.sortingLayerID;
            int topOrder = parentCanvas.sortingOrder;
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                    if (canvas != hintCanvas && canvas.sortingLayerID == parentCanvas.sortingLayerID)
                        topOrder = Mathf.Max(topOrder, canvas.sortingOrder);
            hintCanvas.sortingOrder = topOrder + 1;
            hintCanvas.worldCamera = parentCanvas.worldCamera;
            hintGroup = hintRoot.gameObject.AddComponent<CanvasGroup>(); hintGroup.blocksRaycasts = false; hintGroup.interactable = false;
            hintLines = new RectTransform[3];
            for (int i=0;i<hintLines.Length;i++) {
                hintLines[i]=HintRect("Connector"+i,hintRoot,Vector2.one);
                var line=hintLines[i].gameObject.AddComponent<Image>(); line.color=accent; line.raycastTarget=false;
            }
            hintFrame=HintRect("SkillFocus",hintRoot,Vector2.one);
            hintFrame.gameObject.AddComponent<JcTutorialGraphic>().Configure(JcTutorialGraphic.Shape.Frame,accent,0,4);
            hintCard=HintRect("HintCard",hintRoot,new Vector2(310,94));
            var bg=hintCard.gameObject.AddComponent<JcTutorialGraphic>(); bg.Configure(JcTutorialGraphic.Shape.Panel,new Color(.035f,.09f,.12f,1),8,2.5f); bg.SetOpaqueFill(true);
            hintIcon=HintRect("InputIcon",hintCard,new Vector2(40,50));
            hintIcon.anchorMin=hintIcon.anchorMax=new Vector2(0,.5f); hintIcon.anchoredPosition=new Vector2(39,0);
            iconGraphic=hintIcon.gameObject.AddComponent<JcTutorialHintIcon>(); iconGraphic.color=Color.white; iconGraphic.raycastTarget=false;
            var label=HintRect("Label",hintCard,Vector2.zero); label.anchorMin=Vector2.zero; label.anchorMax=Vector2.one; label.offsetMin=new Vector2(76,10); label.offsetMax=new Vector2(-14,-10);
            hintText=label.gameObject.AddComponent<TextMeshProUGUI>(); hintText.font=message!=null?message.font:heading.font;
            hintText.fontSize=22; hintText.color=Color.white; hintText.alignment=TextAlignmentOptions.MidlineLeft; hintText.raycastTarget=false;
            var key=HintRect("DKey",hintRoot,new Vector2(60,54));
            var keyBg=key.gameObject.AddComponent<JcTutorialGraphic>(); keyBg.Configure(JcTutorialGraphic.Shape.Panel,new Color(.05f,.16f,.12f,1),6,2); keyBg.SetOpaqueFill(true);
            var keyLabel=HintRect("KeyLabel",key,new Vector2(60,54));
            hintKey=keyLabel.gameObject.AddComponent<TextMeshProUGUI>(); hintKey.font=hintText.font; hintKey.text="D"; hintKey.fontSize=28; hintKey.color=Color.white; hintKey.alignment=TextAlignmentOptions.Center; hintKey.raycastTarget=false;
        }
        private void HintLine(int index, Vector2 a, Vector2 b, float alpha)
        {
            var r=hintLines[index]; var d=b-a; r.gameObject.SetActive(d.sqrMagnitude>.01f);
            r.localPosition=(a+b)*.5f; r.sizeDelta=new Vector2(d.magnitude,4.5f);
            r.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);
            r.GetComponent<Image>().color=new Color(accent.r,accent.g,accent.b,alpha);
        }
        private void RenderActionHint(float time)
        {
            if (actionHint==ActionHint.None || hintRoot==null) return;
            if (redirectWhenHidden && !redirectShowCard && time-redirectStarted >= 2f) { SetActionHint(ActionHint.None); return; }
            bool valid=(visible || redirectWhenHidden) && (actionHint==ActionHint.ScrollRight || (hintTarget!=null && hintTarget.gameObject.activeInHierarchy));
            hintRoot.gameObject.SetActive(valid); if(!valid)return;
            hintRoot.SetAsLastSibling();
            float phase=Mathf.Repeat((time-hintStarted)/HintPeriod,1);
            float pulse=.65f+.35f*(.5f-.5f*Mathf.Cos(phase*Mathf.PI*2));
            iconGraphic.phase=phase; iconGraphic.SetVerticesDirty();
            foreach(var line in hintLines)line.gameObject.SetActive(false);
            // 스킬 선택을 마친 뒤의 젬 파워 차단은 강조 효과만 재생하고 안내 말풍선은 반복하지 않습니다.
            hintCard.gameObject.SetActive(!redirectWhenHidden || redirectShowCard);
            hintFrame.gameObject.SetActive(actionHint==ActionHint.ClickSkill);
            var bounds=hintRoot.rect;
            bool scroll=actionHint==ActionHint.ScrollRight, click=actionHint==ActionHint.ClickSkill;
            float cursorAlpha = scroll ? pulse : click ? 1 : Mathf.Min(Mathf.Clamp01(phase/.15f), Mathf.Clamp01((1-phase)/.2f));
            iconGraphic.color = new Color(1, 1, 1, cursorAlpha);
            hintCard.sizeDelta=new Vector2(scroll?350:click?330:240,94);
            Vector2 pos;
            if(scroll) {
                pos=new Vector2(bounds.xMax-hintCard.rect.width*.5f-36,bounds.center.y+140);
                var k=(RectTransform)hintKey.transform.parent; k.localPosition=new Vector2(pos.x+110,pos.y-85+(phase>.55f&&phase<.85f?-3:0));
                HintLine(0,new Vector2(bounds.xMax-12,pos.y-90),new Vector2(bounds.xMax-12,pos.y+90),pulse);
                HintLine(1,new Vector2(bounds.xMax-32,pos.y-8),new Vector2(bounds.xMax-20,pos.y),pulse);
                HintLine(2,new Vector2(bounds.xMax-32,pos.y+8),new Vector2(bounds.xMax-20,pos.y),pulse);
            } else {
                var target=LocalBounds(hintTarget,hintRoot);
                float cardLeft = click ? Mathf.Max(target.xMax + 100, LocalBounds((RectTransform)panel.transform, hintRoot).xMin) : 0;
                pos=click?new Vector2(cardLeft+hintCard.rect.width*.5f,target.center.y):new Vector2(target.xMin-hintCard.rect.width*.5f-40,target.center.y);
                pos.x=Mathf.Clamp(pos.x,bounds.xMin+hintCard.rect.width*.5f+12,bounds.xMax-hintCard.rect.width*.5f-12);
                pos.y=Mathf.Clamp(pos.y,bounds.yMin+59,bounds.yMax-59);
                if(click) {
                    hintFrame.localPosition=target.center; hintFrame.sizeDelta=target.size+Vector2.one*8;
                    hintFrame.GetComponent<JcTutorialGraphic>().color=new Color(accent.r,accent.g,accent.b,pulse);
                    Vector2 from=new Vector2(target.xMax+4,target.center.y), end=new Vector2(pos.x-hintCard.rect.width*.5f,pos.y);
                    if (!redirectWhenHidden || redirectShowCard) HintLine(0,from,end,pulse);
                    // JC | 2026-11-09 | 이동력 강조와 동일 함수/설정으로 2초 수렴·1초 휴식을 반복합니다.
                    if(redirectedSkill==hintTarget)
                        RenderConvergingOutlines(hintRoot,target.min-Vector2.one*focusPadding,target.max+Vector2.one*focusPadding,time-redirectStarted,time);
                } else HintLine(0,new Vector2(pos.x+hintCard.rect.width*.5f,pos.y),new Vector2(target.xMin-6,target.center.y),pulse);
            }
            hintCard.localPosition=pos;
            hintIcon.anchoredPosition=new Vector2(39+(click?0:Mathf.Lerp(-7,7,Mathf.SmoothStep(0,1,Mathf.Clamp01(phase*3)))),0);
        }
        private void OnDestroy() { if(hintRoot!=null) { if(Application.isPlaying)Destroy(hintRoot.gameObject);else DestroyImmediate(hintRoot.gameObject); } }
    }
}
