using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;
namespace JC.BattleTesting
{
    // 위치 선정 및 원본 지형 변환이 끝난 외형에만 적용하는 전투 전용 후처리입니다.
    [ExecuteAlways]
    public sealed partial class JcBattleSurfaceDressing : MonoBehaviour
    {
        readonly List<Object> owned=new List<Object>();
        public int RoadCount {get;private set;}
        public int CrosswalkCount {get;private set;}
        public int NatureCount {get;private set;}
        public string ScatterReport {get;private set;}
        public static JcBattleSurfaceProfile Profile=>Resources.Load<JcBattleSurfaceProfile>("JC_BattleSurface_Default");
        public static void Apply(GameObject root,int seed,Vector3[] cameras=null,bool scatter=true)
        {
            var p=Profile;if(p==null||!p.enableDetails||root.GetComponent<JcBattleSurfaceDressing>()!=null)return;
            var c=root.AddComponent<JcBattleSurfaceDressing>();
            c.Build(p,seed,cameras,scatter);
        }
        public static bool IsCrosswalk(Material m)=>TextureName(m).StartsWith("Road_Cross_",StringComparison.Ordinal);
        static string TextureName(Material m)=>m!=null&&m.HasProperty("_BaseMap")&&m.GetTexture("_BaseMap")!=null?m.GetTexture("_BaseMap").name:"";
        public static void WhiteCrosswalk(Material material,float emboss=0)
        {
            var p=Profile;if(p==null||p.roadShader==null)return;
            material.shader=p.roadShader;material.SetFloat("_MarkMode",1);material.SetFloat("_Emboss",emboss);
        }
        void Build(JcBattleSurfaceProfile p,int seed,Vector3[] cameras,bool scatter)
        {
            var filters=GetComponentsInChildren<MeshFilter>();
            if(GetComponent<JcDynamicBattleOutput>()!=null)PrepareLeftVerge(filters,p);
            // 원래 표시 타일과 사이의 무표시 도로를 하나의 구간으로 연결합니다.
            BuildConnectedRoadLines(filters,p);
            foreach(var f in filters)
            {
                var r=f.GetComponent<MeshRenderer>();if(r==null||f.sharedMesh==null)continue;
                var mats=r.sharedMaterials;bool changed=false;
                for(int i=0;i<mats.Length;i++)
                {
                    var m=mats[i];string name=TextureName(m);
                    bool meshLine=f.name=="Yellow center line";
                    bool cross=IsCrosswalk(m),line=meshLine||name.StartsWith("Road_Centerline_",StringComparison.Ordinal);
                    // 주차장, 교차로의 무표시 도로, 흙길은 차선 추가 대상이 아닙니다.
                    if(!cross&&!line)continue;
                    if(p.roadShader==null||(!cross&&p.plainAsphalt==null))continue;
                    if(meshLine && f.sharedMesh.isReadable)
                    {
                        // 디테일 키트의 좁은 도색 메시는 타일 UV(0~.096)를 사용한다.
                        // 복사본만 폭을 확보하고 UV를 0~1로 맞춰 셰이더의 중앙 .5와 일치시킨다.
                        var expanded=Instantiate(f.sharedMesh);expanded.name=f.sharedMesh.name+"_BattleLineWidth";
                        expanded.hideFlags=HideFlags.DontSave;owned.Add(expanded);
                        var bounds=expanded.bounds;var vertices=expanded.vertices;var uv=new Vector2[vertices.Length];
                        float actualWidth=f.transform.TransformVector(Vector3.right*bounds.size.x).magnitude;
                        float factor=Mathf.Max(1,p.lineWidth*1.1f/Mathf.Max(.0001f,actualWidth));
                        for(int v=0;v<vertices.Length;v++)
                        {
                            uv[v]=new Vector2(Mathf.InverseLerp(bounds.min.x,bounds.max.x,vertices[v].x),Mathf.InverseLerp(bounds.min.z,bounds.max.z,vertices[v].z));
                            vertices[v].x=bounds.center.x+(vertices[v].x-bounds.center.x)*factor;
                        }
                        expanded.vertices=vertices;expanded.uv=uv;expanded.RecalculateBounds();expanded.RecalculateTangents();f.sharedMesh=expanded;
                    }
                    var clone=new Material(m){name=m.name+"_BattleMarkings",hideFlags=HideFlags.DontSave};owned.Add(clone);mats[i]=clone;changed=true;
                    if(cross){WhiteCrosswalk(clone,p.crosswalkEmboss);CrosswalkCount++;continue;}
                    if(!LineFrame(f,i,meshLine?"Road_Centerline_006":name,out var normal,out var offset,out var tangent,out var width)){mats[i]=m;continue;}
                    clone.shader=p.roadShader;clone.SetFloat("_MarkMode",meshLine?3:2);clone.SetTexture("_PlainMap",p.plainAsphalt);
                    clone.SetVector("_LineNormal",new Vector4(normal.x,normal.y,offset,0));clone.SetVector("_LineWorld",tangent);
                    // 텍스처 차선은 별도 연속 구간으로 그립니다. 원본 타일에서는 도색만 제거합니다.
                    clone.SetFloat("_TileMeters",width);clone.SetFloat("_LineWidth",meshLine?p.lineWidth:-1);clone.SetFloat("_DashLength",p.dashLength);clone.SetFloat("_DashGap",p.dashGap);RoadCount++;
                }
                if(changed)r.sharedMaterials=mats;
            }
            if(scatter&&p.scatterNature&&p.naturePrefabs!=null)Scatter(filters,p,seed,cameras);
        }
        static bool LineFrame(MeshFilter f,int sub,string name,out Vector2 normal,out float offset,out Vector3 tangent,out float width)
        {
            normal=Vector2.up;offset=0;tangent=Vector3.right;width=1;
            if(name.EndsWith("002"))offset=1;
            else if(name.EndsWith("003")){normal=Vector2.right;offset=1;}
            else if(name.EndsWith("004"))normal=Vector2.right;
            else if(name.EndsWith("005"))offset=.5f;
            else if(name.EndsWith("006")){normal=Vector2.right;offset=.5f;}
            else if(name.EndsWith("007")){normal=new Vector2(1,1).normalized;offset=Mathf.Sqrt(.5f);}
            else if(!name.EndsWith("001"))return false;
            var mesh=ReadableRoadMesh(f.sharedMesh);if(!mesh.isReadable||sub>=mesh.subMeshCount)return false;
            var v=mesh.vertices;var uv=mesh.uv;var tr=mesh.GetTriangles(sub);if(uv.Length!=v.Length)return false;
            for(int i=0;i<tr.Length;i+=3)
            {
                int a=tr[i],b=tr[i+1],c=tr[i+2];var u=uv[b]-uv[a];var w=uv[c]-uv[a];float det=u.x*w.y-u.y*w.x;if(Mathf.Abs(det)<1e-6f)continue;
                var ab=f.transform.TransformVector(v[b]-v[a]);var ac=f.transform.TransformVector(v[c]-v[a]);
                var du=(ab*w.y-ac*u.y)/det;var dv=(ac*u.x-ab*w.x)/det;
                tangent=du*-normal.y+dv*normal.x;float length=tangent.magnitude;if(length<.001f)continue;
                width=Vector3.Cross(du,dv).magnitude/length;tangent/=length;
                if((Mathf.Abs(tangent.x)>Mathf.Abs(tangent.z)?tangent.x:tangent.z)<0)tangent=-tangent;
                return width>.001f;
            }
            return false;
        }
        struct Triangle
        {
            public Vector3 a,b,c;public float cumulative;public Renderer support;
            public bool Contains(Vector3 p)
            {
                var v0=c-a;var v1=b-a;var v2=p-a;v0.y=v1.y=v2.y=0;
                float d00=Vector3.Dot(v0,v0),d01=Vector3.Dot(v0,v1),d11=Vector3.Dot(v1,v1),d20=Vector3.Dot(v2,v0),d21=Vector3.Dot(v2,v1),den=d00*d11-d01*d01;
                if(Mathf.Abs(den)<1e-7f)return false;float u=(d11*d20-d01*d21)/den,v=(d00*d21-d01*d20)/den;
                return u>=-.001f&&v>=-.001f&&u+v<=1.001f;
            }
        }
        void Scatter(MeshFilter[] filters,JcBattleSurfaceProfile p,int seed,Vector3[] cameras)
        {
            var grass=new List<Triangle>();float area=0;
            foreach(var f in filters)
            {
                var r=f.GetComponent<MeshRenderer>();var mesh=f.sharedMesh;if(r==null||mesh==null||!mesh.isReadable)continue;
                var mats=r.sharedMaterials;var v=mesh.vertices;
                for(int sub=0;sub<Mathf.Min(mats.Length,mesh.subMeshCount);sub++)
                {
                    string texture=TextureName(mats[sub]);if(texture!="tilemap_04"&&texture!="Grass_Color")continue;
                    var indices=mesh.GetTriangles(sub);
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        var a=f.transform.TransformPoint(v[indices[i]]);var b=f.transform.TransformPoint(v[indices[i+1]]);var c=f.transform.TransformPoint(v[indices[i+2]]);
                        var n=Vector3.Cross(b-a,c-a);if(n.y<=0||n.normalized.y<.94f)continue;
                        area+=n.magnitude*.5f;grass.Add(new Triangle{a=a,b=b,c=c,cumulative=area,support=r});
                    }
                }
            }
            if(grass.Count==0){ScatterReport="풀밭 후보 없음";return;}
            if(leftVerge)PlantVergeGrass(grass,p,seed,cameras);
            var prefabs=p.naturePrefabs.Where(x=>x!=null).ToArray();if(prefabs.Length==0)return;
            var dynamicOutput=GetComponent<JcDynamicBattleOutput>();
            var obstacles=filters.Where(f=>!f.name.Contains("TileChunk_")).Select(f=>f.GetComponent<Renderer>())
                .Where(r=>r!=null&&r.bounds.size.y>.15f&&(dynamicOutput==null||!dynamicOutput.TerrainRenderers.Contains(r))).ToArray();
            var occupied=new List<Vector3>();var random=new System.Random(unchecked(seed+p.seed));
            var group=new GameObject("JC_BattleNature"){hideFlags=HideFlags.DontSave};group.transform.SetParent(transform,false);
            int limit=Mathf.Min(p.propLimit,Mathf.FloorToInt(area/(p.propSpacing*p.propSpacing*3)));
            int near=0,footprint=0,obstacle=0,view=0,empty=0,extraTrees=0;
            var vergeGrass=grass.Where(t=>leftVerge&&Mathf.Min(t.a.x,t.b.x,t.c.x)<vergeEdge).ToArray();
            for(int attempt=0;attempt<(leftVerge?3200:1600);attempt++)
            {
                bool extra=attempt>=1600;
                if(!extra&&NatureCount>=limit)continue;
                if(extra&&(extraTrees>=p.leftExtraTreeLimit||vergeGrass.Length==0))break;
                float choose=(float)random.NextDouble()*area;int index=grass.FindIndex(t=>t.cumulative>=choose);
                var tri=extra?vergeGrass[random.Next(vergeGrass.Length)]:grass[Mathf.Max(0,index)];
                float u=Mathf.Sqrt((float)random.NextDouble()),v=(float)random.NextDouble();var pos=(1-u)*tri.a+u*(1-v)*tri.b+u*v*tri.c;
                if(extra&&(pos.x>vergeEdge-.8f||pos.x<vergeEdge-18||Mathf.Abs(pos.z)>35))continue;
                if(pos.sqrMagnitude>65*65||(Mathf.Abs(pos.x)<8&&Mathf.Abs(pos.z)<22)||occupied.Any(x=>(x-pos).sqrMagnitude<p.propSpacing*p.propSpacing)){near++;continue;}
                bool verge=leftVerge&&pos.x<vergeEdge;
                if(verge&&UnderVergeSlope(pos))continue;
                var choices=verge?prefabs.Where(x=>x.name.StartsWith("Tree",StringComparison.OrdinalIgnoreCase)).ToArray():prefabs;
                if(choices.Length==0)continue;
                var prefab=choices[random.Next(choices.Length)];var renderers=prefab.GetComponentsInChildren<MeshRenderer>();if(renderers.Length==0)continue;
                Bounds local=new Bounds();bool has=false;
                foreach(var rr in renderers)
                {
                    var mf=rr.GetComponent<MeshFilter>();if(mf==null||mf.sharedMesh==null)continue;
                    var b=mf.sharedMesh.bounds;var matrix=prefab.transform.worldToLocalMatrix*rr.transform.localToWorldMatrix;
                    foreach(float x in new[]{-1f,1f})foreach(float y in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})
                    {var point=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));if(!has){local=new Bounds(point,Vector3.zero);has=true;}else local.Encapsulate(point);}
                }
                if(!has||local.size.y<.001f){empty++;continue;}
                bool tree=prefab.name.StartsWith("Tree",StringComparison.OrdinalIgnoreCase);
                float height=tree?2.2f+(float)random.NextDouble()*1.5f:.35f+(float)random.NextDouble()*.5f;
                float scale=height/local.size.y;float radius=Mathf.Max(local.size.x,local.size.z)*scale*.55f+.2f;
                bool safe=true;
                foreach(var corner in new[]{new Vector3(-radius,0,-radius),new Vector3(radius,0,-radius),new Vector3(radius,0,radius),new Vector3(-radius,0,radius)})
                    if(!grass.Any(t=>Mathf.Abs(t.a.y-pos.y)<.15f&&t.Contains(pos+corner))){safe=false;break;}
                if(!safe){footprint++;continue;}
                var bounds=new Bounds(pos+Vector3.up*height*.5f,new Vector3(radius*2,height,radius*2));
                // 산 메시의 평평한 야지 위에 놓을 때 지지면 자체를 장애물로 판정하지 않습니다.
                if(obstacles.Any(r=>r!=tri.support&&!(verge&&r.name.StartsWith("Mount",StringComparison.OrdinalIgnoreCase))&&r.bounds.Intersects(bounds))){obstacle++;continue;}
                if(cameras!=null)foreach(var eye in cameras)foreach(float x in new[]{-3.3f,0,3.3f})foreach(float z in new[]{-7.3f,-4f,4f,7.3f})
                {var delta=new Vector3(x,1.3f,z)-eye;var expanded=bounds;expanded.Expand(1);if(expanded.Contains(eye)||(expanded.IntersectRay(new Ray(eye,delta.normalized),out float hit)&&hit<delta.magnitude))safe=false;}
                if(!safe){view++;continue;}
                var node=new GameObject(prefab.name){hideFlags=HideFlags.DontSave};node.transform.SetParent(group.transform,false);node.transform.position=pos;node.transform.rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);node.transform.localScale=Vector3.one*scale;
                foreach(var rr in renderers)
                {
                    var mf=rr.GetComponent<MeshFilter>();if(mf==null||mf.sharedMesh==null)continue;
                    var child=new GameObject(rr.name){hideFlags=HideFlags.DontSave};child.transform.SetParent(node.transform,false);
                    child.transform.localPosition=prefab.transform.InverseTransformPoint(rr.transform.position)-new Vector3(local.center.x,local.min.y,local.center.z);
                    child.transform.localRotation=Quaternion.Inverse(prefab.transform.rotation)*rr.transform.rotation;child.transform.localScale=rr.transform.lossyScale;
                    child.AddComponent<MeshFilter>().sharedMesh=mf.sharedMesh;child.AddComponent<MeshRenderer>().sharedMaterials=rr.sharedMaterials;
                }
                occupied.Add(pos);NatureCount++;if(extra)extraTrees++;
            }
            ScatterReport=$"풀밭 면적 {area:F0}, 목표 {limit}, 배치 {NatureCount}, 거리 제외 {near}, 경계 제외 {footprint}, 장애물 제외 {obstacle}, 시야 제외 {view}, 빈 메시 {empty}";
        }
        void OnDestroy(){ClearRoadLines();foreach(var o in owned)if(o!=null){if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}}
    }
}
