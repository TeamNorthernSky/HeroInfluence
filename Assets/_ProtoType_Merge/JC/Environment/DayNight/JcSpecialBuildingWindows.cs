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
  readonly Dictionary<Mesh,Mesh> copies=new Dictionary<Mesh,Mesh>();
  readonly JcNightMaterialBindings materials=new JcNightMaterialBindings();
  static Catalog catalog;
  public void Scan(Scene scene){
   if(catalog==null){var data=Resources.Load<TextAsset>("JcSpecialBuildingWindows");if(data==null)return;catalog=JsonUtility.FromJson<Catalog>(data.text);}
   foreach(var root in scene.GetRootGameObjects())foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)){
    var renderer=filter.GetComponent<MeshRenderer>();if(renderer==null||filter.sharedMesh==null)continue;
    var existing=bound.Find(b=>b.filter==filter);
    if(existing!=null){if(filter.sharedMesh!=existing.shown&&filter.sharedMesh!=existing.original)continue;filter.sharedMesh=existing.shown;Bind(renderer);continue;}
    var source=filter.sharedMesh;Recipe recipe=null;
    foreach(var item in catalog.entries)if(source.name==item.meshName&&source.vertexCount==item.vertexCount&&IndexCount(source)==item.indexCount){recipe=item;break;}
    if(recipe==null)continue;
    // Do not replace a silhouette/unsupported material until its regular material returns.
    var slots=renderer.sharedMaterials;if(slots.Length==0||slots[0]==null||slots[0].shader.name!="Universal Render Pipeline/Lit")continue;
    if(!copies.TryGetValue(source,out var copy)){copy=Build(source,recipe);copies.Add(source,copy);}
    bound.Add(new Bound{filter=filter,original=source,shown=copy});filter.sharedMesh=copy;Bind(renderer);
   }
  }
  static long IndexCount(Mesh mesh){long count=0;for(int i=0;i<mesh.subMeshCount;i++)count+=mesh.GetIndexCount(i);return count;}
  void Bind(Renderer renderer){for(int i=0;i<renderer.sharedMaterials.Length;i++){materials.Bind(renderer,i,null);var m=renderer.sharedMaterials[i];if(m!=null&&m.shader.name=="JC/Environment/Night Lit")m.SetFloat("_JcSpecialWindow",1);}}
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
  public void Dispose(){materials.Dispose();foreach(var b in bound)if(b.filter!=null&&b.filter.sharedMesh==b.shown)b.filter.sharedMesh=b.original;bound.Clear();foreach(var mesh in copies.Values)if(Application.isPlaying)UnityEngine.Object.Destroy(mesh);else UnityEngine.Object.DestroyImmediate(mesh);copies.Clear();}
 }
}
