// JC | 2026-11-05 | 기존 원경 편집 기능을 폴리싱에서 재사용합니다. Misc 원본 작업본과 참조는 보존합니다.
using System;
using System.Linq;
using UnityEngine;
namespace JC.BackgroundEditing
{
    public static partial class BackgroundOps
    {
        public static int Sample(BackgroundDocument d,Vector2Int position)
        {
            int stored=d.cells.FindIndex(c=>c.position==position);if(stored>=0)return d.cells[stored].kind;
            var p=new Vector3(position.x,0,position.y);float highest=float.NegativeInfinity;int found=-1;
            foreach(var f in d.baseline.GetComponentsInChildren<MeshFilter>())
            {
                if(f.name.StartsWith("BG",StringComparison.Ordinal))continue;var r=f.GetComponent<Renderer>();var b=r.bounds;
                if(p.x<b.min.x||p.x>b.max.x||p.z<b.min.z||p.z>b.max.z)continue;
                var m=r.sharedMaterial;string tex=m&&m.HasProperty("_BaseMap")&&m.GetTexture("_BaseMap")?m.GetTexture("_BaseMap").name:"";string name=m?m.name:"";
                int extra=Array.IndexOf(TileTextures,tex);int kind=extra>=5?extra:tex.StartsWith("Road",StringComparison.Ordinal)||name.Contains("Asphalt")?0:tex=="tilemap_01"||name.Contains("Pavement")?1:tex=="tilemap_04"?2:tex=="tilemap_02"||name.Contains("Water")?3:-1;if(kind<0)continue;
                var mesh=f.sharedMesh;var v=mesh.vertices;var ids=mesh.triangles;
                for(int i=0;i<ids.Length;i+=3)
                {
                    var a=f.transform.TransformPoint(v[ids[i]]);var c=f.transform.TransformPoint(v[ids[i+1]]);var e=f.transform.TransformPoint(v[ids[i+2]]);
                    float den=(c.z-e.z)*(a.x-e.x)+(e.x-c.x)*(a.z-e.z);if(Mathf.Abs(den)<1e-6f)continue;
                    float u=((c.z-e.z)*(p.x-e.x)+(e.x-c.x)*(p.z-e.z))/den;float w=((e.z-a.z)*(p.x-e.x)+(a.x-e.x)*(p.z-e.z))/den;float t=1-u-w;
                    if(u<-.001f||w<-.001f||t<-.001f)continue;float y=u*a.y+w*c.y+t*e.y;if(y>highest){highest=y;found=kind;}
                }
            }
            return found;
        }
    }
}
