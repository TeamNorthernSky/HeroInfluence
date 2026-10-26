using UnityEngine;
using UnityEngine.UI;

namespace JC.Tutorial
{
    /// <summary>입력을 받지 않는 튜토리얼 커서/좌클릭 도형.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class JcTutorialHintIcon : MaskableGraphic
    {
        [HideInInspector] public bool mouse;
        [HideInInspector] public float phase;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!mouse) {
                // 커서 화살표의 닫힌 실루엣. 오목한 부분은 삼각형을 직접 연결한다.
                Vector2[] p={new Vector2(.2f,.95f),new Vector2(.2f,.25f),new Vector2(.4f,.43f),new Vector2(.58f,.06f),new Vector2(.72f,.13f),new Vector2(.53f,.5f),new Vector2(.85f,.5f)};
                for(int i=0;i<p.Length;i++)Vert(vh,p[i],color);
                vh.AddTriangle(0,1,2);vh.AddTriangle(0,2,5);vh.AddTriangle(0,5,6);vh.AddTriangle(2,3,4);vh.AddTriangle(2,4,5);return;
            }
            // 40 x 50 아이콘. 좌버튼 색 면적은 전체의 약 13%로 제한한다.
            Color dark=new Color(.035f,.09f,.12f,1);
            Oval(vh,new Vector2(.5f,.5f),new Vector2(.42f,.47f),color);
            Oval(vh,new Vector2(.5f,.5f),new Vector2(.33f,.40f),dark);
            var pressed=color;pressed.a=.45f+.55f*(.5f-.5f*Mathf.Cos(phase*Mathf.PI*2));
            Quad(vh,.21f,.60f,.45f,.82f,pressed);
            Quad(vh,.465f,.57f,.535f,.91f,color);
            Quad(vh,.17f,.54f,.83f,.60f,color);
            Quad(vh,.475f,.67f,.525f,.77f,Color.white);
        }
        private void Vert(VertexHelper vh,Vector2 p,Color c) {
            var r=rectTransform.rect;var v=UIVertex.simpleVert;
            v.position=new Vector3(r.xMin+p.x*r.width,r.yMin+p.y*r.height);v.color=c;vh.AddVert(v);
        }
        private void Quad(VertexHelper vh,float x0,float y0,float x1,float y1,Color c) {
            int n=vh.currentVertCount;Vert(vh,new Vector2(x0,y0),c);Vert(vh,new Vector2(x0,y1),c);Vert(vh,new Vector2(x1,y1),c);Vert(vh,new Vector2(x1,y0),c);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
        private void Oval(VertexHelper vh,Vector2 center,Vector2 size,Color c) {
            int n=vh.currentVertCount;Vert(vh,center,c);
            for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;Vert(vh,center+new Vector2(Mathf.Cos(a)*size.x,Mathf.Sin(a)*size.y),c);}
            for(int i=0;i<48;i++)vh.AddTriangle(n,n+1+i,n+1+(i+1)%48);
        }
    }
}
