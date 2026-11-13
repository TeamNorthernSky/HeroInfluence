using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace JC.BattleTesting
{
    public static partial class JcDynamicBattleGeometry
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
        public static JcDynamicBattleSource Capture(Scene scene, bool tutorial = false, bool forCacheGeneration = false)
        {
            if(!scene.IsValid()||!scene.isLoaded)throw new InvalidOperationException("탐사 입력 씬이 로드되지 않았습니다.");
            var data=ScriptableObject.CreateInstance<JcDynamicBattleSource>();data.hideFlags=HideFlags.DontSave;data.sourceScene=scene.path;
            var parts=new List<JcDynamicBattleSource.Part>();var roads=new List<JcDynamicBattleSource.Road>();
            foreach(var f in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MeshFilter>()))
            {
                // JC | 2026-11-04 | 도로·차선을 부분별로 조명할 수 있게 해 가로등 하나의 영향이 도로 전체로 퍼지는 현상을 줄입니다.
                if (f.name == JC.EnvironmentDetail.JcLampSurfaceChunks.VisualName) continue;
                var r=f.GetComponent<MeshRenderer>();if(r==null||f.sharedMesh==null||Character(f.transform)||Barricade(f.transform))continue;
                if(f.GetComponentInParent<JC.EnvironmentDetail.JcRoadMarkingControl>()!=null)continue;
                bool originalRoad=JC.EnvironmentDetail.JcExploreRoadMarkings.TryGetOriginal(r,out var original);
                if(!(originalRoad?original.enabled:r.enabled)||r.forceRenderingOff)continue;
                bool tutorialSurface = tutorial && f.transform.parent != null && f.transform.parent.name == "Tutorial_VisualOnly";
                bool ground=Ancestor(f.transform,"Generated Tile Meshes")||Ancestor(f.transform,"JC_ExploreDetail_Visuals")||tutorialSurface;
                // JC | 2026-11-13 | 튜토리얼 건물의 외형만 포함하며 적·아이템·전투 기능은 복제하지 않습니다.
                bool tutorialBuilding = tutorial && (f.GetComponentInParent<TutorialBuildingObject>() != null || f.GetComponentInParent<TutorialOutpostObject>() != null);
                if(!ground&&!tutorialBuilding&&!roots.Contains(f.transform.root.name))continue;
                if(f.GetComponentInParent<JcDynamicBattleOutput>()!=null)continue;
                var inputMesh=originalRoad?original.mesh:f.sharedMesh;
                // Edit-mode cache generation needs the imported source. Both player and
                // editor Play Mode need writable vertex data to bake the battle transform.
                if (!forCacheGeneration && !inputMesh.isReadable)
                {
                    var cache = Resources.Load<JcDynamicBattleSource>("JC_Encounters/DynamicSource");
                    var readable = cache ? cache.FindReadable(inputMesh) : null;
                    if (!readable && tutorial)
                        readable = Resources.Load<JcDynamicBattleSource>("JC_Encounters/TutorialDynamicSource")?.FindReadable(inputMesh);
                    if (!readable)
                        readable = Resources.Load<JcDynamicBattleSource>("JC_Encounters/DecorationMeshes")?.FindReadable(inputMesh);
                    if (!readable) throw new InvalidOperationException("읽기 가능한 외형 캐시 누락: " + f.name);
                    inputMesh = readable;
                }
                var part=new JcDynamicBattleSource.Part{name=f.name,group=Group(f.transform),mesh=inputMesh,originalMesh=f.sharedMesh,materials=originalRoad?original.materials:r.sharedMaterials,matrix=f.transform.localToWorldMatrix,bounds=r.bounds,terrain=ground,renderer=r};
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
        // Distances are in field space, before the encounter's rotation and scale.
        static Vector3 RoadwardOffset(Vector3 foot, float width, JcDynamicBattleSource.Road[] roads)
        {
            Vector2 position = new Vector2(foot.x, foot.z);
            Vector2 nearest = Vector2.zero;
            float best = float.PositiveInfinity;
            Vector2 horizontal = Vector2.zero, vertical = Vector2.zero;
            foreach (var road in roads)
            {
                float half = road.size * .5f;
                if (half <= 0f) continue;
                Vector2 delta = new Vector2(
                    Mathf.Clamp(position.x, road.center.x - half, road.center.x + half),
                    Mathf.Clamp(position.y, road.center.z - half, road.center.z + half)) - position;
                float distance = delta.magnitude;
                if (distance < best) { best = distance; nearest = delta; }
                if (delta.y == 0 && (horizontal == Vector2.zero || distance < horizontal.magnitude))
                    horizontal = delta;
                if (delta.x == 0 && (vertical == Vector2.zero || distance < vertical.magnitude))
                    vertical = delta;
            }
            // Adjacent perpendicular road edges describe the same intersection corner.
            if (best > 0 && horizontal != Vector2.zero && vertical != Vector2.zero &&
                Mathf.Max(horizontal.magnitude, vertical.magnitude) <= best + width)
                nearest = horizontal.normalized + vertical.normalized;
            Vector2 offset = nearest.normalized * width;
            return new Vector3(offset.x, 0, offset.y);
        }

        static Matrix4x4 BattlePropMatrix(JcDynamicBattleSource.Part part, Matrix4x4 fieldToBattle,
            JcDynamicBattleSource.Road[] roads, Vector3[] vertices)
        {
            var placement = part.matrix;
            if (part.name == "Streetlight")
            {
                Vector3 foot = placement.MultiplyPoint3x4(Vector3.zero);
                foot.y = part.bounds.min.y;
                float baseTop = foot.y + part.bounds.size.y * .02f;
                // The square footplate is centred on the pole. Its corner radius gives
                // its side length irrespective of the lamp's orientation.
                float radiusSquared = 0f;
                foreach (var vertex in vertices)
                {
                    Vector3 point = placement.MultiplyPoint3x4(vertex);
                    if (point.y > baseTop) continue;
                    float x = point.x - foot.x, z = point.z - foot.z;
                    radiusSquared = Mathf.Max(radiusSquared, x * x + z * z);
                }
                const float previousScale = .6f;
                const float lampScale = previousScale * .7f;
                float previousWidth = Mathf.Sqrt(2f * radiusSquared) * previousScale;
                Vector3 offset = RoadwardOffset(foot, previousWidth, roads);
                placement = Matrix4x4.TRS(foot * (1f - lampScale) + offset, Quaternion.identity,
                    Vector3.one * lampScale) * placement;
            }
            return fieldToBattle * placement;
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

        // JC | 2026-11-05 | 산의 Bounds 안 빈 공간을 가림으로 오인하지 않도록 실제 표면을 검사합니다.
        // 한 번의 배치 탐색 안에서만 월드 정점/인덱스를 재사용하며 Collider나 씬 객체를 만들지 않습니다.
        sealed class SightMesh
        {
            public readonly Vector3[] vertices;
            public readonly int[] triangles;
            public SightMesh(JcDynamicBattleSource.Part part)
            {
                vertices = part.mesh.vertices;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = part.matrix.MultiplyPoint3x4(vertices[i]);
                triangles = part.mesh.triangles;
            }
        }

        static float SegmentDistanceSquared(Vector3 p, Vector3 q, Vector3 a, Vector3 b)
        {
            var u = q - p; var v = b - a; var w = p - a;
            float uu = Vector3.Dot(u, u), vv = Vector3.Dot(v, v), vw = Vector3.Dot(v, w);
            float s, t;
            if (uu <= 1e-12f && vv <= 1e-12f) return w.sqrMagnitude;
            if (uu <= 1e-12f) { s = 0; t = Mathf.Clamp01(vw / vv); }
            else
            {
                float uw = Vector3.Dot(u, w);
                if (vv <= 1e-12f) { t = 0; s = Mathf.Clamp01(-uw / uu); }
                else
                {
                    float uv = Vector3.Dot(u, v), denominator = uu * vv - uv * uv;
                    s = denominator > 1e-12f ? Mathf.Clamp01((uv * vw - uw * vv) / denominator) : 0;
                    t = (uv * s + vw) / vv;
                    if (t < 0) { t = 0; s = Mathf.Clamp01(-uw / uu); }
                    else if (t > 1) { t = 1; s = Mathf.Clamp01((uv - uw) / uu); }
                }
            }
            return (w + s * u - t * v).sqrMagnitude;
        }

        static float PointTriangleDistanceSquared(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            var ab = b - a; var ac = c - a; var normal = Vector3.Cross(ab, ac);
            float normalSquared = normal.sqrMagnitude;
            // JC | 2026-11-05 | 면적이 없는 삼각형은 변으로 검사해 0으로 나누는 계산을 방지합니다.
            if (normalSquared > 1e-12f)
            {
                float height = Vector3.Dot(p - a, normal);
                var projected = p - normal * (height / normalSquared);
                if (Vector3.Dot(Vector3.Cross(b - a, projected - a), normal) >= 0 &&
                    Vector3.Dot(Vector3.Cross(c - b, projected - b), normal) >= 0 &&
                    Vector3.Dot(Vector3.Cross(a - c, projected - c), normal) >= 0)
                    return height * height / normalSquared;
            }
            return Mathf.Min(SegmentDistanceSquared(p, p, a, b),
                SegmentDistanceSquared(p, p, b, c), SegmentDistanceSquared(p, p, c, a));
        }

        static bool TriangleBlocksSight(Vector3 eye, Vector3 target, float radius,
            Vector3 a, Vector3 b, Vector3 c)
        {
            var direction = target - eye;
            var normal = Vector3.Cross(b - a, c - a);
            float denominator = Vector3.Dot(normal, direction);
            if (Mathf.Abs(denominator) > 1e-12f)
            {
                float t = Vector3.Dot(normal, a - eye) / denominator;
                if (t >= 0 && t <= 1 && PointTriangleDistanceSquared(eye + t * direction, a, b, c) <= 1e-10f)
                    return true;
            }
            // JC | 2026-11-05 | 양면 표면과 시선 구간의 최소 거리로 기존 전투 기준 0.5 여유 폭도 유지합니다.
            // 카메라 뒤/캐릭터 너머의 무한 직선은 검사하지 않으며 경사면의 빈 Bounds는 차단하지 않습니다.
            float squared = radius * radius;
            return PointTriangleDistanceSquared(eye, a, b, c) <= squared ||
                   PointTriangleDistanceSquared(target, a, b, c) <= squared ||
                   SegmentDistanceSquared(eye, target, a, b) <= squared ||
                   SegmentDistanceSquared(eye, target, b, c) <= squared ||
                   SegmentDistanceSquared(eye, target, c, a) <= squared;
        }

        static bool BlocksSight(JcDynamicBattleSource.Part part, Ray ray, float length, float radius,
            Dictionary<JcDynamicBattleSource.Part, SightMesh> cache)
        {
            if (part.terrain) return false;
            var bounds = part.bounds;
            bounds.Expand(radius * 2);
            if (!bounds.Contains(ray.origin) && !(bounds.IntersectRay(ray, out float hit) && hit <= length)) return false;
            if (!cache.TryGetValue(part, out var geometry)) cache.Add(part, geometry = new SightMesh(part));
            var target = ray.GetPoint(length);
            for (int i = 0; i < geometry.triangles.Length; i += 3)
            {
                var a = geometry.vertices[geometry.triangles[i]];
                var b = geometry.vertices[geometry.triangles[i + 1]];
                var c = geometry.vertices[geometry.triangles[i + 2]];
                var triangleBounds = new Bounds(a, Vector3.zero);
                triangleBounds.Encapsulate(b); triangleBounds.Encapsulate(c); triangleBounds.Expand(radius * 2);
                if (!triangleBounds.Contains(ray.origin) &&
                    !(triangleBounds.IntersectRay(ray, out float triangleHit) && triangleHit <= length)) continue;
                if (TriangleBlocksSight(ray.origin, target, radius, a, b, c)) return true;
            }
            return false;
        }

        // 배경을 변형하지 않고, 도로 안에 진형 사각형이 들어가는 가까운 위치를 찾습니다.
        public static Placement Resolve(JcDynamicBattleSource source,JcDynamicBattleSettings settings,Vector3[] cameras=null)
        {
            Validate(source,settings);
            // JC | 2026-11-05 | Bounds는 빠른 1차 선별만 담당하고 실제 가림 여부는 정밀 메시 검사로 결정합니다.
            var sightMeshes = new Dictionary<JcDynamicBattleSource.Part, SightMesh>();
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
                            if(source.parts.Any(part => BlocksSight(part, ray, length, .5f / scale, sightMeshes)))fits=false;
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
                // Leave room for attack-camera pullbacks beyond the placement cameras.
                float extent = Mathf.Max(144f, settings.captureRadius * scale, settings.battleLength * .5f + 6);
                float radius = extent / scale;
                bool InView(JcDynamicBattleSource.Part part)
                {
                    var bounds = part.bounds;
                    return bounds.min.x <= p.x + radius && bounds.max.x >= p.x - radius &&
                           bounds.min.z <= p.z + radius && bounds.max.z >= p.z - radius;
                }
                var groups = new HashSet<string>(source.parts.Where(part => !part.terrain && InView(part))
                    .Select(part => part.group).Where(group => !string.IsNullOrEmpty(group)));
                var selected = source.parts.Where(part => part.mesh &&
                    (InView(part) || (!part.terrain && groups.Contains(part.group)))).ToArray();
                JC.Environment.JcBattleLampGroup lamps = null;
                Matrix4x4 meshToBulb = Matrix4x4.identity;
                foreach(var part in selected)
                {
                    Mesh mesh;
                    if(part.terrain)mesh=Terrain(part,transform*part.matrix,extent);
                    else {mesh=UnityEngine.Object.Instantiate(part.mesh);var v=mesh.vertices;var matrix=BattlePropMatrix(part,transform,source.roads,v);
                        if (part.name == "Streetlight")
                        {
                            if (!lamps)
                            {
                                var profile = Resources.Load<JC.Environment.JcBattleLampProfile>(JC.Environment.JcBattleLampProfile.ResourceName);
                                var prefab = profile.lampPrefab;
                                var original = prefab.GetComponentInChildren<MeshFilter>(true);
                                var bulb = prefab.GetComponentInChildren<Light>(true);
                                meshToBulb = original.transform.worldToLocalMatrix * bulb.transform.localToWorldMatrix;
                                lamps = root.AddComponent<JC.Environment.JcBattleLampGroup>();
                            }
                            lamps.Add(matrix * meshToBulb);
                        }
                        for(int i=0;i<v.Length;i++)v[i]=matrix.MultiplyPoint3x4(v[i]);mesh.vertices=v;var normals=mesh.normals;var normalMatrix=matrix.inverse.transpose;for(int i=0;i<normals.Length;i++)normals[i]=normalMatrix.MultiplyVector(normals[i]).normalized;mesh.normals=normals;mesh.RecalculateBounds();mesh.RecalculateTangents();}
                    if(mesh==null)continue;
                    var renderer = output.Add(part.name,mesh,part.materials,part.terrain,part.renderer);
                    if (part.name == "Streetlight" && lamps) lamps.BindLens(renderer);
                }
                output.Report=$"조우 {settings.encounterPosition} → 기준점 {p} / 이동 {Vector3.Distance(new Vector3(settings.encounterPosition.x,p.y,settings.encounterPosition.z),p):F2} / 방향 {(placement.alongX?"X":"Z")} / 동일 확대 {scale:F2} / 원형 보존 외형 {selected.Length}";
                // 캐시 입력으로 생성하는 전투도 탐사와 같은 일방통행 차선 제외를 적용합니다.
                if (source.sourceScene != null && source.sourceScene.EndsWith("/DHScene_3.unity", StringComparison.Ordinal))
                    JC.EnvironmentDetail.JcOneWayRoadSurface.Apply(root, transform.inverse);
                // 탐사 드레싱 적용 전의 입력도 튜토리얼 점선과 원본 도색이 겹치지 않게 한다.
                if (source.sourceScene != null && source.sourceScene.EndsWith("/TutorialExploreScene.unity", StringComparison.Ordinal) &&
                    selected.Any(part => part.name == "CenterLane_ThinDense_VisualOnly"))
                    JC.EnvironmentDetail.JcTutorialRoadSurface.Apply(root);
                if (settings.addBattleSurfaceDetails) JcBattleSurfaceDressing.Apply(root,Mathf.RoundToInt(settings.encounterPosition.x*1000)^Mathf.RoundToInt(settings.encounterPosition.z*7919),cameras);
                else if (source.sourceScene != null && source.sourceScene.EndsWith("/TutorialExploreScene.unity", StringComparison.Ordinal))
                    JcBattleSurfaceDressing.ApplyAuthoredLineSpacing(root);
                // Add after dressing so the distant ground never receives roads or scatter props.
                // JC | 2026-11-13 | 본게임·테스트 배경 생성에 동일한 튜토리얼 도로 접합 처리를 적용합니다.
                output.AddDistantGround(source, transform);
                if (source.sourceScene != null && source.sourceScene.EndsWith("/TutorialExploreScene.unity", StringComparison.Ordinal)) RepairTutorialRoads(root);
                Debug.Log(output.Report,root);
                return root;
            }
            catch{if(Application.isPlaying)UnityEngine.Object.Destroy(root);else UnityEngine.Object.DestroyImmediate(root);throw;}
        }
    }
}
