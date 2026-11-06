// JC | 2026-11-05 | 기존 원경 편집 기능을 폴리싱에서 재사용합니다. Misc 원본 작업본과 참조는 보존합니다.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using JC.EnvironmentDetail;
namespace JC.BackgroundEditing
{
 public static partial class BackgroundOps
 {
  // 저장된 kind 번호는 목록 순서를 바꾸지 않습니다. 4는 기존 UI의 비우기 예약값입니다.
  public static readonly string[] TileTextures=new[]{"Road_01","tilemap_01","tilemap_04","tilemap_02",""}.Concat(new[]{"ParkRoad_01","ParkRoad_02","ParkRoad_02-2","ParkRoad_03","ParkRoad_04","ParkRoad_04-2","ParkRoad_05","ParkRoad_05-2","ParkRoad_05-3","ParkRoad_05-4","ParkRoad_06","ParkRoad_06-2","ParkRoad_07","ParkRoad_08","ParkRoad_08-2","ParkRoad_09","ParkRoad3_01"}).Concat(Enumerable.Range(1,7).Select(i=>"Road_Centerline_"+i.ToString("000"))).ToArray();
  public static readonly string[] TileLabels=TileTextures.Select((s,i)=>i<5?new[]{"도로","인도","풀밭","수로","비우기"}[i]:s.StartsWith("Park")?"산책로 / "+s:"차선 / "+s).ToArray();
  static string SurfaceTexture(Renderer r)
  {
   var m=r.sharedMaterial;if(!m)return null;var t=m.HasProperty("_BaseMap")?m.GetTexture("_BaseMap"):null;string n=t?t.name:"";
   if(n.StartsWith("Road_",StringComparison.Ordinal)||n.StartsWith("ParkRoad",StringComparison.Ordinal)||new[]{"tilemap_01","tilemap_02","tilemap_04"}.Contains(n))return n;
   if(m.name.Contains("CityAsphalt"))return "Road_01";if(m.name.Contains("CityPavement"))return "tilemap_01";return null;
  }
  static bool Fence(MeshFilter f)=>f.name.IndexOf("Fence",StringComparison.OrdinalIgnoreCase)>=0||f.sharedMesh.name.IndexOf("Fence",StringComparison.OrdinalIgnoreCase)>=0;
  static bool OldDressing(MeshFilter f)=>f.name.StartsWith("Detail_")||f.name.StartsWith("수로 ·")||f.name.StartsWith("연속 연석")||f.name.StartsWith("도로 풀밭")||f.name=="차선";
  sealed class SurfaceCell {public string texture;public float height;}
  static Dictionary<Vector2Int,SurfaceCell> ReadSurface(BackgroundDocument d)
  {
   var map=new Dictionary<Vector2Int,SurfaceCell>();
   foreach(var f in d.baseline.GetComponentsInChildren<MeshFilter>()){
    if(f.name.StartsWith("BG")||Fence(f))continue;string texture=SurfaceTexture(f.GetComponent<Renderer>());if(texture==null)continue;
    var mesh=f.sharedMesh;var v=mesh.vertices.Select(f.transform.TransformPoint).ToArray();var ids=mesh.GetTriangles(0);
    for(int i=0;i<ids.Length;i+=3){var a=v[ids[i]];var b=v[ids[i+1]];var c=v[ids[i+2]];float den=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(den)<.00001f)continue;
     int x0=Mathf.CeilToInt(Mathf.Min(a.x,b.x,c.x)),x1=Mathf.FloorToInt(Mathf.Max(a.x,b.x,c.x)),z0=Mathf.CeilToInt(Mathf.Min(a.z,b.z,c.z)),z1=Mathf.FloorToInt(Mathf.Max(a.z,b.z,c.z));
     for(int x=x0;x<=x1;x++)for(int z=z0;z<=z1;z++){float u=((b.z-c.z)*(x-c.x)+(c.x-b.x)*(z-c.z))/den,w=((c.z-a.z)*(x-c.x)+(a.x-c.x)*(z-c.z))/den,t=1-u-w;if(u<-.0001f||w<-.0001f||t<-.0001f)continue;float y=u*a.y+w*b.y+t*c.y;var key=new Vector2Int(x,z);if(!map.TryGetValue(key,out var old)||y>old.height+.001f)map[key]=new SurfaceCell{texture=texture,height=y};}
    }
   }
   foreach(var cell in d.cells){if(cell.kind<0||cell.kind==4)map.Remove(cell.position);else if(cell.kind<TileTextures.Length)map[cell.position]=new SurfaceCell{texture=TileTextures[cell.kind],height=cell.kind==3?-.59f:cell.kind==1?.18f:.02f};}
   if(d.cropOutput)foreach(var p in map.Keys.Where(p=>p.x<d.editMin.x||p.x>d.editMax.x||p.y<d.editMin.y||p.y>d.editMax.y).ToArray())map.Remove(p);
   bool Parking(Vector2Int p)=>d.parkingAreas.Any(a=>p.x>=a.min.x&&p.x<=a.max.x&&p.y>=a.min.y&&p.y<=a.max.y);
   foreach(var pair in map)if(Parking(pair.Key)&&pair.Value.texture.StartsWith("Road_Centerline"))pair.Value.texture="Road_01";
   // JC | 2026-11-05 | 새 작업본은 원본 차선/도로를 그대로 유지하고 편집한 부분만 출력합니다.
   if(d.preserveWorld){foreach(var p in map.Keys.Where(p=>!d.cuts.Any(c=>p.x>=c.min.x&&p.x<=c.max.x&&p.y>=c.min.y&&p.y<=c.max.y)&&!d.cells.Any(c=>c.position==p)).ToArray())map.Remove(p);}
   else AddRoadLines(map,Parking);return map;
  }
  static void AddRoadLines(Dictionary<Vector2Int,SurfaceCell> map,Func<Vector2Int,bool> parking)
  {
   bool Road(Vector2Int p)=>map.TryGetValue(p,out var c)&&c.texture.StartsWith("Road_")&&!c.texture.Contains("parking");
   int Run(Vector2Int p,Vector2Int dir){int n=0;while(n<8&&Road(p+dir*(n+1)))n++;return n;}
   var add=new Dictionary<Vector2Int,string>();
   foreach(var pair in map){if(pair.Value.texture!="Road_01"||parking(pair.Key))continue;var p=pair.Key;int l=Run(p,Vector2Int.left),r=Run(p,Vector2Int.right),b=Run(p,Vector2Int.down),t=Run(p,Vector2Int.up);
    // 양쪽 끝이 닫힌 도로의 중앙에만 배치하여 교차로·광장에는 선을 긋지 않습니다.
    if(l+r>=1&&l+r<=6&&(l==r||l==r+1)&&b>=2&&t>=2&&Math.Abs(p.y)%3==0)add[p]="Road_Centerline_006";
    else if(b+t>=1&&b+t<=6&&(b==t||b==t+1)&&l>=2&&r>=2&&Math.Abs(p.x)%3==0)add[p]="Road_Centerline_005";
   }
   foreach(var p in add)map[p.Key].texture=p.Value;
  }
  static void BuildWholeSurface(BackgroundDocument d,Transform parent,bool detailed)
  {
   var map=ReadSurface(d);var world=SceneManager.GetSceneByPath("Assets/_ProtoType_Merge/Scenes/DHScene_3.unity");bool opened=!world.IsValid()||!world.isLoaded;var preview=EditorSceneManager.NewPreviewScene();var owned=new List<UnityEngine.Object>();
   if(detailed)EditorUtility.DisplayProgressBar("배경 전체 드레싱","기존 지형과 편집 지형의 호안·연석·차선을 생성합니다.",.25f);
   try{
    if(opened)world=EditorSceneManager.OpenScene("Assets/_ProtoType_Merge/Scenes/DHScene_3.unity",OpenSceneMode.Additive);
    var sources=JcExploreDetailBake.Sources(world);var input=new GameObject("입력");SceneManager.MoveGameObjectToScene(input,preview);
    bool Interior(KeyValuePair<Vector2Int,SurfaceCell> p){if(p.Value.texture!="tilemap_01")return false;for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)if(!map.TryGetValue(p.Key+new Vector2Int(x,z),out var n)||n.texture!="tilemap_01"||Mathf.Abs(n.height-p.Value.height)>.001f)return false;return true;}
    foreach(var group in map.GroupBy(k=>new{texture=k.Value.texture,interior=Interior(k)})){
     var src=sources.FirstOrDefault(f=>SurfaceTexture(f.GetComponent<Renderer>())==group.Key.texture&&(group.Key.texture!="tilemap_01"||f.name.EndsWith("_RaisedCube")))??sources.First(f=>SurfaceTexture(f.GetComponent<Renderer>())=="tilemap_04"&&!f.name.EndsWith("Cube"));
     int stride=src.name.EndsWith("Cube")?20:4;var sv=src.sharedMesh.vertices;var su=src.sharedMesh.uv;var center=src.transform.TransformPoint((sv[0]+sv[2])*.5f);var vs=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
     foreach(var cell in group){int b=vs.Count;var target=new Vector3(cell.Key.x,cell.Value.height,cell.Key.y);for(int j=0;j<stride;j++){vs.Add(src.transform.TransformPoint(sv[j])+target-center);uv.Add(su[j]);}for(int n=0;n<stride/4;n++){int k=b+n*4;tri.AddRange(new[]{k,k+2,k+1,k,k+3,k+2});}}
     var mesh=new Mesh{name="TileChunk_Misc_"+group.Key.texture,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vs);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);
     var mat=new Material(src.GetComponent<Renderer>().sharedMaterial);owned.Add(mat);if(SurfaceTexture(src.GetComponent<Renderer>())!=group.Key.texture){var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_ProtoType_Merge/DH/AlphaAsset/Tile/"+group.Key.texture+".png");if(!texture)throw new InvalidOperationException("타일 없음: "+group.Key.texture);mat.SetTexture("_BaseMap",texture);}
     var go=Child(input.transform,mesh.name+(stride==20?(group.Key.texture=="tilemap_01"?"_RaisedCube":"_FlatCube"):""));go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;if(detailed&&group.Key.interior){Visual(go.GetComponent<MeshFilter>(),input.transform,"보도 내부");go.GetComponent<Renderer>().enabled=false;}
    }
    var o=new GameObject("드레싱");SceneManager.MoveGameObjectToScene(o,preview);var overlay=o.AddComponent<JcExploreDetailOverlay>();overlay.profile=Resources.Load<JcExploreDetailProfile>("JC_ExploreDetail_Default");if(detailed)overlay.GeneratePreview("Misc 전체 배경");
    int index=0;var copier=new AssetCopier(d.assetFolder+"/Generated/WholeMaterials",false);AssetDatabase.StartAssetEditing();try{
     foreach(var f in input.GetComponentsInChildren<MeshFilter>().Concat(overlay.VisualRoot?overlay.VisualRoot.GetComponentsInChildren<MeshFilter>():Array.Empty<MeshFilter>())){var r=f.GetComponent<Renderer>();if(!r.enabled||r.forceRenderingOff)continue;var go=Visual(f,parent);go.GetComponent<MeshFilter>().sharedMesh=Store(UnityEngine.Object.Instantiate(f.sharedMesh),d.assetFolder+"/Generated/Whole_"+index+++".asset");go.GetComponent<Renderer>().sharedMaterials=r.sharedMaterials.Select(m=>(Material)copier.Copy(m)).ToArray();}
    }finally{AssetDatabase.StopAssetEditing();}
   }finally{if(detailed)EditorUtility.ClearProgressBar();EditorSceneManager.ClosePreviewScene(preview);foreach(var o in owned)if(o)UnityEngine.Object.DestroyImmediate(o);if(opened&&world.IsValid()&&world.isLoaded)EditorSceneManager.CloseScene(world,true);}
  }
 }
}
