// JC | 2026-11-05 | 기존 원경 편집 기능을 폴리싱에서 재사용합니다. Misc 원본 작업본과 참조는 보존합니다.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace JC.BackgroundEditing { public static partial class BackgroundOps {
    struct ClipVertex
    {
        public Vector3 p,n;public Vector4 tangent,u0,u1,u2,u3;public Color color;
        public static ClipVertex Lerp(ClipVertex a,ClipVertex b,float t)=>new ClipVertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t),tangent=Vector4.Lerp(a.tangent,b.tangent,t),u0=Vector4.Lerp(a.u0,b.u0,t),u1=Vector4.Lerp(a.u1,b.u1,t),u2=Vector4.Lerp(a.u2,b.u2,t),u3=Vector4.Lerp(a.u3,b.u3,t),color=Color.Lerp(a.color,b.color,t)};
    }    static Mesh Clip(MeshFilter f,Vector2 min,Vector2 max)
    {
        var m=f.sharedMesh;var vs=m.vertices;var ns=m.normals;var ts=m.tangents;var cs=m.colors;var sourceUV=new List<Vector4>[4];var resultUV=new List<Vector4>[4];for(int c=0;c<4;c++){sourceUV[c]=new List<Vector4>();m.GetUVs(c,sourceUV[c]);resultUV[c]=new List<Vector4>();}Vector4 U(int channel,int index)=>sourceUV[channel].Count==vs.Length?sourceUV[channel][index]:Vector4.zero;var verts=new List<Vector3>();var norms=new List<Vector3>();var tangents=new List<Vector4>();var colors=new List<Color>();var subs=new List<int>[m.subMeshCount];
        List<ClipVertex> Clip(List<ClipVertex> p,int axis,float boundary,bool greater)
        {
            var result=new List<ClipVertex>();if(p.Count==0)return result;var a=p[p.Count-1];float av=f.transform.TransformPoint(a.p)[axis];bool inside=greater?av>=boundary:av<=boundary;
            foreach(var b in p){float bv=f.transform.TransformPoint(b.p)[axis];bool next=greater?bv>=boundary:bv<=boundary;if(next!=inside)result.Add(ClipVertex.Lerp(a,b,(boundary-av)/(bv-av)));if(next)result.Add(b);a=b;av=bv;inside=next;}return result;
        }
        for(int sub=0;sub<m.subMeshCount;sub++)
        {
            subs[sub]=new List<int>();var ids=m.GetTriangles(sub);
            for(int i=0;i<ids.Length;i+=3)
            {
                var p=new List<ClipVertex>();for(int j=0;j<3;j++){int k=ids[i+j];p.Add(new ClipVertex{p=vs[k],n=ns.Length==vs.Length?ns[k]:Vector3.up,u0=U(0,k),u1=U(1,k),u2=U(2,k),u3=U(3,k),tangent=ts.Length==vs.Length?ts[k]:Vector4.zero,color=cs.Length==vs.Length?cs[k]:Color.white});}
                // 네 바깥 영역을 서로 겹치지 않게 순서대로 분리합니다.
                var pieces=new List<List<ClipVertex>>();pieces.Add(Clip(p,0,min.x,false));p=Clip(p,0,min.x,true);pieces.Add(Clip(p,0,max.x,true));p=Clip(p,0,max.x,false);pieces.Add(Clip(p,2,min.y,false));p=Clip(p,2,min.y,true);pieces.Add(Clip(p,2,max.y,true));
                foreach(var piece in pieces)for(int j=1;j+1<piece.Count;j++){foreach(var v in new[]{piece[0],piece[j],piece[j+1]}){subs[sub].Add(verts.Count);verts.Add(v.p);norms.Add(v.n);resultUV[0].Add(v.u0);resultUV[1].Add(v.u1);resultUV[2].Add(v.u2);resultUV[3].Add(v.u3);tangents.Add(v.tangent);colors.Add(v.color);}}
            }
        }
        var output=new Mesh{name=f.name+" Right clipped",indexFormat=IndexFormat.UInt32};output.SetVertices(verts);output.SetNormals(norms);for(int c=0;c<4;c++)if(sourceUV[c].Count==vs.Length)output.SetUVs(c,resultUV[c]);if(ts.Length==vs.Length)output.SetTangents(tangents);if(cs.Length==vs.Length)output.SetColors(colors);output.subMeshCount=subs.Length;for(int i=0;i<subs.Length;i++)output.SetTriangles(subs[i],i);output.RecalculateBounds();return output;
    }
}

}