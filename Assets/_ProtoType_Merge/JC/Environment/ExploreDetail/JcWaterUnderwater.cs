using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace JC.EnvironmentDetail
{
    // 확정 수면의 삼각형만 내려 단순한 수중 공간을 만듭니다. 원본 메시·콜라이더는 건드리지 않습니다.
    public sealed class JcWaterUnderwater : IDisposable
    {
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly Dictionary<Vector2Int,float> wet = new Dictionary<Vector2Int,float>();
        readonly Dictionary<Vector2Int,List<int>> triangleGrid=new Dictionary<Vector2Int,List<int>>();
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<int> triangles = new List<int>();
        GameObject root, capsule;
        Material bedMaterial;
        Vector3 routeA, routeB;
        float depth = -1;
        bool hasRoute;
        const float Grid = .5f;
        public int WetCount => wet.Count;
        public bool HasRoute => hasRoute;
        public Vector3 RouteA => routeA;
        public Vector3 RouteB => routeB;
        public Transform Capsule => capsule != null ? capsule.transform : null;
        public JcWaterUnderwater(Transform parent,IEnumerable<Renderer> surfaces)
        {
            root=new GameObject("수중 공간 (자동 생성)"){hideFlags=HideFlags.DontSave};
            root.transform.SetParent(parent,false);root.transform.position=Vector3.zero;root.transform.rotation=Quaternion.identity;root.transform.localScale=Vector3.one;
            foreach(var r in surfaces)
            {
                var f=r.GetComponent<MeshFilter>();var mesh=f!=null?f.sharedMesh:null;
                if(mesh==null||!mesh.isReadable)continue;
                var vs=mesh.vertices;var mats=r.sharedMaterials;
                for(int sub=0;sub<mesh.subMeshCount&&sub<mats.Length;sub++)
                {
                    if(mats[sub]==null||mats[sub].shader.name!="JC/Environment/Calm Canal Water")continue;
                    var indices=mesh.GetTriangles(sub);
                    for(int k=0;k<indices.Length;k+=3)
                    {
                        var a=r.transform.TransformPoint(vs[indices[k]]);var b=r.transform.TransformPoint(vs[indices[k+1]]);var c=r.transform.TransformPoint(vs[indices[k+2]]);
                        int start=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);
                        Raster(a,b,c);
                    }
                }
            }
            var template=Resources.Load<Material>("JC_Water_Submerged");
            bedMaterial=template!=null?new Material(template):new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            bedMaterial.name="JC water basin";bedMaterial.hideFlags=HideFlags.HideAndDontSave;owned.Add(bedMaterial);
            FindRoute();
            if(hasRoute)
            {
                capsule=GameObject.CreatePrimitive(PrimitiveType.Capsule);capsule.name="수중 캡슐 · 실루엣 시제품";capsule.hideFlags=HideFlags.DontSave;
                capsule.transform.SetParent(root.transform,false);
                var collider=capsule.GetComponent<Collider>();collider.enabled=false;Release(collider);
                capsule.transform.localScale=new Vector3(.42f,1.1f,.42f);
                var mat=new Material(bedMaterial){name="JC submerged silhouette",hideFlags=HideFlags.HideAndDontSave};mat.SetColor("_BaseColor",new Color(.025f,.065f,.075f,1));owned.Add(mat);
                var r=capsule.GetComponent<Renderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            }
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        void Raster(Vector3 a,Vector3 b,Vector3 c)
        {
            var aa=new Vector2(a.x,a.z);var bb=new Vector2(b.x,b.z);var cc=new Vector2(c.x,c.z);
            if(Mathf.Abs(Cross(bb-aa,cc-aa))<.00001f)return;
            int x0=Mathf.CeilToInt(Mathf.Min(a.x,Mathf.Min(b.x,c.x))/Grid),x1=Mathf.FloorToInt(Mathf.Max(a.x,Mathf.Max(b.x,c.x))/Grid);
            int z0=Mathf.CeilToInt(Mathf.Min(a.z,Mathf.Min(b.z,c.z))/Grid),z1=Mathf.FloorToInt(Mathf.Max(a.z,Mathf.Max(b.z,c.z))/Grid);
            for(int z=z0-1;z<=z1;z++)for(int x=x0-1;x<=x1;x++)
            {
                var key=new Vector2Int(x,z);if(!triangleGrid.TryGetValue(key,out var list)){list=new List<int>();triangleGrid.Add(key,list);}list.Add(vertices.Count-3);
            }
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)
            {
                var p=new Vector2(x*Grid,z*Grid);float ab=Cross(bb-aa,p-aa),bc=Cross(cc-bb,p-bb),ca=Cross(aa-cc,p-cc);
                if((ab>=-.0001f&&bc>=-.0001f&&ca>=-.0001f)||(ab<=.0001f&&bc<=.0001f&&ca<=.0001f))wet[new Vector2Int(x,z)]=(a.y+b.y+c.y)/3;
            }
        }
        public bool Contains(Vector3 p)
        {
            if(!triangleGrid.TryGetValue(new Vector2Int(Mathf.FloorToInt(p.x/Grid),Mathf.FloorToInt(p.z/Grid)),out var list))return false;
            var q=new Vector2(p.x,p.z);
            foreach(int i in list)
            {
                var a=new Vector2(vertices[i].x,vertices[i].z);var b=new Vector2(vertices[i+1].x,vertices[i+1].z);var c=new Vector2(vertices[i+2].x,vertices[i+2].z);
                float ab=Cross(b-a,q-a),bc=Cross(c-b,q-b),ca=Cross(a-c,q-c);
                if((ab>=-.00001f&&bc>=-.00001f&&ca>=-.00001f)||(ab<=.00001f&&bc<=.00001f&&ca<=.00001f))return true;
            }
            return false;
        }
        bool Clear(Vector2Int p)
        {
            if(!wet.TryGetValue(p,out float h))return false;
            for(int y=-3;y<=3;y++)for(int x=-3;x<=3;x++)if(!wet.TryGetValue(p+new Vector2Int(x,y),out float other)||Mathf.Abs(h-other)>.03f)return false;
            return true;
        }
        void FindRoute()
        {
            Vector2 mean=Vector2.zero;foreach(var p in wet.Keys)mean+=(Vector2)p;mean/=Mathf.Max(1,wet.Count);float best=0;
            foreach(var p in wet.Keys)if(Clear(p))foreach(var axis in new[]{Vector2Int.right,Vector2Int.up})
            {
                if(Clear(p-axis))continue;
                var end=p;while(Clear(end+axis)&&Mathf.Abs(wet[end+axis]-wet[p])<.03f)end+=axis;
                float length=Vector2.Distance(p,end)*Grid;if(length<6)continue;
                float score=length/(1+Vector2.Distance(((Vector2)p+end)*.5f,mean)*Grid*.12f);
                if(score<=best)continue;best=score;hasRoute=true;
                routeA=new Vector3(p.x*Grid,wet[p],p.y*Grid);routeB=new Vector3(end.x*Grid,wet[end],end.y*Grid);
            }
        }
        struct Edge {public Vector3 a,b;public int count;}
        static string Key(Vector3 v)=>Mathf.RoundToInt(v.x*1000)+","+Mathf.RoundToInt(v.y*1000)+","+Mathf.RoundToInt(v.z*1000);
        readonly List<Mesh> basinMeshes=new List<Mesh>();
        GameObject basin;
        void BuildBasin(float newDepth)
        {
            if(basin!=null){basin.SetActive(false);Release(basin);}foreach(var m in basinMeshes)Release(m);basinMeshes.Clear();
            depth=newDepth;var v=new List<Vector3>(vertices.Count);foreach(var p in vertices)v.Add(p-Vector3.up*depth);
            var t=new List<int>(triangles);var edges=new Dictionary<string,Edge>();
            for(int i=0;i<triangles.Count;i+=3)for(int j=0;j<3;j++)
            {
                var a=vertices[triangles[i+j]];var b=vertices[triangles[i+(j+1)%3]];string ka=Key(a),kb=Key(b),key=string.CompareOrdinal(ka,kb)<0?ka+"/"+kb:kb+"/"+ka;
                if(edges.TryGetValue(key,out var edge)){edge.count++;edges[key]=edge;}else edges.Add(key,new Edge{a=a,b=b,count=1});
            }
            foreach(var e in edges.Values)if(e.count==1)
            {
                var mid=(e.a+e.b)*.5f;var across=Vector3.Cross(e.b-e.a,Vector3.up).normalized*.015f;
                // 청크 경계의 서로 다른 분할 길이도 점 포함 판정으로 내부 벽에서 제외합니다.
                if(Contains(mid+across)&&Contains(mid-across))continue;
                int i=v.Count;v.Add(e.a);v.Add(e.b);v.Add(e.a-Vector3.up*depth);v.Add(e.b-Vector3.up*depth);
                t.AddRange(new[]{i,i+2,i+1,i+1,i+2,i+3,i+1,i+2,i,i+3,i+2,i+1});
            }
            var mesh=new Mesh{name="JC submerged basin",hideFlags=HideFlags.HideAndDontSave,indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateBounds();basinMeshes.Add(mesh);
            basin=new GameObject("단순 바닥·측면"){hideFlags=HideFlags.DontSave};basin.transform.SetParent(root.transform,false);basin.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=basin.AddComponent<MeshRenderer>();r.sharedMaterial=bedMaterial;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }
        public void Apply(JcWaterSettings settings)
        {
            float d=Mathf.Clamp(settings.basinDepth,.8f,5);if(!Mathf.Approximately(depth,d))BuildBasin(d);
            bedMaterial.SetColor("_BaseColor",settings.basinColor);if(capsule!=null)capsule.SetActive(settings.underwaterCapsule);
        }
        public void Tick(JcWaterSettings settings,float time)
        {
            float daylight=JC.Environment.JcDayNightController.WaterBrightness;
            if(bedMaterial!=null)bedMaterial.SetColor("_BaseColor",settings.basinColor*new Color(daylight,daylight,daylight,1));
            if(capsule==null)return;
            capsule.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor",new Color(.025f*daylight,.065f*daylight,.075f*daylight,1));
            float length=Vector3.Distance(routeA,routeB),phase=.55f+time*Mathf.Max(0,settings.capsuleSpeed)*2/Mathf.Max(.01f,length);
            float u=.5f+.5f*Mathf.Sin(phase);
            capsule.transform.position=Vector3.Lerp(routeA,routeB,u)-Vector3.up*Mathf.Clamp(settings.capsuleDepth,.25f,.65f);
            // 카툰 실루엣 시제품은 끝에서 감속·반전합니다. 캡슐은 앞뒤 대칭이므로 회전 점프가 없습니다.
            capsule.transform.rotation=Quaternion.FromToRotation(Vector3.up,(routeB-routeA).normalized);
        }
        static void Release(UnityEngine.Object o){if(o==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);}
        public void Dispose(){if(root!=null){root.SetActive(false);Release(root);}foreach(var m in basinMeshes)Release(m);foreach(var o in owned)Release(o);owned.Clear();basinMeshes.Clear();}
    }
}
