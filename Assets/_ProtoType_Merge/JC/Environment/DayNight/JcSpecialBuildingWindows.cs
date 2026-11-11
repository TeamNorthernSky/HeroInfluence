using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace JC.Environment
{
 // Explicit model face selections: palette colours are also used by roofs and signs.
 public sealed class JcSpecialBuildingWindows : IDisposable
 {
  [Serializable] sealed class Catalog { public Recipe[] entries; }
  [Serializable] sealed class Recipe { public string meshName; public int vertexCount,indexCount; public int[] triangles; public Pane[] panes; }
  [Serializable] sealed class Pane { public Vector3 center,normal; public float width,height; }
  sealed class Bound { public MeshFilter filter; public Mesh original,shown; }
  readonly List<Bound> bound=new List<Bound>();
  // JC | 2026-11-10 | 복원 순서는 목록으로 유지하고 이미 연결된 필터만 사전으로 조회합니다. 스캔 시점·대상은 바꾸지 않습니다.
  readonly Dictionary<MeshFilter,Bound> boundByFilter=new Dictionary<MeshFilter,Bound>();
  // JC | 2026-11-10 | 전체 발견과 기존 대상 재확인을 분리합니다. 가림 재질 때문에 아직 연결하지 못한 창문 후보도 재확인합니다.
  readonly List<MeshFilter> candidates=new List<MeshFilter>();
  readonly HashSet<MeshFilter> candidateSet=new HashSet<MeshFilter>();
  static readonly Unity.Profiling.ProfilerMarker scanMarker=new Unity.Profiling.ProfilerMarker("JC DayNight.WindowDiscovery");
  static readonly Unity.Profiling.ProfilerMarker refreshMarker=new Unity.Profiling.ProfilerMarker("JC DayNight.WindowRefresh");
  readonly Dictionary<Mesh,Mesh> copies=new Dictionary<Mesh,Mesh>();
  readonly JcNightMaterialBindings materials=new JcNightMaterialBindings();
  static Catalog catalog;
  public void Scan(Scene scene){
   // JC | 2026-11-10 | 최초/레벨 전체 변경 또는 명시 요청에서만 씬 계층을 수집합니다. 평상시 신규 대상은 RefreshRoot로 추가합니다.
   using var sample=scanMarker.Auto();
   if(!EnsureCatalog())return;
   candidates.Clear();candidateSet.Clear();
   using var rootLease=UnityEngine.Pool.ListPool<GameObject>.Get(out var roots);
   using var filterLease=UnityEngine.Pool.ListPool<MeshFilter>.Get(out var filters);
   scene.GetRootGameObjects(roots);
   foreach(var root in roots){root.GetComponentsInChildren(true,filters);foreach(var filter in filters)if(TryBind(filter))Remember(filter);}
  }
  // JC | 2026-11-10 | 상태 변경 알림은 해당 건물 하위만 갱신하여 같은 프레임에 원래 재질·창문 연결을 되살립니다.
  public void RefreshRoot(Component root){
   if(root==null||!EnsureCatalog())return;
   using var filterLease=UnityEngine.Pool.ListPool<MeshFilter>.Get(out var filters);
   root.GetComponentsInChildren(true,filters);
   foreach(var filter in filters)if(TryBind(filter))Remember(filter);
  }
  public void RefreshKnown(){
   // JC | 2026-11-10 | 등록 대상의 재질·메시를 계속 읽으므로 점령 복원·가림 해제처럼 같은 상태의 재질 재적용도 반영합니다.
   using var sample=refreshMarker.Auto();
   for(int i=0;i<candidates.Count;i++){var filter=candidates[i];if(filter==null){candidateSet.Remove(filter);candidates.RemoveAt(i--);continue;}TryBind(filter);}
  }
  void Remember(MeshFilter filter){if(candidateSet.Add(filter))candidates.Add(filter);}
  static bool EnsureCatalog(){if(catalog==null){var data=Resources.Load<TextAsset>("JcSpecialBuildingWindows");if(data==null)return false;catalog=JsonUtility.FromJson<Catalog>(data.text);}return catalog!=null;}
  bool TryBind(MeshFilter filter){
    // JC | 2026-11-10 | 후보 수집 위치만 바꾸고 기존 메시 식별·가림 재질 예외·연결 정책은 그대로 사용합니다.
    if(filter==null)return false;
    var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null||filter.sharedMesh==null)return false;
    if(boundByFilter.TryGetValue(filter,out var existing)){if(filter.sharedMesh!=existing.shown&&filter.sharedMesh!=existing.original)return true;filter.sharedMesh=existing.shown;Bind(renderer);return true;}
    var source=filter.sharedMesh;Recipe recipe=null;
    foreach(var item in catalog.entries)if(source.name==item.meshName&&source.vertexCount==item.vertexCount&&IndexCount(source)==item.indexCount){recipe=item;break;}
    if(recipe==null)return false;
    // Do not replace a silhouette/unsupported material until its regular material returns.
    var slots=renderer.sharedMaterials;if(slots.Length==0||slots[0]==null||slots[0].shader.name!="Universal Render Pipeline/Lit")return true;
    if(!copies.TryGetValue(source,out var copy)){copy=Build(source,recipe);copies.Add(source,copy);}
    // JC | 2026-11-10 | 목록과 조회용 사전을 함께 등록하고 Dispose에서 함께 비웁니다.
    var binding=new Bound{filter=filter,original=source,shown=copy};bound.Add(binding);boundByFilter.Add(filter,binding);filter.sharedMesh=copy;Bind(renderer);
    return true;
  }
  static long IndexCount(Mesh mesh){long count=0;for(int i=0;i<mesh.subMeshCount;i++)count+=mesh.GetIndexCount(i);return count;}
  void Bind(Renderer renderer){
   // JC | 2026-11-10 | 슬롯 수·재질 연결·창문 플래그 설정에 한 배열을 사용하고 연결 결과도 그 배열에 즉시 반영합니다.
   var slots=renderer.sharedMaterials;
   for(int i=0;i<slots.Length;i++){materials.Bind(renderer,i,null,slots);var m=slots[i];if(m!=null&&m.shader.name=="JC/Environment/Night Lit")m.SetFloat("_JcSpecialWindow",1);}
  }
  static Mesh Build(Mesh source,Recipe recipe){
   var copy=UnityEngine.Object.Instantiate(source);copy.name=source.name+" · window faces";copy.hideFlags=HideFlags.HideAndDontSave;
   var vertices=new List<Vector3>(source.vertices);var normals=new List<Vector3>(source.normals);var uv=new List<Vector2>(source.uv);var uv2=new List<Vector2>(source.uv2);var tangents=new List<Vector4>(source.tangents);var indices=new List<int>(source.triangles);
   bool hasUv2=uv2.Count==vertices.Count,hasTangents=tangents.Count==vertices.Count;
   foreach(int face in recipe.triangles)for(int corner=0;corner<3;corner++){
    int at=face*3+corner,old=indices[at];indices[at]=vertices.Count;vertices.Add(vertices[old]);normals.Add(normals[old]);uv.Add(uv[old]+Vector2.right);if(hasUv2)uv2.Add(uv2[old]);if(hasTangents)tangents.Add(tangents[old]);
   }
   // Library source contains open frames without glass. Fill only their inner panes.
   foreach(var pane in recipe.panes){
    Vector3 right=Vector3.Cross(Vector3.up,pane.normal);int first=vertices.Count;
    foreach(var corner in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)}){
     vertices.Add(pane.center+right*(corner.x*pane.width*.5f)+Vector3.up*(corner.y*pane.height*.5f));normals.Add(pane.normal);uv.Add(new Vector2(1+1.5f/16,15.5f/16));if(hasUv2)uv2.Add(Vector2.zero);if(hasTangents)tangents.Add(new Vector4(right.x,right.y,right.z,1));
    }
    indices.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
   }
   copy.SetVertices(vertices);copy.SetNormals(normals);copy.SetUVs(0,uv);if(hasUv2)copy.SetUVs(1,uv2);if(hasTangents)copy.SetTangents(tangents);int offset=0;for(int sub=0;sub<source.subMeshCount;sub++){int count=(int)source.GetIndexCount(sub);var part=indices.GetRange(offset,count);offset+=count;if(sub==0)part.AddRange(indices.GetRange(recipe.indexCount,indices.Count-recipe.indexCount));copy.SetTriangles(part,sub);}
   copy.RecalculateBounds();return copy;
  }
  // JC | 2026-11-10 | 재연결 시 오래된 조회가 남지 않게 원본 복원 후 사전도 해제합니다.
  public void Dispose(){materials.Dispose();foreach(var b in bound)if(b.filter!=null&&b.filter.sharedMesh==b.shown)b.filter.sharedMesh=b.original;bound.Clear();boundByFilter.Clear();candidates.Clear();candidateSet.Clear();foreach(var mesh in copies.Values)if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);copies.Clear();}
 }
}
