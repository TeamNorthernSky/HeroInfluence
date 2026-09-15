using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class LicenseHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private string heading, body, tags;
    private Sprite icon;
    private TMP_FontAsset font;
    private bool bubble;
    private RectTransform panel;
    private static LicenseHoverTooltip visible;
    public void Bind(string title, string description, Sprite sprite, TMP_FontAsset textFont, bool speechBubble, string badges = "")
    {
        heading = title; body = description; icon = sprite; font = textFont; bubble = speechBubble; tags = badges;
        Hide();
        if (panel) { Destroy(panel.gameObject); panel = null; }
    }
    public void OnPointerEnter(PointerEventData e)
    {
        if (visible && visible != this) visible.Hide();
        if (!panel) Build();
        if (!panel) return;
        visible = this; panel.gameObject.SetActive(true); Place();
    }
    public void OnPointerExit(PointerEventData e) => Hide();
    private void OnDisable() => Hide();
    private void OnDestroy() { if (panel) Destroy(panel.gameObject); }
    private void Hide() { if (panel) panel.gameObject.SetActive(false); if (visible == this) visible = null; }
    private void LateUpdate() { if (panel && panel.gameObject.activeSelf) Place(); }
    private RectTransform Rect(string name, float x, float y, float w, float h)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(panel, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h); return r;
    }
    private Image Box(string name, float x, float y, float w, float h, Color color)
    {
        var r = Rect(name,x,y,w,h);
        Image i = bubble && (name == "Border" || name == "Body")
            ? r.gameObject.AddComponent<LicenseRoundedImage>() : r.gameObject.AddComponent<Image>();
        i.color = color; i.raycastTarget = false;
        return i;
    }
    private TMP_Text Text(string name, string value, float x, float y, float w, float h, int size, Color color)
    {
        var t = Rect(name,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>(); t.font = font; t.text = value;
        t.fontSize = size; t.enableAutoSizing = true; t.fontSizeMin = 17; t.fontSizeMax = size; t.color = color; t.raycastTarget = false; return t;
    }
    private void Build()
    {
        var owner = GetComponentInParent<Canvas>(); if (!owner) return;
        panel = new GameObject("LicenseHoverPanel", typeof(RectTransform)).GetComponent<RectTransform>();
        panel.SetParent(owner.rootCanvas.transform, false); panel.pivot = new Vector2(0,1);
        panel.sizeDelta = bubble ? new Vector2(620,126) : new Vector2(480,230);
        var canvas = panel.gameObject.AddComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = owner.sortingOrder + 10;
        var group = panel.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
        if (bubble)
        {
            var blue = new Color(.25f,.46f,.8f);
            var tailBorder=Box("TailBorder",165,102,28,28,blue).rectTransform; tailBorder.pivot=new Vector2(.5f,.5f);tailBorder.localRotation=Quaternion.Euler(0,0,45);
            Box("Border",0,0,620,104,blue);
            Box("Body",6,6,608,92,Color.white);
            var tail=Box("Tail",165,98,20,20,Color.white).rectTransform;tail.pivot=new Vector2(.5f,.5f);tail.localRotation=Quaternion.Euler(0,0,45);
            Text("Description",heading + ": " + body,22,19,576,69,25,Color.black);
        }
        else
        {
            Box("GoldBorder",0,0,480,230,new Color(1,.8f,.16f));
            Box("BlueBorder",4,4,472,222,new Color(.05f,.3f,.65f));
            Box("Body",8,8,464,214,new Color(.015f,.045f,.12f));
            var image=Box("SkillIcon",22,25,116,116,Color.white); image.sprite=icon; image.preserveAspect=true; image.enabled=icon!=null;
            Text("Title",heading,154,21,300,42,26,Color.white);
            Box("Badge",154,68,285,30,new Color(.04f,.36f,.75f));
            Text("Tags",tags,162,70,273,27,19,Color.white);
            Text("Description",body,154,109,301,102,22,Color.white);
        }
        panel.gameObject.SetActive(false);
    }
    private void Place()
    {
        var parent=(RectTransform)panel.parent; var corners=new Vector3[4]; ((RectTransform)transform).GetWorldCorners(corners);
        Vector2 left=parent.InverseTransformPoint(corners[1]), right=parent.InverseTransformPoint(corners[2]);
        float w=panel.rect.width,h=panel.rect.height;
        float x=bubble?left.x:right.x+12; float y=bubble?left.y+h+16:right.y;
        if(!bubble && x+w>parent.rect.xMax-12)x=left.x-w-12;
        x=Mathf.Clamp(x,parent.rect.xMin+12,Mathf.Max(parent.rect.xMin+12,parent.rect.xMax-w-12));
        y=Mathf.Clamp(y,parent.rect.yMin+h+12,Mathf.Max(parent.rect.yMin+h+12,parent.rect.yMax-12));
        panel.localPosition=new Vector3(x,y,0);
    }
}
// 둥근 UI 사각형을 메시로 그려 외부 스프라이트 의존성을 없앤다.
public sealed class LicenseRoundedImage : Image
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); var r = GetPixelAdjustedRect(); float radius = Mathf.Min(12, Mathf.Min(r.width,r.height)*.5f);
        vh.AddVert(r.center,color,Vector2.zero);
        for(int corner=0;corner<4;corner++)
        {
            Vector2 center = new Vector2(corner==0||corner==3?r.xMax-radius:r.xMin+radius,
                corner<2?r.yMax-radius:r.yMin+radius);
            for(int step=0;step<=8;step++)
            {
                float angle=(corner*90+step*90f/8)*Mathf.Deg2Rad;
                vh.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,color,Vector2.zero);
            }
        }
        for(int i=1;i<=36;i++)vh.AddTriangle(0,i,i==36?1:i+1);
    }
}