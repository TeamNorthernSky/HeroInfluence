using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-17 | 원본을 덮어쓰지 않는 원경 전용 생성기입니다. 모든 UV/색상/서브메시 슬롯을 보존합니다.
    public static class JcBossCityLodBuilder
    {
        const string Folder="Assets/_ProtoType_Merge/JC/Environment/BossStage/CityLod";
        sealed class Data
        {
            public Vector3[] positions,normals;
            public Vector4[] tangents;
            public Color[] colors;
            public readonly List<Vector4>[] uv=new List<Vector4>[8];
            public Data(Mesh mesh) {
                positions=mesh.vertices;normals=mesh.normals;tangents=mesh.tangents;colors=mesh.colors;
                for(int i=0;i<8;i++) {uv[i]=new List<Vector4>();mesh.GetUVs(i,uv[i]);}
            }
            public Mesh Extract(Mesh source,List<int>[] slots,string name) {
                var ids=new List<int>();var map=new Dictionary<int,int>();
                var triangles=new int[slots.Length][];
                for(int s=0;s<slots.Length;s++) {
                    triangles[s]=new int[slots[s].Count];
                    for(int i=0;i<slots[s].Count;i++) {
                        int id=slots[s][i];if(!map.TryGetValue(id,out var index)){index=ids.Count;map.Add(id,index);ids.Add(id);}triangles[s][i]=index;
                    }
                }
                var result=new Mesh{name=name,indexFormat=IndexFormat.UInt32};
                result.SetVertices(ids.ConvertAll(i=>positions[i]));
                if(normals.Length==positions.Length)result.SetNormals(ids.ConvertAll(i=>normals[i]));
                if(tangents.Length==positions.Length)result.SetTangents(ids.ConvertAll(i=>tangents[i]));
                if(colors.Length==positions.Length)result.SetColors(ids.ConvertAll(i=>colors[i]));
                for(int c=0;c<8;c++)if(uv[c].Count==positions.Length)result.SetUVs(c,ids.ConvertAll(i=>uv[c][i]));
                result.subMeshCount=slots.Length;
                for(int s=0;s<slots.Length;s++)result.SetTriangles(triangles[s],s,false);
                result.RecalculateBounds();return result;
            }
        }
        static long Triangles(Mesh mesh) {long count=0;for(int i=0;i<mesh.subMeshCount;i++)count+=mesh.GetIndexCount(i)/3;return count;}
        static Mesh Save(Mesh mesh,string name) {
            string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old)throw new InvalidOperationException("생성 대상이 이미 있습니다: "+path);
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static List<Mesh> Split(Mesh mesh,string id)
        {
            // JC | 2026-11-17 | 넓은 지표/수로는 12m 원본 구역 단위로 나눠 화면 밖 영역을 개별 제외합니다.
            var size=mesh.bounds.size;
            if(Mathf.Max(size.x,size.z)<24 || Triangles(mesh)<4000)return new List<Mesh>{mesh};
            var data=new Data(mesh);var groups=new Dictionary<Vector2Int,List<int>[]>();
            for(int s=0;s<mesh.subMeshCount;s++) {
                var indices=mesh.GetTriangles(s);
                for(int i=0;i<indices.Length;i+=3) {
                    var c=(data.positions[indices[i]]+data.positions[indices[i+1]]+data.positions[indices[i+2]])/3;
                    var key=new Vector2Int(Mathf.FloorToInt(c.x/12),Mathf.FloorToInt(c.z/12));
                    if(!groups.TryGetValue(key,out var slots)){slots=Enumerable.Range(0,mesh.subMeshCount).Select(_=>new List<int>()).ToArray();groups.Add(key,slots);}
                    slots[s].Add(indices[i]);slots[s].Add(indices[i+1]);slots[s].Add(indices[i+2]);
                }
            }
            if(groups.Count<2)return new List<Mesh>{mesh};
            var result=new List<Mesh>();int part=0;
            foreach(var group in groups.OrderBy(g=>g.Key.x).ThenBy(g=>g.Key.y))result.Add(Save(data.Extract(mesh,group.Value,"JC 구역 "+id+" "+part),id+"_p"+(part++)+"_high"));
            return result;
        }
        sealed class Sum
        {
            public int count;public Vector3 position,normal;public Vector4 tangent;public Color color;
            public readonly Vector4[] uv=new Vector4[8];
        }
        static Mesh Simplify(Mesh mesh,float ratio,string name)
        {
            // JC | 2026-11-17 | 위치 격자 축약은 법선·색상·재질 경계를 분리하고 UV는 해당 군집 안에서만 평균합니다.
            // 구역 경계 정점은 고정하여 인접 수로/바닥 청크가 다른 LOD일 때 틈이 생기지 않게 합니다.
            var data=new Data(mesh);float step=Mathf.Max(mesh.bounds.size.x,Mathf.Max(mesh.bounds.size.y,mesh.bounds.size.z))*ratio;
            if(step<=1e-7f)return UnityEngine.Object.Instantiate(mesh);
            // JC | 2026-11-17 | 위치가 같은 UV/법선 분리 정점을 용접한 토폴로지로 열린 경계를 판별합니다.
            var welded=new Dictionary<Vector3,int>();var pointIds=new int[data.positions.Length];
            for(int i=0;i<pointIds.Length;i++){if(!welded.TryGetValue(data.positions[i],out int id)){id=welded.Count;welded.Add(data.positions[i],id);}pointIds[i]=id;}
            var edges=new Dictionary<(int,int),int>();
            for(int sub=0;sub<mesh.subMeshCount;sub++){var idx=mesh.GetTriangles(sub);for(int i=0;i<idx.Length;i+=3)for(int e=0;e<3;e++){
                int a=pointIds[idx[i+e]],b=pointIds[idx[i+(e+1)%3]];if(a==b)continue;var key=a<b?(a,b):(b,a);edges.TryGetValue(key,out int count);edges[key]=count+1;
            }}
            var boundary=new HashSet<int>();foreach(var edge in edges)if(edge.Value==1){boundary.Add(edge.Key.Item1);boundary.Add(edge.Key.Item2);}
            // JC | 2026-11-17 | 창문 아틀라스의 UV 이음선은 축약에서 고정하여 먼 LOD의 무늬가 비틀리지 않게 합니다.
            var seamUv=new Dictionary<int,Vector4>();
            if(data.uv[0].Count==pointIds.Length)for(int i=0;i<pointIds.Length;i++){
                if(seamUv.TryGetValue(pointIds[i],out var prior)){if((prior-data.uv[0][i]).sqrMagnitude>1e-8f)boundary.Add(pointIds[i]);}
                else seamUv.Add(pointIds[i],data.uv[0][i]);
            }
            var positions=new Dictionary<Vector3Int,Sum>();var cells=new Vector3Int[data.positions.Length];
            for(int i=0;i<cells.Length;i++) {
                var p=data.positions[i];var cell=boundary.Contains(pointIds[i])?new Vector3Int(int.MinValue,pointIds[i],0):Vector3Int.RoundToInt(p/step);cells[i]=cell;
                if(!positions.TryGetValue(cell,out var sum))positions.Add(cell,sum=new Sum());sum.position+=p;sum.count++;
            }
            var groups=new Dictionary<(Vector3Int,Vector3Int,Color32,Vector2Int,int),int>();
            var sums=new List<Sum>();var tris=new List<int>[mesh.subMeshCount];
            bool normals=data.normals.Length==cells.Length,colors=data.colors.Length==cells.Length,tangents=data.tangents.Length==cells.Length;
            for(int sub=0;sub<mesh.subMeshCount;sub++) {
                tris[sub]=new List<int>();var indices=mesh.GetTriangles(sub);var remap=new Dictionary<int,int>();
                foreach(int i in indices)if(!remap.ContainsKey(i)) {
                    var normal=normals?data.normals[i]:Vector3.up;var color=colors?(Color32)data.colors[i]:new Color32(255,255,255,255);
                    var uv0=data.uv[0].Count==cells.Length?data.uv[0][i]:Vector4.zero;
                    var key=(cells[i],Vector3Int.RoundToInt(normal*32),color,new Vector2Int(Mathf.FloorToInt(uv0.x),Mathf.FloorToInt(uv0.y)),sub);
                    if(!groups.TryGetValue(key,out int index)){index=sums.Count;groups.Add(key,index);sums.Add(new Sum());}
                    remap.Add(i,index);var sum=sums[index];sum.count++;sum.normal+=normal;sum.color+=colors?data.colors[i]:Color.white;
                    if(tangents)sum.tangent+=data.tangents[i];
                    for(int c=0;c<8;c++)if(data.uv[c].Count==cells.Length)sum.uv[c]+=data.uv[c][i];
                    sum.position=positions[cells[i]].position/positions[cells[i]].count;
                }
                for(int i=0;i<indices.Length;i+=3) {
                    int a=remap[indices[i]],b=remap[indices[i+1]],c=remap[indices[i+2]];
                    if(a==b||b==c||a==c||Vector3.Cross(sums[b].position-sums[a].position,sums[c].position-sums[a].position).sqrMagnitude<step*step*step*step*1e-8f)continue;
                    tris[sub].Add(a);tris[sub].Add(b);tris[sub].Add(c);
                }
            }
            var result=new Mesh{name=name,indexFormat=IndexFormat.UInt32};result.SetVertices(sums.ConvertAll(s=>s.position));
            if(normals)result.SetNormals(sums.ConvertAll(s=>s.normal.normalized));
            if(colors)result.SetColors(sums.ConvertAll(s=>s.color/s.count));
            if(tangents)result.SetTangents(sums.ConvertAll(s=>{var t=s.tangent/s.count;var n=new Vector3(t.x,t.y,t.z).normalized;return new Vector4(n.x,n.y,n.z,t.w>=0?1:-1);}));
            for(int c=0;c<8;c++)if(data.uv[c].Count==cells.Length){int channel=c;result.SetUVs(c,sums.ConvertAll(s=>s.uv[channel]/s.count));}
            result.subMeshCount=mesh.subMeshCount;
            for(int s=0;s<tris.Length;s++)result.SetTriangles(tris[s],s,false);
            result.RecalculateBounds();
            if(Triangles(result)==0){UnityEngine.Object.DestroyImmediate(result);return UnityEngine.Object.Instantiate(mesh);}
            return result;
        }
        // JC | 2026-11-17 | 검수 후 축약 알고리즘을 갱신해도 에셋 GUID와 라이브러리 연결은 유지합니다.
        public static string RebuildSimplified()
        {
            var library=JcBossBattleBackground.Load().cityLodLibrary;long high=0,medium=0,low=0;
            foreach(var entry in library.entries)foreach(var part in entry.parts){
                var targets=new[]{part.medium,part.low};var ratios=new[]{.0125f,.03f};
                for(int level=0;level<2;level++){
                    var fresh=Simplify(part.high,ratios[level],targets[level].name);var target=targets[level];
                    target.Clear();target.indexFormat=fresh.indexFormat;target.vertices=fresh.vertices;
                    target.normals=fresh.normals;target.tangents=fresh.tangents;target.colors=fresh.colors;
                    for(int c=0;c<8;c++){var uv=new List<Vector4>();fresh.GetUVs(c,uv);if(uv.Count>0)target.SetUVs(c,uv);}
                    target.subMeshCount=fresh.subMeshCount;for(int sub=0;sub<fresh.subMeshCount;sub++)target.SetTriangles(fresh.GetTriangles(sub),sub,false);
                    target.bounds=fresh.bounds;UnityEngine.Object.DestroyImmediate(fresh);EditorUtility.SetDirty(target);AssetDatabase.SaveAssetIfDirty(target);
                }
                high+=Triangles(part.high);medium+=Triangles(part.medium);low+=Triangles(part.low);
            }
            return $"high={high} medium={medium} low={low}";
        }
        public static string Build(Transform stage)
        {
            if(Application.isPlaying)throw new InvalidOperationException("에디트 모드에서 생성하세요.");
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_ProtoType_Merge/JC/Environment/BossStage","CityLod");
            if(AssetDatabase.LoadAssetAtPath<JcBossCityLodLibrary>(Folder+"/Library.asset"))throw new InvalidOperationException("기존 LOD 라이브러리를 먼저 검토하세요.");
            var sources=JcBossCityTime.Roots(stage).SelectMany(c=>c.GetComponentsInChildren<MeshFilter>(true)).Select(f=>f.sharedMesh).Where(m=>m&&m.isReadable&&m.vertexCount>=300).Distinct().ToArray();
            var entries=new List<JcBossCityLodLibrary.Entry>();long high=0,medium=0,low=0;int partCount=0;
            foreach(var mesh in sources) {
                if(Enumerable.Range(0,mesh.subMeshCount).Any(s=>mesh.GetTopology(s)!=MeshTopology.Triangles))continue;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh,out string guid,out long fileId);string id=guid+"_"+fileId;
                var parts=new List<JcBossCityLodLibrary.Part>();int n=0;
                foreach(var part in Split(mesh,id)) {
                    var mid=Save(Simplify(part,.0125f,"JC 원경 LOD1 "+mesh.name),id+"_p"+n+"_medium");
                    var far=Save(Simplify(part,.03f,"JC 원경 LOD2 "+mesh.name),id+"_p"+n+"_low");n++;
                    high+=Triangles(part);medium+=Triangles(mid);low+=Triangles(far);partCount++;
                    parts.Add(new JcBossCityLodLibrary.Part{high=part,medium=mid,low=far});
                }
                entries.Add(new JcBossCityLodLibrary.Entry{source=mesh,parts=parts.ToArray()});
            }
            var library=ScriptableObject.CreateInstance<JcBossCityLodLibrary>();library.entries=entries.ToArray();AssetDatabase.CreateAsset(library,Folder+"/Library.asset");
            var settings=JcBossBattleBackground.Load();settings.cityLodLibrary=library;EditorUtility.SetDirty(settings);AssetDatabase.SaveAssetIfDirty(settings);
            return $"sources={sources.Length} parts={partCount} triangles high={high} medium={medium} low={low}";
        }
    }
}
