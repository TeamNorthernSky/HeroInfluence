using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace JC.EnvironmentDetail
{
    // 확정 메시에서 수로 폭을 읽습니다. 현재 지형이나 베이크 원본은 수정하지 않습니다.
    public static class JcWaterFlowConstraint
    {
        public static Texture2D Create(GameObject root,Material source)
        {
            var map=source.GetTexture("_FlowMap") as Texture2D;
            if(map==null || !map.isReadable)return null;
            var bounds=source.GetVector("_FlowMapBounds");
            if(bounds.z<=0 || bounds.w<=0)return null;
            int w=map.width,h=map.height;
            var wet=new bool[w*h];
            Func<Vector3,Vector2> grid=v=>new Vector2((v.x-bounds.x)*bounds.z*w-.5f,(v.z-bounds.y)*bounds.w*h-.5f);
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter=renderer.GetComponent<MeshFilter>();var mesh=filter!=null?filter.sharedMesh:null;
                if(mesh==null || !mesh.isReadable)continue;
                var vertices=mesh.vertices;var mats=renderer.sharedMaterials;
                for(int sub=0;sub<mesh.subMeshCount && sub<mats.Length;sub++)
                {
                    var mat=mats[sub];if(mat==null || mat.shader!=source.shader)continue;
                    var indices=mesh.GetTriangles(sub);
                    for(int k=0;k<indices.Length;k+=3)
                    {
                        var a=grid(renderer.transform.TransformPoint(vertices[indices[k]]));
                        var b=grid(renderer.transform.TransformPoint(vertices[indices[k+1]]));
                        var c=grid(renderer.transform.TransformPoint(vertices[indices[k+2]]));
                        if(Mathf.Abs(Cross(b-a,c-a))<.0001f)continue;
                        int x0=Mathf.Max(0,Mathf.CeilToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))-.001f));
                        int x1=Mathf.Min(w-1,Mathf.FloorToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))+.001f));
                        int y0=Mathf.Max(0,Mathf.CeilToInt(Mathf.Min(a.y,Mathf.Min(b.y,c.y))-.001f));
                        int y1=Mathf.Min(h-1,Mathf.FloorToInt(Mathf.Max(a.y,Mathf.Max(b.y,c.y))+.001f));
                        for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
                        {
                            var p=new Vector2(x,y);float ab=Cross(b-a,p-a),bc=Cross(c-b,p-b),ca=Cross(a-c,p-c);
                            if((ab>=-.001f&&bc>=-.001f&&ca>=-.001f)||(ab<=.001f&&bc<=.001f&&ca<=.001f))wet[y*w+x]=true;
                        }
                    }
                }
            }
            if(!wet.Any(v=>v))return null;
            var colors=Constrain(map.GetPixels(),wet,w,h);
            for(int i=0;i<colors.Length;i++)colors[i].b=wet[i]?1:0;
            var result=new Texture2D(w,h,TextureFormat.RGBAHalf,false,true){name="JC constrained canal flow",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            result.SetPixels(colors);result.Apply(false,false);return result;
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        // 직선부의 양쪽 호안이 이어지는 행/열만 고정하고, 넓게 열린 교차점은 기존 방향을 보존합니다.
        public static Color[] Constrain(Color[] source,bool[] wet,int w,int h)
        {
            var output=(Color[])source.Clone();int waterCount=0;
            var sx=new int[wet.Length];var sy=new int[wet.Length];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(wet[y*w+x])
            {
                int left=x,right=x,down=y,up=y;
                while(left>0&&wet[y*w+left-1])left--;
                while(right<w-1&&wet[y*w+right+1])right++;
                while(down>0&&wet[(down-1)*w+x])down--;
                while(up<h-1&&wet[(up+1)*w+x])up++;
                int i=y*w+x;sx[i]=right-left+1;sy[i]=up-down+1;waterCount++;
            }
            if(waterCount==0)return output;
            var filled=(bool[])wet.Clone();var queue=new Queue<int>();
            for(int i=0;i<wet.Length;i++)if(wet[i])
            {
                var d=new Vector2(source[i].r*2-1,source[i].g*2-1);
                if(sy[i]>sx[i]*3)d=new Vector2(0,d.y<0?-1:1);
                else if(sx[i]>sy[i]*3)d=new Vector2(d.x<0?-1:1,0);
                else if(d.sqrMagnitude>.0001f)d.Normalize();
                output[i]=new Color(d.x*.5f+.5f,d.y*.5f+.5f,0,1);queue.Enqueue(i);
            }
            while(queue.Count>0)
            {
                int i=queue.Dequeue(),x=i%w,y=i/w;
                foreach(int j in new[]{x>0?i-1:-1,x+1<w?i+1:-1,y>0?i-w:-1,y+1<h?i+w:-1})
                {if(j<0||filled[j])continue;filled[j]=true;output[j]=output[i];queue.Enqueue(j);}
            }
            return output;
        }
    }
}
