using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace JC.BattleTesting
{
    public static class JcDynamicBattleGeometry
    {
        static readonly HashSet<string> roots = new HashSet<string>{"Land","DecorativeSystemAndRoot","JC_WorldDressing","GateSystemAndRoot","HeroUnionSystem","OutpostSystemAndRoot","VillainUnionSystem","ObstacleRoot"};
        static bool Ancestor(Transform t,string name){for(;t!=null;t=t.parent)if(t.name==name)return true;return false;}
        static bool Character(Transform t){for(;t!=null;t=t.parent)if(t.GetComponent<BattleCharactor>()!=null||t.GetComponent<EnemyGridMover>()!=null||t.name.StartsWith("Armature",StringComparison.OrdinalIgnoreCase))return true;return false;}
        static string Group(Transform t){for(var p=t;p!=null;p=p.parent)if(p.name.EndsWith("(Clone)",StringComparison.Ordinal))return p.GetInstanceID().ToString();return t.GetInstanceID().ToString();}

        // 탐사 원본은 유지하고 전투 배경 입력에서만 차단물 외형을 제외합니다.
        static bool Barricade(Transform transform)
        {
            for (var current = transform; current != null; current = current.parent)
            {
                var placement = current.GetComponent<DecorativeObjectPlacement>();
                if (placement != null && string.Equals(placement.PrefabKey, "barrier", StringComparison.OrdinalIgnoreCase))
                    return true;
                // 게이트 내부 모델에는 배치 컴포넌트가 없는 경우도 있습니다.
                if (string.Equals(current.name, "barrier", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(current.name, "barrier(Clone)", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        // 입력 씬이 살아 있는 동안 Capture 결과를 Build에 전달합니다. 결과는 자원을 독립 소유합니다.
        public static JcDynamicBattleSource Capture(Scene scene, bool tutorial = false)
        {
            if(!scene.IsValid()||!scene.isLoaded)throw new InvalidOperationException("탐사 입력 씬이 로드되지 않았습니다.");
            var data=ScriptableObject.CreateInstance<JcDynamicBattleSource>();data.hideFlags=HideFlags.DontSave;data.sourceScene=scene.path;
            var parts=new List<JcDynamicBattleSource.Part>();var roads=new List<JcDynamicBattleSource.Road>();
            foreach(var f in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshFilter>()))
            {
                var r=f.GetComponent<MeshRenderer>();if(r==null||!r.enabled||r.forceRenderingOff||f.sharedMesh==null||Character(f.transform)||Barricade(f.transform))continue;
                bool tutorialSurface = tutorial && f.transform.parent != null && f.transform.parent.name == "Tutorial_VisualOnly";
                bool ground=Ancestor(f.transform,"Generated Tile Meshes")||Ancestor(f.transform,"JC_ExploreDetail_Visuals")||tutorialSurface;
                if(!ground&&!roots.Contains(f.transform.root.name))continue;
                if(f.GetComponentInParent<JcDynamicBattleOutput>()!=null)continue;
                var inputMesh=f.sharedMesh;
                if(!Application.isEditor&&!inputMesh.isReadable)
                {
                    var cache=Resources.Load<JcDynamicBattleSource>(tutorial ? "JC_Encounters/TutorialDynamicSource" : "JC_Encounters/DynamicSource");
                    var readable=cache!=null?cache.parts.FirstOrDefault(part=>part.originalMesh==inputMesh&&part.mesh!=null&&part.mesh.isReadable):null;
                    if(readable==null)throw new InvalidOperationException("읽기 가능한 외형 캐시를 갱신해 주세요: "+f.name);
                    inputMesh=readable.mesh;
                }
                var part=new JcDynamicBattleSource.Part{name=f.name,group=Group(f.transform),mesh=inputMesh,originalMesh=f.sharedMesh,materials=r.sharedMaterials,matrix=f.transform.localToWorldMatrix,bounds=r.bounds,terrain=ground,renderer=r};
                parts.Add(part);
                // 비이동 타일의 시각적 도로 보강도 배경 배치 후보로만 사용합니다. 레벨/이동 데이터는 수정하지 않습니다.
                if (tutorialSurface && f.name.StartsWith("BlockedRoad_", StringComparison.Ordinal))
                    roads.Add(new JcDynamicBattleSource.Road { center = r.bounds.center, size = Mathf.Min(r.bounds.size.x, r.bounds.size.z) });
            }
            // 외형 보강으로 숨겨진 원본 청크에서도 도로 측정 좌표를 읽습니다.
            foreach(var f in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshFilter>()).Where(f=>f.name.StartsWith("TileChunk_",StringComparison.Ordinal)&&f.sharedMesh!=null))
            {
                var r=f.GetComponent<MeshRenderer>();var mat=r!=null?r.sharedMaterial:null;
                var tex=mat!=null&&mat.HasProperty("_BaseMap")?mat.GetTexture("_BaseMap"):null;
                if(tex==null||!tex.name.StartsWith("Road_",StringComparison.Ordinal))continue;
                var v=f.sharedMesh.vertices;int stride=f.name.EndsWith("Cube",StringComparison.Ordinal)?20:4;
                for(int i=0;i+2<v.Length;i+=stride){var a=f.transform.TransformPoint(v[i]);var b=f.transform.TransformPoint(v[i+2]);roads.Add(new JcDynamicBattleSource.Road{center=(a+b)*.5f,size=Mathf.Abs(b.x-a.x)});}
                if(data.roadMaterial==null||tex.name=="Road_01")data.roadMaterial=mat;
            }
            data.parts=parts.ToArray();data.roads=roads.ToArray();return data;
        }
        struct Vertex
        {
            public Vector3 p,n;public Vector2 uv;public Vector4 data;public Color color;
            public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t),uv=Vector2.Lerp(a.uv,b.uv,t),data=Vector4.Lerp(a.data,b.data,t),color=Color.Lerp(a.color,b.color,t)};
        }
        static List<Vertex> Clip(List<Vertex> input,int axis,float boundary,bool greater)
        {
            var result=new List<Vertex>();if(input.Count==0)return result;var a=input[input.Count-1];bool insideA=greater?a.p[axis]>=boundary:a.p[axis]<=boundary;
            foreach(var b in input){bool insideB=greater?b.p[axis]>=boundary:b.p[axis]<=boundary;if(insideA!=insideB)result.Add(Vertex.Lerp(a,b,(boundary-a.p[axis])/(b.p[axis]-a.p[axis])));if(insideB)result.Add(b);a=b;insideA=insideB;}return result;
        }
        static Mesh Terrain(JcDynamicBattleSource.Part part,Matrix4x4 transform,float extent)
        {
            var src=part.mesh;var verts=src.vertices;var normals=src.normals;var uv=src.uv;var data=new List<Vector4>();src.GetUVs(1,data);var colors=src.colors;
            var vs=new List<Vector3>();var ns=new List<Vector3>();var us=new List<Vector2>();var ds=new List<Vector4>();var cs=new List<Color>();var subs=new List<int>[src.subMeshCount];
            Func<int,Vertex> vertex=i=>{var p=transform.MultiplyPoint3x4(verts[i]);return new Vertex{p=p,n=normals.Length==verts.Length?transform.MultiplyVector(normals[i]).normalized:Vector3.up,uv=uv.Length==verts.Length?uv[i]:Vector2.zero,data=data.Count==verts.Length?data[i]:Vector4.zero,color=colors.Length==verts.Length?colors[i]:Color.white};};
            for(int sub=0;sub<src.subMeshCount;sub++)
            {
                var indices=src.GetTriangles(sub);subs[sub]=new List<int>();
                Action<List<Vertex>> emit=poly=>{for(int j=1;j<poly.Count-1;j++)foreach(var v in new[]{poly[0],poly[j],poly[j+1]}){subs[sub].Add(vs.Count);vs.Add(v.p);ns.Add(v.n);us.Add(v.uv);ds.Add(v.data);cs.Add(v.color);}};
                for(int i=0;i<indices.Length;i+=3)
                {
                    var poly=new List<Vertex>{vertex(indices[i]),vertex(indices[i+1]),vertex(indices[i+2])};
                    poly=Clip(Clip(Clip(Clip(poly,0,-extent,true),0,extent,false),2,-extent,true),2,extent,false);
                    emit(poly);
                }
            }
            if(vs.Count==0)return null;
            var mesh=new Mesh{name="Dynamic clipped terrain",indexFormat=IndexFormat.UInt32,hideFlags=HideFlags.DontSave};mesh.SetVertices(vs);mesh.SetNormals(ns);mesh.SetUVs(0,us);mesh.SetUVs(1,ds);mesh.SetColors(cs);mesh.subMeshCount=subs.Length;for(int i=0;i<subs.Length;i++)mesh.SetTriangles(subs[i],i);mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }
        public static void Validate(JcDynamicBattleSource source,JcDynamicBattleSettings settings)
        {
            if(source==null||source.parts==null||source.parts.Length==0||source.roadMaterial==null)throw new InvalidOperationException("공통 탐사 외형 입력을 먼저 수집해 주세요.");
            if(settings==null)throw new InvalidOperationException("동적 배경 설정이 없습니다.");
            var p=settings.encounterPosition;
            var values=new[]{p.x,p.y,p.z,settings.enemyPosition.x,settings.enemyPosition.y,settings.enemyPosition.z,settings.scale,settings.captureRadius,settings.battleWidth,settings.battleLength,settings.clearance,settings.anchorSearchRadius,settings.maxAutoScale};
            if(values.Any(v=>float.IsNaN(v)||float.IsInfinity(v))||settings.scale<1||settings.scale>8||settings.captureRadius<5||settings.captureRadius>40||settings.battleWidth<10||settings.battleWidth>40||settings.battleLength<20||settings.battleLength>80||settings.clearance<0||settings.clearance>20||settings.anchorSearchRadius<0||settings.anchorSearchRadius>20||settings.maxAutoScale<settings.scale||settings.maxAutoScale>8)throw new InvalidOperationException("동적 배경 입력 범위를 확인해 주세요. 반경5~40, 폭10~40, 길이20~80, 여유0~20입니다.");
            if(!source.roads.Any(r=>Vector2.Distance(new Vector2(r.center.x,r.center.z),new Vector2(p.x,p.z))<=settings.anchorSearchRadius+ r.size))throw new InvalidOperationException("탐색 범위에 전투 기준점을 배치할 도로가 없습니다.");
        }
        public struct Placement
        {
            public Vector3 center;
            public float scale;
            public bool alongX;
        }

        // 배경을 변형하지 않고, 도로 안에 진형 사각형이 들어가는 가까운 위치를 찾습니다.
        public static Placement Resolve(JcDynamicBattleSource source,JcDynamicBattleSettings settings,Vector3[] cameras=null)
        {
            Validate(source,settings);
            var requested=settings.encounterPosition;
            var roadIndex=new Dictionary<Vector2Int,List<JcDynamicBattleSource.Road>>();
            foreach(var road in source.roads)
            {
                var lo=road.center-Vector3.one*(road.size*.5f);var hi=road.center+Vector3.one*(road.size*.5f);
                for(int x=Mathf.FloorToInt(lo.x);x<=Mathf.FloorToInt(hi.x);x++)
                for(int z=Mathf.FloorToInt(lo.z);z<=Mathf.FloorToInt(hi.z);z++)
                {var key=new Vector2Int(x,z);if(!roadIndex.TryGetValue(key,out var list))roadIndex[key]=list=new List<JcDynamicBattleSource.Road>();list.Add(road);}
            }
            Func<Vector3,bool> roadAt=q=>roadIndex.TryGetValue(new Vector2Int(Mathf.FloorToInt(q.x),Mathf.FloorToInt(q.z)),out var list)&&list.Any(r=>Mathf.Abs(r.center.x-q.x)<=r.size*.5f+.001f&&Mathf.Abs(r.center.z-q.z)<=r.size*.5f+.001f&&Mathf.Abs(r.center.y-q.y)<.05f);
            var candidates=new List<Vector3>{requested};
            foreach(var r in source.roads.Where(r=>Vector2.Distance(new Vector2(r.center.x,r.center.z),new Vector2(requested.x,requested.z))<=settings.anchorSearchRadius))
            {
                candidates.Add(r.center);
                // 도로 중앙이 타일 사이에 놓인 경우를 위해 반 셀 후보도 검사합니다.
                foreach(var d in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})candidates.Add(r.center+d*(r.size*.5f));
            }
            float best=float.PositiveInfinity;var answer=new Placement();bool found=false;
            foreach(var c0 in candidates.Distinct())
            {
                var near=source.roads.FirstOrDefault(r=>Mathf.Abs(r.center.x-c0.x)<=r.size*.501f&&Mathf.Abs(r.center.z-c0.z)<=r.size*.501f);
                var c=c0;c.y=near.center.y;float distance=Vector2.Distance(new Vector2(c.x,c.z),new Vector2(requested.x,requested.z));
                if(distance>settings.anchorSearchRadius)continue;
                for(int direction=0;direction<2;direction++)
                for(float scale=settings.scale;scale<=settings.maxAutoScale+.001f;scale+=.25f)
                {
                    if (settings.axis == JcBattleRoadAxis.X && direction != 0 ||
                        settings.axis == JcBattleRoadAxis.Z && direction != 1) continue;
                    var approach=settings.enemyPosition-requested;
                    bool preferX=Mathf.Abs(approach.x)>=Mathf.Abs(approach.z);
                    float score=distance+(scale-settings.scale)*1.5f+((direction==0)==preferX?0:.0001f);
                    if(score>=best)continue;
                    bool alongX=direction==0;
                    float halfW=(settings.battleWidth*.5f+.25f)/scale,halfL=(settings.battleLength*.5f+.25f)/scale;
                    float hx=alongX?halfL:halfW,hz=alongX?halfW:halfL;
                    bool fits=true;
                    int nx=Mathf.CeilToInt(2*hx/.2f),nz=Mathf.CeilToInt(2*hz/.2f);
                    for(int ix=0;ix<=nx&&fits;ix++)for(int iz=0;iz<=nz;iz++)
                        if(!roadAt(c+new Vector3(Mathf.Lerp(-hx,hx,(float)ix/nx),0,Mathf.Lerp(-hz,hz,(float)iz/nz)))){fits=false;break;}
                    if(!fits)continue;
                    var footprint=new Bounds(c+Vector3.up*(1.5f/scale),new Vector3(hx*2,3/scale,hz*2));
                    if(source.parts.Any(p=>!p.terrain&&p.bounds.Intersects(footprint)))continue;
                    // 카메라에서 각 진형의 몸통으로 향하는 시선이 가려지는 후보는 제외합니다.
                    var inverse=Quaternion.Euler(0,alongX?90:0,0);
                    if(cameras!=null)
                    foreach(var camera in cameras)
                    {
                        var eye=c+inverse*camera/scale;
                        foreach(float x in new[]{-3.3f,0,3.3f})foreach(float z in new[]{-7.3f,-4f,4f,7.3f})
                        {
                            var target=c+inverse*new Vector3(x,1.3f,z)/scale;var delta=target-eye;
                            var ray=new Ray(eye,delta.normalized);float length=delta.magnitude;
                            if(source.parts.Any(part=>
                            {
                                if(part.terrain)return false;
                                var obstacle=part.bounds;
                                // 몸통 중심뿐 아니라 캐릭터 외곽도 보이도록 전투 기준 0.5만큼 여유를 둡니다.
                                obstacle.Expand(1f/scale);
                                return obstacle.Contains(eye)||(obstacle.IntersectRay(ray,out float hit)&&hit<length);
                            }))fits=false;
                        }
                        if(!fits)break;
                    }
                    if(!fits)continue;
                    best=score;found=true;answer=new Placement{center=c,scale=scale,alongX=alongX};
                    break;
                }
            }
            if(!found)throw new InvalidOperationException("주변 도로에 진형을 수용할 공간이 없습니다. 기준점 탐색 반경 또는 최대 확대 배율을 조절해 주세요. 원본 도로는 변형하지 않습니다.");
            return answer;
        }
        public static GameObject Build(JcDynamicBattleSource source,JcDynamicBattleSettings settings,Transform owner=null,Vector3[] cameras=null)
        {
            var placement=Resolve(source,settings,cameras);var p=placement.center;float scale=placement.scale;
            var rotation=Quaternion.Euler(0,placement.alongX?-90:0,0);
            var transform=Matrix4x4.TRS(Vector3.zero,rotation,Vector3.one*scale)*Matrix4x4.Translate(-p);
            var root=new GameObject("JC_DynamicEncounter"){hideFlags=HideFlags.DontSave};if(owner!=null)root.transform.SetParent(owner,false);root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=Vector3.one;
            var output=root.AddComponent<JcDynamicBattleOutput>();
            try
            {
                float extent=Mathf.Max(settings.captureRadius*scale,settings.battleLength*.5f+6);
                var selected=source.parts.Where(x=>x.mesh!=null&&x.bounds.SqrDistance(p)<settings.captureRadius*settings.captureRadius).ToArray();
                foreach(var part in selected)
                {
                    Mesh mesh;
                    if(part.terrain)mesh=Terrain(part,transform*part.matrix,extent);
                    else {mesh=UnityEngine.Object.Instantiate(part.mesh);var v=mesh.vertices;var matrix=transform*part.matrix;for(int i=0;i<v.Length;i++)v[i]=matrix.MultiplyPoint3x4(v[i]);mesh.vertices=v;var normals=mesh.normals;var normalMatrix=matrix.inverse.transpose;for(int i=0;i<normals.Length;i++)normals[i]=normalMatrix.MultiplyVector(normals[i]).normalized;mesh.normals=normals;mesh.RecalculateBounds();mesh.RecalculateTangents();}
                    if(mesh==null)continue;output.Add(part.name,mesh,part.materials,part.terrain,part.renderer);
                }
                output.Report=$"조우 {settings.encounterPosition} → 기준점 {p} / 이동 {Vector3.Distance(new Vector3(settings.encounterPosition.x,p.y,settings.encounterPosition.z),p):F2} / 방향 {(placement.alongX?"X":"Z")} / 동일 확대 {scale:F2} / 원형 보존 외형 {selected.Length}";
                if (settings.addBattleSurfaceDetails) JcBattleSurfaceDressing.Apply(root,Mathf.RoundToInt(settings.encounterPosition.x*1000)^Mathf.RoundToInt(settings.encounterPosition.z*7919),cameras);
                Debug.Log(output.Report,root);
                return root;
            }
            catch{if(Application.isPlaying)UnityEngine.Object.Destroy(root);else UnityEngine.Object.DestroyImmediate(root);throw;}
        }
    }
}
