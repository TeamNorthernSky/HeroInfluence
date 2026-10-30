using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace JC.BattleTesting
{
    public sealed partial class JcBattleSurfaceDressing
    {
        bool leftVerge;
        float vergeEdge;
        readonly List<Triangle> vergeSurface=new List<Triangle>();
        bool UnderVergeSlope(Vector3 point)
        {
            foreach(var t in vergeSurface){var n=Vector3.Cross(t.b-t.a,t.c-t.a);if(Mathf.Abs(n.y)<.0001f||!t.Contains(point))continue;
                float height=t.a.y-(n.x*(point.x-t.a.x)+n.z*(point.z-t.a.z))/n.y;if(height>point.y+.25f)return true;}
            return false;
        }
        // Build가 소유하는 복사본만 수정합니다. 도로에 닿는 산의 가장자리는 고정합니다.
        void PrepareLeftVerge(MeshFilter[] filters,JcBattleSurfaceProfile p)
        {
            if(p.leftMountainSetback<=0)return;
            foreach(var f in filters)
            {
                if(!f.name.StartsWith("Mount",StringComparison.OrdinalIgnoreCase)||!f.sharedMesh||!f.sharedMesh.isReadable)continue;
                var r=f.GetComponent<Renderer>();if(!r||r.bounds.max.x>=-3||r.bounds.min.z>20||r.bounds.max.z< -20)continue;
                float edge=r.bounds.max.x;
                var mesh=f.sharedMesh;var vertices=mesh.vertices;
                for(int i=0;i<vertices.Length;i++)
                {
                    var world=f.transform.TransformPoint(vertices[i]);
                    float weight=Mathf.SmoothStep(0,1,Mathf.Clamp01((edge-world.x)/4));
                    world.x-=p.leftMountainSetback*weight;
                    vertices[i]=f.transform.InverseTransformPoint(world);
                }
                mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
                var triangles=mesh.triangles;
                for(int i=0;i<triangles.Length;i+=3)vergeSurface.Add(new Triangle{a=f.transform.TransformPoint(vertices[triangles[i]]),b=f.transform.TransformPoint(vertices[triangles[i+1]]),c=f.transform.TransformPoint(vertices[triangles[i+2]])});
                vergeEdge=leftVerge?Mathf.Max(vergeEdge,edge):edge;leftVerge=true;
            }
        }
        void PlantVergeGrass(List<Triangle> grass,JcBattleSurfaceProfile p,int seed,Vector3[] cameras)
        {
            var candidates=grass.Where(t=>Mathf.Min(t.a.x,t.b.x,t.c.x)<vergeEdge&&Mathf.Max(t.a.z,t.b.z,t.c.z)>-45&&Mathf.Min(t.a.z,t.b.z,t.c.z)<45).ToArray();
            if(candidates.Length==0||p.leftGrassLimit<=0)return;
            var weights=new float[candidates.Length];float total=0;
            for(int i=0;i<candidates.Length;i++){total+=Vector3.Cross(candidates[i].b-candidates[i].a,candidates[i].c-candidates[i].a).magnitude*.5f;weights[i]=total;}
            var obstacles=GetComponentsInChildren<Renderer>().Where(r=>r.bounds.size.y>.4f&&!r.name.StartsWith("Mount",StringComparison.OrdinalIgnoreCase)&&!GetComponent<JcDynamicBattleOutput>().TerrainRenderers.Contains(r)).ToArray();
            var points=new List<Vector3>();var vertices=new List<Vector3>();var indices=new List<int>();
            var random=new System.Random(unchecked(seed+p.seed+719));
            for(int attempt=0;attempt<p.leftGrassLimit*25&&points.Count<p.leftGrassLimit;attempt++)
            {
                float sample=(float)random.NextDouble()*total;int k=Array.FindIndex(weights,x=>x>=sample);var tri=candidates[Mathf.Max(0,k)];
                float u=Mathf.Sqrt((float)random.NextDouble()),v=(float)random.NextDouble();var point=(1-u)*tri.a+u*(1-v)*tri.b+u*v*tri.c;
                if(point.x>vergeEdge-.45f||point.x<vergeEdge-18||Mathf.Abs(point.z)>45||points.Any(x=>(x-point).sqrMagnitude<.55f))continue;
                if(obstacles.Any(r=>r.bounds.Intersects(new Bounds(point+Vector3.up*.2f,new Vector3(.6f,.4f,.6f)))))continue;
                if(UnderVergeSlope(point))continue;
                points.Add(point);
                // 낮은 불투명 풀잎을 묶어 투명 정렬 비용 없이 표현합니다.
                for(int blade=0;blade<5;blade++)
                {
                    float angle=(float)random.NextDouble()*Mathf.PI*2,height=.16f+(float)random.NextDouble()*.22f;
                    var side=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.055f;
                    var center=point+new Vector3((float)random.NextDouble()-.5f,0,(float)random.NextDouble()-.5f)*.4f+Vector3.up*.015f;
                    int start=vertices.Count;vertices.Add(center-side);vertices.Add(center+side);vertices.Add(center+Vector3.up*height+side*.5f);
                    indices.AddRange(new[]{start,start+2,start+1});
                }
            }
            if(vertices.Count==0)return;
            var mesh=new Mesh{name="Battle left verge grass",hideFlags=HideFlags.DontSave};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)return;
            var material=new Material(shader){name="Battle verge grass",hideFlags=HideFlags.DontSave};material.SetColor("_BaseColor",new Color(.32f,.40f,.19f,1));material.SetFloat("_Smoothness",0);material.SetFloat("_Cull",0);owned.Add(material);
            var group=new GameObject("JC_BattleVergeGrass"){hideFlags=HideFlags.DontSave};group.transform.SetParent(transform,false);group.AddComponent<MeshFilter>().sharedMesh=mesh;group.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
    }
}
