using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

namespace JC.Tutorial
{
    /// <summary>스프라이트 없이 해상도에 맞게 그리는 안내 배경·테두리·포인터.</summary>
    [RequireComponent(typeof(CanvasRenderer)), AddComponentMenu("JC Tutorial/안내 도형")]
    public sealed class JcTutorialGraphic : MaskableGraphic
    {
        public enum Shape { [InspectorName("둥근 패널")] Panel, [InspectorName("강조 테두리")] Frame, [InspectorName("아래 방향 포인터")] Pointer, [InspectorName("큰 안내 화살표")] Arrow }
        [SerializeField, Tooltip("배경 패널, 빈 강조 테두리, 아래 방향 포인터 중 표시할 도형입니다.")]
        private Shape shape;
        [SerializeField, Min(0), Tooltip("모서리 반경입니다. Canvas 기준 단위이며 짧은 변의 절반까지만 적용합니다.")]
        private float radius = 16;
        [SerializeField, Min(0), Tooltip("테두리 두께입니다. Canvas 기준 단위이며 0이면 테두리를 표시하지 않습니다.")]
        private float borderWidth = 1.5f;
        [SerializeField, Min(0), Tooltip("강조 테두리 바깥으로 부드럽게 번지는 폭입니다. Canvas 단위이며 0이면 글로우를 끕니다.")]
        private float glowWidth;
        [SerializeField, Tooltip("패널 배경에 30% 섞을 보조 색입니다.")]
        private Color lowerColor = new Color(.025f, .065f, .09f, .96f);
        [SerializeField, Tooltip("패널의 윤곽선 색입니다. 강조 테두리는 Graphic의 Color를 사용합니다.")]
        private Color borderColor = new Color(.42f, .94f, .8f, .5f);
        [SerializeField, Min(0), Tooltip("안내 테두리의 최소 두께입니다. 모든 클릭 안내에 공통 적용합니다.")]
        private float emphasisWidth = 6;
        [SerializeField, Min(.1f), Tooltip("안내 테두리를 따라 빛이 한 바퀴 도는 시간(초)입니다.")]
        private float sweepPeriod = 1.8f;
        [SerializeField, Min(20), Tooltip("분절 화살표의 최대 세로 길이(Canvas 단위)입니다.")]
        private float arrowLength=190;
        [SerializeField, Range(.1f,1), Tooltip("안내 담당자가 지정한 너비에 적용하는 축소 비율입니다.")]
        private float arrowWidthScale=.88f;
        [SerializeField, Min(0), Tooltip("화살촉과 막대의 꼭짓점에서 둥글게 다듬는 길이(Canvas 단위)입니다.")]
        private float arrowCorner=3;
        [SerializeField, Min(0), Tooltip("화살표가 대상에서 멀어졌다 돌아오는 왕복 폭(Canvas 단위)입니다.")]
        private float arrowBobTravel=4;
        private float sweepClock;
        public void AnimateSweep(float time) { sweepClock = time; if(shape == Shape.Frame || shape == Shape.Arrow)SetVerticesDirty(); }
        private void LateUpdate() { if(shape == Shape.Frame)AnimateSweep(Time.unscaledTime); }
        public const float ArrowSweepPeriod = .9f;
        public const int ArrowSweepCount = 3;
        public float GlowWidth => glowWidth;

        public void Configure(Shape value, Color fill, float rounding, float width)
        { shape = value; color = fill; radius = rounding; borderWidth = width; raycastTarget = false; SetVerticesDirty(); }
        public void SetGlowWidth(float value) { glowWidth = Mathf.Max(0, value); SetVerticesDirty(); }

        public void SetBorderWidth(float value) { if(borderWidth==value)return; borderWidth=value;SetVerticesDirty(); }
        public void SetShape(Shape value) { if(shape==value)return;shape=value;SetVerticesDirty(); }
        // 수렴 글로우의 최대 확장 범위 밖에서 짧은 화살표를 직각 방향으로 안내한다.
        public void AimFromCenter(Vector2 min, Vector2 max, float width, float glowClearance=0)
        {
            var parent=rectTransform.parent as RectTransform;if(parent==null)return;
            Vector2 target=(min+max)*.5f;
            bool down=target.y<=parent.rect.center.y;
            float available=down?parent.rect.yMax-max.y:min.y-parent.rect.yMin;
            Vector2 direction=down?Vector2.down:Vector2.up;
            float gap=20+Mathf.Max(0,glowClearance);
            float bob=arrowBobTravel*(.5f-.5f*Mathf.Cos(sweepClock/ArrowSweepPeriod*Mathf.PI*2));
            Vector2 tip=new Vector2(target.x,down?max.y+gap:min.y-gap);
            if(available<160) {
                bool right=target.x>parent.rect.center.x;direction=right?Vector2.right:Vector2.left;
                tip=new Vector2(right?min.x-gap:max.x+gap,target.y);
                available=right?min.x-parent.rect.xMin:parent.rect.xMax-max.x;
            }
            float length=Mathf.Clamp(available-gap-arrowBobTravel-12,1,arrowLength);
            SetShape(Shape.Arrow);raycastTarget=false;
            rectTransform.localPosition=tip-direction*(length*.5f+bob);
            rectTransform.localRotation=Quaternion.FromToRotation(Vector3.down,new Vector3(direction.x,direction.y,0));
            rectTransform.sizeDelta=new Vector2(Mathf.Min(width*arrowWidthScale,length*.8f),length);
        }

