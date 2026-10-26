#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace JC.EnvironmentDetail
{
    public sealed partial class JcExploreDetailOverlay
    {
        public struct WaterBoundaryEntry { public Vector3 position; public string category; }
        readonly List<WaterBoundaryEntry> waterBoundaries=new List<WaterBoundaryEntry>();
        public IReadOnlyList<WaterBoundaryEntry> WaterBoundaries=>waterBoundaries;
        struct BankEdge { public Vector3 a,b; public float waterY,width; public bool enabled; }
        struct WaterVertex
        {
            public Vector3 p; public Vector2 uv; public float distance,landY;
            public static WaterVertex Lerp(WaterVertex a,WaterVertex b,float t)=>new WaterVertex{p=Vector3.Lerp(a.p,b.p,t),uv=Vector2.Lerp(a.uv,b.uv,t),distance=Mathf.Lerp(a.distance,b.distance,t),landY=Mathf.Lerp(a.landY,b.landY,t)};
        }
        sealed class WaterfrontMesh
        {
            public readonly List<Vector3> points=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>(),data=new List<Vector2>();
            readonly List<int>[] tris={new List<int>(),new List<int>()};
            public void Polygon(List<WaterVertex> polygon,int sub,float width,float cap)
            {
                if(polygon.Count<3)return;
                for(int j=1;j<polygon.Count-1;j++)
                {
                    var face=new[]{polygon[0],polygon[j],polygon[j+1]};
                    if(Vector3.Cross(face[1].p-face[0].p,face[2].p-face[0].p).sqrMagnitude<1e-14f)continue;
                    foreach(var v in face)
                    {
                        var p=v.p;float d=v.distance;
                        if(sub==1)
                        {
                            float toe=width*.15f;
                            if(d<=cap)p.y=v.landY;
                            else if(d<width-toe)p.y=Mathf.Lerp(v.landY,v.p.y+.025f,(d-cap)/(width-toe-cap));
                            else p.y=Mathf.Lerp(v.p.y+.025f,v.p.y,Mathf.InverseLerp(width-toe,width,d));
                        }
                        tris[sub].Add(points.Count);points.Add(p);uv.Add(v.uv);data.Add(new Vector2(Mathf.Max(0,d-width),width>0?Mathf.Clamp01(d/width):0));
                    }
                }
            }
            public Mesh Make(string name)
            {
                var m=new Mesh{name=name,hideFlags=HideFlags.DontSave,indexFormat=IndexFormat.UInt32};m.SetVertices(points);m.SetUVs(0,uv);m.SetUVs(1,data);m.subMeshCount=2;
                for(int i=0;i<2;i++)m.SetTriangles(tris[i],i);m.RecalculateNormals();m.RecalculateBounds();return m;
            }
        }
        static List<WaterVertex> ClipWater(List<WaterVertex> input,float threshold,bool greater)
        {
            var result=new List<WaterVertex>();if(input.Count==0)return result;
            var previous=input[input.Count-1];bool prevIn=greater?previous.distance>=threshold:previous.distance<=threshold;
            foreach(var current in input)
            {
                bool inside=greater?current.distance>=threshold:current.distance<=threshold;
                if(inside!=prevIn)result.Add(WaterVertex.Lerp(previous,current,(threshold-previous.distance)/(current.distance-previous.distance)));
                if(inside)result.Add(current);previous=current;prevIn=inside;
            }
            return result;
        }
        string BuildWaterfront(MeshFilter[] sources,Dictionary<Vector2Int,Cell> cells)
        {
            waterBoundaries.Clear();if(!profile.canalBanks&&!profile.cartoonWater)return "수로 원본";
            var water=sources.Where(f=>Kind(f)==5&&f.GetComponent<Renderer>().enabled&&!f.GetComponent<Renderer>().forceRenderingOff).ToArray();
            if(water.Length==0)return "수면 타일 없음";
            Shader ws=profile.waterShader!=null?profile.waterShader:Shader.Find("JC/Environment/Calm Canal Water");
            Shader bs=profile.bankShader!=null?profile.bankShader:Shader.Find("JC/Environment/Canal Bank");
            if((profile.cartoonWater&&ws==null)||(profile.canalBanks&&bs==null))throw new InvalidOperationException("수로 셰이더 참조가 없습니다.");
            Material wm=null,bm=null;
            if(profile.cartoonWater)
            {
                wm=new Material(ws){hideFlags=HideFlags.DontSave};owned.Add(wm);
                var waterProfile=Resources.Load<JcWaterProfile>("JC_Water_Default");
                (waterProfile!=null?waterProfile.settings:new JcWaterSettings()).Apply(wm);
            }
            if(profile.canalBanks)
            {
                bm=new Material(bs){hideFlags=HideFlags.DontSave};owned.Add(bm);bm.SetColor("_BaseColor",profile.bankTint);
                bm.SetFloat("_PanelLength",profile.bankPanelLength);bm.SetFloat("_JointStrength",profile.bankJointStrength);bm.SetFloat("_WetStrength",profile.bankWetStrength);
            }
            var bridgeCells=FindBridgeWaterCells(cells);
            Mesh bridgeMesh=null;
            if(bridgeCells.Count>0)
            {
                cells=new Dictionary<Vector2Int,Cell>(cells);
                var bv=new List<Vector3>();var buv=new List<Vector2>();
                foreach(var cell in bridgeCells.Values)
                {
                    cells[Key(cell.center)]=cell;float h=cell.size*.5f;var c=cell.center;
                    bv.AddRange(new[]{c+new Vector3(-h,0,-h),c+new Vector3(h,0,-h),c+new Vector3(h,0,h),c+new Vector3(-h,0,h)});
                    buv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});
                }
                bridgeMesh=new Mesh{name="JC bridge water source",hideFlags=HideFlags.DontSave};bridgeMesh.SetVertices(bv);bridgeMesh.SetUVs(0,buv);owned.Add(bridgeMesh);
                OpenBridgeTerrainSides(sources,bridgeCells);
            }
            if(wm!=null)BuildWaterFlowMap(cells,wm);
            var edges=new List<BankEdge>();int excluded=0;
            foreach(var c in cells.Values.Where(c=>c.kind==5))for(int d=0;d<4;d++)
            {
                if(!cells.TryGetValue(Neighbor(c,d),out var land)||land.kind<1||land.kind>4)continue;
                var n=new Vector3(dirs[d].x,0,dirs[d].y);var tangent=d<2?Vector3.forward:Vector3.right;var center=c.center+n*c.size*.5f;center.y=land.center.y;
                bool active=profile.canalBanks&&land.center.y>c.center.y+.04f&&!profile.bankExcludedLandCells.Contains(new Vector2Int(Mathf.RoundToInt(land.center.x),Mathf.RoundToInt(land.center.z)));
                if(!active)excluded++;
                edges.Add(new BankEdge{a=center-tangent*c.size*.5f,b=center+tangent*c.size*.5f,waterY=c.center.y,width=Mathf.Clamp(profile.bankWidth,.05f,c.size*.45f),enabled=active});
                waterBoundaries.Add(new WaterBoundaryEntry{position=center,category=active?"콘크리트 호안":"호안 제외 · 물가"});
            }
            for(int chunk=0;chunk<water.Length+(bridgeMesh!=null?1:0);chunk++)
            {
                bool underBridge=chunk==water.Length;var f=underBridge?water[0]:water[chunk];
                var mesh=underBridge?bridgeMesh:f.sharedMesh;var vs=mesh.vertices;var uv=mesh.uv;var builder=new WaterfrontMesh();
                for(int i=0;i<vs.Length;i+=4)
                {
                    var lo=underBridge?vs[i]:f.transform.TransformPoint(vs[i]);var hi=underBridge?vs[i+2]:f.transform.TransformPoint(vs[i+2]);var center=(lo+hi)*.5f;float size=Mathf.Abs(hi.x-lo.x);
                    var nearby=edges.Where(e=>Mathf.Abs((e.a.x+e.b.x)*.5f-center.x)<size*2&&Mathf.Abs((e.a.z+e.b.z)*.5f-center.z)<size*2).ToArray();
                    float width=profile.canalBanks?Mathf.Clamp(profile.bankWidth,.05f,size*.45f):0,cap=Mathf.Min(profile.bankCapWidth,width*.35f);
                    int steps=nearby.Any(e=>e.enabled)?10:2;
                    Func<int,int,WaterVertex> sample=(x,z)=>
                    {
                        var p=new Vector3(Mathf.Lerp(lo.x,hi.x,x/(float)steps),lo.y,Mathf.Lerp(lo.z,hi.z,z/(float)steps));float distance=100,landY=lo.y;
                        foreach(var edge in nearby)
                        {
                            var delta=edge.b-edge.a;delta.y=0;var rel=p-edge.a;rel.y=0;var closest=edge.a+delta*Mathf.Clamp01(Vector3.Dot(rel,delta)/delta.sqrMagnitude);closest.y=p.y;
                            float dist=Vector3.Distance(p,closest);
                            // 비활성 경계는 수면 색에만 반영하며 경사면을 만들지 않습니다.
                            if(!edge.enabled)dist+=width;
                            if(dist<distance){distance=dist;landY=edge.a.y;}
                        }
                        var tex=Vector2.Lerp(Vector2.Lerp(uv[i],uv[i+1],x/(float)steps),Vector2.Lerp(uv[i+3],uv[i+2],x/(float)steps),z/(float)steps);
                        return new WaterVertex{p=p,uv=tex,distance=distance,landY=landY};
                    };
                    Action<WaterVertex,WaterVertex,WaterVertex> triangle=(a,b,c)=>
                    {
                        var polygon=new List<WaterVertex>{a,b,c};builder.Polygon(ClipWater(polygon,width,true),0,width,cap);
                        if(width<=0)return;
                        float[] limits={0,cap,width*.85f,width};
                        // 교량 아래의 호안 상단은 유지되는 도로 상판과 같은 높이로 겹칩니다.
                        // 수면과 경사면은 유지하고, 도로가 덮는 수평 상단 띠만 생성하지 않습니다.
                        for(int band=underBridge?1:0;band<3;band++)builder.Polygon(ClipWater(ClipWater(polygon,limits[band],true),limits[band+1],false),1,width,cap);
                    };
                    for(int z=0;z<steps;z++)for(int x=0;x<steps;x++){var a=sample(x,z);var b=sample(x+1,z);var c=sample(x+1,z+1);var d=sample(x,z+1);triangle(a,c,b);triangle(a,d,c);}
                }
                var result=builder.Make(underBridge?"JC bridge flowing water":"JC canal · "+f.name);owned.Add(result);
                var go=new GameObject(underBridge?"수로 · 교량 하부 연결 수면":"수로 · "+f.name){hideFlags=HideFlags.DontSave};go.transform.SetParent(output.transform,false);go.layer=f.gameObject.layer;go.AddComponent<MeshFilter>().sharedMesh=result;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{wm!=null?wm:f.GetComponent<Renderer>().sharedMaterial,bm!=null?bm:f.GetComponent<Renderer>().sharedMaterial};renderer.shadowCastingMode=ShadowCastingMode.Off;
                if(!underBridge){var original=f.GetComponent<Renderer>();originals[original]=original.forceRenderingOff;original.forceRenderingOff=true;}
            }
            if(profile.canalBanks)RemoveCoveredBankWalls(sources,edges.Where(e=>e.enabled).ToArray());
            return $"수면 {water.Length}청크, 호안 {edges.Count-excluded}변, 제외 {excluded}변, 교량 하부 {bridgeCells.Count}셀";
        }
        Dictionary<Vector2Int,Cell> FindBridgeWaterCells(Dictionary<Vector2Int,Cell> cells)
        {
            var result=new Dictionary<Vector2Int,Cell>();if(!profile.bridgeWaterPassage)return result;
            var bridges=gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Where(t=>t.name=="Bridge"||t.name.StartsWith("Bridge(")).ToArray();
            foreach(var bridge in bridges)
            {
                var renderers=bridge.GetComponentsInChildren<Renderer>();if(renderers.Length==0)continue;
                var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                foreach(var cell in cells.Values.Where(c=>c.kind==1))
                {
                    if(cell.center.x<bounds.min.x||cell.center.x>bounds.max.x||cell.center.z<bounds.min.z||cell.center.z>bounds.max.z)continue;
                    for(int axis=0;axis<2;axis++)
                    {
                        var delta=axis==0?Vector3.right:Vector3.forward;var waters=new List<Cell>();
                        foreach(int sign in new[]{-1,1})for(int step=1;step<=4;step++)
                        {
                            var position=cell.center+delta*(sign*step*cell.size);
                            if(!cells.TryGetValue(Key(position),out var next))break;
                            if(next.kind==5){waters.Add(next);break;}
                            if(next.kind!=1||position.x<bounds.min.x||position.x>bounds.max.x||position.z<bounds.min.z||position.z>bounds.max.z)break;
                        }
                        if(waters.Count!=2||Mathf.Abs(waters[0].center.y-waters[1].center.y)>.01f)continue;
                        var water=cell;water.kind=5;water.center.y=(waters[0].center.y+waters[1].center.y)*.5f;water.texture=waters[0].texture;
                        result[Key(cell.center)]=water;break;
                    }
                }
            }
            return result;
        }
        void OpenBridgeTerrainSides(MeshFilter[] sources,Dictionary<Vector2Int,Cell> bridgeCells)
        {
            foreach(var source in sources.Where(f=>Kind(f)==1&&Stride(f)==20))
            {
                var sourceVertices=source.sharedMesh.vertices;var groups=new List<int>();
                for(int i=0;i<sourceVertices.Length;i+=20)if(bridgeCells.ContainsKey(Key((source.transform.TransformPoint(sourceVertices[i])+source.transform.TransformPoint(sourceVertices[i+2]))*.5f)))groups.Add(i);
                if(groups.Count==0)continue;
                bool hasCopy=visualCopies.TryGetValue(source,out var copy);var filter=hasCopy?copy:source;var original=filter.sharedMesh;
                var vertices=original.vertices.ToList();var uv=original.uv.ToList();var triangles=original.GetTriangles(0).ToList();
                foreach(int first in groups)
                {
                    float top=filter.transform.TransformPoint(vertices[first]).y;float bottom=top-profile.bridgeDeckThickness;
                    for(int j=0;j<20;j++){var world=filter.transform.TransformPoint(vertices[first+j]);world.y=Mathf.Max(world.y,bottom);vertices[first+j]=filter.transform.InverseTransformPoint(world);}
                    int start=vertices.Count;
                    for(int j=0;j<4;j++){var world=filter.transform.TransformPoint(vertices[first+j]);world.y=bottom;vertices.Add(filter.transform.InverseTransformPoint(world));uv.Add(uv[first+j]);}
                    triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
                }
                var replacement=Instantiate(original);replacement.name=original.name+"_BridgeDeck";replacement.hideFlags=HideFlags.DontSave;owned.Add(replacement);
                replacement.SetVertices(vertices);replacement.SetUVs(0,uv);replacement.SetTriangles(triangles,0);replacement.RecalculateNormals();replacement.RecalculateTangents();replacement.RecalculateBounds();
                if(hasCopy)filter.sharedMesh=replacement;else CloneVisual(source,replacement,source.GetComponent<MeshRenderer>().sharedMaterials);
            }
        }
        void RemoveCoveredBankWalls(MeshFilter[] sources,BankEdge[] edges)
        {
            foreach(var source in sources)
            {
                if(Kind(source)<1||Kind(source)>4)continue;
                var bounds=source.GetComponent<Renderer>().bounds;var candidates=edges.Where(e=>e.a.x>=bounds.min.x-.01f&&e.a.x<=bounds.max.x+.01f&&e.a.z>=bounds.min.z-.01f&&e.a.z<=bounds.max.z+.01f).ToArray();if(candidates.Length==0)continue;
                bool hasCopy=visualCopies.TryGetValue(source,out var copy);var filter=hasCopy?copy:source;
                var m=filter.sharedMesh;var vertices=m.vertices;var world=vertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();var lists=new List<int>[m.subMeshCount];int removed=0;
                for(int sub=0;sub<m.subMeshCount;sub++)
                {
                    lists[sub]=new List<int>();var indices=m.GetTriangles(sub);
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        var a=world[indices[i]];var b=world[indices[i+1]];var c=world[indices[i+2]];bool covered=false;
                        if(Mathf.Max(a.y,b.y,c.y)-Mathf.Min(a.y,b.y,c.y)>.02f)foreach(var e in candidates)
                        {
                            var delta=e.b-e.a;delta.y=0;float len=delta.magnitude;var tangent=delta/len;var normal=new Vector3(-tangent.z,0,tangent.x);
                            if(new[]{a,b,c}.All(p=>Mathf.Abs(Vector3.Dot(p-e.a,normal))<.0001f&&Vector3.Dot(p-e.a,tangent)>=-.0001f&&Vector3.Dot(p-e.a,tangent)<=len+.0001f&&p.y<=e.a.y+.0001f)){covered=true;break;}
                        }
                        if(covered)removed++;else lists[sub].AddRange(new[]{indices[i],indices[i+1],indices[i+2]});
                    }
                }
                if(removed==0)continue;
                var replacement=Instantiate(m);replacement.name=m.name+"_BankWallRemoved";replacement.hideFlags=HideFlags.DontSave;owned.Add(replacement);for(int sub=0;sub<lists.Length;sub++)replacement.SetTriangles(lists[sub],sub);
                if(hasCopy)filter.sharedMesh=replacement;else CloneVisual(source,replacement,source.GetComponent<MeshRenderer>().sharedMaterials);
            }
        }
    }
}

#endif
