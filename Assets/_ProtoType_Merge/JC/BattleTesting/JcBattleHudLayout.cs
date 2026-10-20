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
                if(item.rect){r.anchorMin=item.anchorMin;r.anchorMax=item.anchorMax;r.pivot=item.pivot;r.anchoredPosition=item.position;r.sizeDelta=item.size;}
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
        }
        void Style(RectTransform r,string[] names)
        {
            if(!names.Contains(r.name))return;var image=r.GetComponent<Image>();if(image==null)return;
            bool outline=r.name=="SelectedOverlay"||r.name=="SelectedSkillFrame"||r.name=="SkillButtonFrame";
            image.sprite=outline?border:surface;image.type=Image.Type.Sliced;image.material=null;
            image.color=outline?new Color(.48f,.73f,.88f,.85f):r.name=="Background"?new Color(.045f,.065f,.095f,.92f):new Color(.1f,.14f,.19f,.92f);
            if(r.name=="BaseHoverOverlay")image.color=new Color(.4f,.62f,.78f,.16f);
        }
    }
}