        private Color ArrowShade(float y,float alpha)
        {
            float d=y-(1-Mathf.Repeat(sweepClock/ArrowSweepPeriod,1));
            Color lit=Color.Lerp(color,Color.white,.55f*Mathf.Exp(-d*d/.008f));
            lit.a=color.a*alpha;return lit;
        }
        private void ArrowStrip(VertexHelper vh,Rect r,float y0,float y1,float w0,float w1,float a0,float a1)
        {
            int n=vh.currentVertCount;
            for(int row=0;row<2;row++)for(int col=0;col<3;col++) {
                float y=row==0?y0:y1,w=row==0?w0:w1,alpha=row==0?a0:a1;
                Add(vh,new Vector2(r.center.x+(col-1)*w,r.yMin+r.height*y),ArrowShade(y,alpha));
            }
            vh.AddTriangle(n,n+3,n+1);vh.AddTriangle(n+1,n+3,n+4);
            vh.AddTriangle(n+1,n+4,n+2);vh.AddTriangle(n+2,n+4,n+5);
        }

        private void RoundedArrowPart(VertexHelper vh,Rect r,Vector2[] corners,float alphaNear,float alphaFar)
        {
            var outline=new List<Vector2>();
            for(int i=0;i<corners.Length;i++) {
                Vector2 c=corners[i],prev=corners[(i+corners.Length-1)%corners.Length],next=corners[(i+1)%corners.Length];
                float cut=Mathf.Min(arrowCorner,Mathf.Min((prev-c).magnitude,(next-c).magnitude)*.2f);
                Vector2 a=c+(prev-c).normalized*cut,b=c+(next-c).normalized*cut;
                for(int j=0;j<=6;j++){float u=j/6f;outline.Add((1-u)*(1-u)*a+2*(1-u)*u*c+u*u*b);}
            }
            float bottom=float.PositiveInfinity,top=float.NegativeInfinity;
            var levels=new List<float>();foreach(var v in outline){bottom=Mathf.Min(bottom,v.y);top=Mathf.Max(top,v.y);levels.Add(v.y);}
            for(int i=1;i<16;i++)levels.Add(Mathf.Lerp(bottom,top,i/16f));levels.Sort();
            float HalfWidth(float y) {
                float right=0;
                for(int i=0;i<outline.Count;i++){
                    Vector2 a=outline[i],b=outline[(i+1)%outline.Count];
                    if(Mathf.Abs(a.y-b.y)<.00001f){if(Mathf.Abs(y-a.y)<.0001f)right=Mathf.Max(right,Mathf.Max(a.x,b.x));continue;}
                    if(y>=Mathf.Min(a.y,b.y)-.00001f&&y<=Mathf.Max(a.y,b.y)+.00001f)right=Mathf.Max(right,Mathf.Lerp(a.x,b.x,Mathf.Clamp01((y-a.y)/(b.y-a.y))));
                }
                return right;
            }
            for(int i=0;i<levels.Count-1;i++){
                float a=levels[i],b=levels[i+1];if(b-a<.00001f)continue;
                ArrowStrip(vh,r,a/r.height,b/r.height,HalfWidth(a),HalfWidth(b),Mathf.Lerp(alphaNear,alphaFar,Mathf.InverseLerp(bottom,top,a)),Mathf.Lerp(alphaNear,alphaFar,Mathf.InverseLerp(bottom,top,b)));
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (r.width <= 0 || r.height <= 0) return;
            if (shape == Shape.Arrow)
            {
                RoundedArrowPart(vh,r,new[]{new Vector2(0,0),new Vector2(r.width*.5f,r.height*.28f),new Vector2(-r.width*.5f,r.height*.28f)},1,1);
                for(int i=0;i<3;i++) {
                    float y=r.height*(.38f+i*.22f),h=r.height*.13f,w=r.width*(.34f-i*.065f);
                    float near=i==0?.85f:i==1?.5f:.22f,far=i==0?.65f:i==1?.3f:0;
                    RoundedArrowPart(vh,r,new[]{new Vector2(-w,y),new Vector2(w,y),new Vector2(w,y+h),new Vector2(-w,y+h)},near,far);
                }
                return;
            }
            if (shape == Shape.Pointer)
            {
                // 두 겹의 면으로 만든 짧은 화살촉. 클릭 판정은 갖지 않는다.
                Add(vh, new Vector2(r.xMin, r.yMax), color);
                Add(vh, new Vector2(r.center.x, r.yMin), color);
                Add(vh, new Vector2(r.xMax, r.yMax), color);
                Add(vh, new Vector2(r.center.x, Mathf.Lerp(r.yMin, r.yMax, .65f)), color);
                vh.AddTriangle(0, 1, 3); vh.AddTriangle(3, 1, 2); return;
            }
            float corner = Mathf.Min(radius, Mathf.Min(r.width, r.height) * .5f);
            float width = Mathf.Min(shape == Shape.Frame ? Mathf.Max(borderWidth,emphasisWidth) : borderWidth, Mathf.Min(r.width, r.height) * .45f);
            const int perCorner = 31;
            const int count = 4 * (perCorner + 1);
            if (shape == Shape.Panel)
            {
                Color fill = Color.Lerp(lowerColor, color, .7f);
                Add(vh, r.center, fill);
                for (int i = 0; i < count; i++)
                { Vector2 p = RoundedPoint(r, corner, i, perCorner); Add(vh, p, fill); }
                for (int i = 0; i < count; i++) vh.AddTriangle(0, i + 1, (i + 1) % count + 1);
            }
            if (width <= 0) return;
            if (shape == Shape.Frame && glowWidth > 0)
            {
                int start = vh.currentVertCount;
                Rect expanded = new Rect(r.xMin - glowWidth, r.yMin - glowWidth, r.width + glowWidth * 2, r.height + glowWidth * 2);
                Color transparent = color; transparent.a = 0;
                Color glow = color; glow.a *= .45f;
                for (int i = 0; i < count; i++)
                { Add(vh, RoundedPoint(expanded, corner + glowWidth, i, perCorner), transparent); Add(vh, RoundedPoint(r, corner, i, perCorner), glow); }
                for (int i = 0; i < count; i++)
                { int a = start + i * 2, b = start + ((i + 1) % count) * 2; vh.AddTriangle(a, b, a + 1); vh.AddTriangle(a + 1, b, b + 1); }
            }
            int offset = vh.currentVertCount;
            Rect inner = new Rect(r.xMin + width, r.yMin + width, r.width - width * 2, r.height - width * 2);
            Color edge = shape == Shape.Frame ? color : borderColor;
            for (int i = 0; i < count; i++)
            {
                Color shine = edge;
                if(shape == Shape.Frame) {
                    float d=Mathf.Abs(Mathf.Repeat(i/(float)count - sweepClock/Mathf.Max(.1f,sweepPeriod)+.5f,1)-.5f);
                    float strength=Mathf.Exp(-d*d/.003f);
                    shine=Color.Lerp(edge,Color.white,strength*.95f);shine.a=edge.a;
                }
                Add(vh, RoundedPoint(r, corner, i, perCorner), shine); Add(vh, RoundedPoint(inner, Mathf.Max(0, corner - width), i, perCorner), shine);
            }
            for (int i = 0; i < count; i++)
            { int a = offset + i * 2, b = offset + ((i + 1) % count) * 2; vh.AddTriangle(a, b, a + 1); vh.AddTriangle(a + 1, b, b + 1); }
        }

        private static Vector2 RoundedPoint(Rect r, float radius, int index, int perCorner)
        {
            int corner = index / (perCorner + 1);
            float phase=(index % (perCorner+1))/(float)(perCorner+1);
            float angle=(corner*90 + Mathf.Min(1,phase*2)*90)*Mathf.Deg2Rad;
            Vector2 center = new Vector2(corner == 0 || corner == 3 ? r.xMax-radius : r.xMin+radius, corner < 2 ? r.yMax-radius : r.yMin+radius);
            Vector2 end=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
            if(phase<=.5f)return end;
            Vector2 direction=corner==0?Vector2.left:corner==1?Vector2.down:corner==2?Vector2.right:Vector2.up;
            float length=(corner%2==0?r.width:r.height)-2*radius;
            return end+direction*length*(phase*2-1);
        }

        private static void Add(VertexHelper vh, Vector2 p, Color c)
        { var v = UIVertex.simpleVert; v.position = p; v.color = c; vh.AddVert(v); }
    }
}
