// JC | 2026-11-05 | 기존 원경 편집 기능을 폴리싱에서 재사용합니다. Misc 원본 작업본과 참조는 보존합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using JC.EnvironmentDetail;
using JC.BattleTesting;
namespace JC.BackgroundEditing
{
    public static partial class BackgroundOps
    {
        public const string Local="Assets/_ProtoType_Merge/JC/Environment/BossStage/CityEditing";
        public const string Boss="Assets/_ProtoType_Merge/JC/Environment/BossStage/";
        public static void Guard(){if(Application.dataPath!="C:/Dev/HeroInfluence_Polish/Assets"||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)throw new InvalidOperationException("폴리싱 프로젝트의 편집 모드에서만 사용할 수 있습니다.");}
        public static GameObject Child(Transform parent,string name){var g=new GameObject(name);g.transform.SetParent(parent,false);return g;}
        public static void Folder(string p){if(!AssetDatabase.IsValidFolder(p)){Folder(Path.GetDirectoryName(p).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(p).Replace('\\','/'),Path.GetFileName(p));}}
        public static T Store<T>(T value,string path)where T:UnityEngine.Object{value.hideFlags=HideFlags.None;var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old){if(value is Mesh sourceMesh && old is Mesh targetMesh)CopyMesh(sourceMesh,targetMesh);else EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(value,path);return value;}
        // Mesh의 직렬화 데이터만 복사하면 기존 GPU 버퍼가 남을 수 있으므로 공개 메시 API로 갱신합니다.
        public static void CopyMesh(Mesh source,Mesh target)
        {
            target.Clear(false);target.name=source.name;target.indexFormat=source.indexFormat;
            target.vertices=source.vertices;target.normals=source.normals;target.tangents=source.tangents;target.colors=source.colors;
            for(int channel=0;channel<8;channel++){var uv=new List<Vector4>();source.GetUVs(channel,uv);if(uv.Count>0)target.SetUVs(channel,uv);}
            target.subMeshCount=source.subMeshCount;
            for(int sub=0;sub<source.subMeshCount;sub++)target.SetIndices(source.GetIndices(sub),source.GetTopology(sub),sub,false);
            target.bounds=source.bounds;target.UploadMeshData(false);
        }
        public static GameObject Visual(MeshFilter f,Transform parent,string name=null){var g=Child(parent,name??f.name);
            // JC | 2026-11-05 | 입력의 필드 좌표를 출력 부모의 로컬 좌표로 복사하여 무대 배율이 위치/크기에 한 번씩만 적용됩니다.
            g.transform.localPosition=f.transform.position;g.transform.localRotation=f.transform.rotation;g.transform.localScale=f.transform.lossyScale;g.AddComponent<MeshFilter>().sharedMesh=f.sharedMesh;var r=g.AddComponent<MeshRenderer>();r.sharedMaterials=f.GetComponent<Renderer>().sharedMaterials;// JC | 2026-11-05 | 그림자와 가로등을 메시 복사 과정에서도 보존합니다.
            var original=f.GetComponent<Renderer>();r.shadowCastingMode=original.shadowCastingMode;r.receiveShadows=original.receiveShadows;CopyLights(f.transform,g.transform);return g;}
        public static HashSet<Vector2Int> PaintCells(BackgroundDocument d,Vector2Int a,Vector2Int b,int width,bool rectangle)
        {
            if((long)(Math.Abs(b.x-a.x)+1)*(Math.Abs(b.y-a.y)+1)>10000)throw new InvalidOperationException("한 번에 10000셀 이하로 편집해 주세요.");var points=new HashSet<Vector2Int>();
            if(rectangle){for(int x=Math.Min(a.x,b.x);x<=Math.Max(a.x,b.x);x++)for(int z=Math.Min(a.y,b.y);z<=Math.Max(a.y,b.y);z++)points.Add(new Vector2Int(x,z));}
            else {if(Math.Abs(b.x-a.x)>=Math.Abs(b.y-a.y))b.y=a.y;else b.x=a.x;int count=Math.Max(Math.Abs(b.x-a.x),Math.Abs(b.y-a.y));for(int i=0;i<=count;i++){var p=Vector2Int.RoundToInt(Vector2.Lerp(a,b,count==0?0:(float)i/count));for(int n=0;n<width;n++)points.Add(p+(a.y==b.y?Vector2Int.up:Vector2Int.right)*(n-width/2));}}
            points.RemoveWhere(p=>d.limitArea&&(p.x<d.editMin.x||p.x>d.editMax.x||p.y<d.editMin.y||p.y>d.editMax.y));
            if(points.Count>10000)throw new InvalidOperationException("한 번에 10000셀 이하로 편집해 주세요.");return points;
        }
        public static void Paint(BackgroundDocument d,Vector2Int a,Vector2Int b,int kind,int width,bool rectangle)
        {
            Guard();if(kind < -1 || kind >= TileTextures.Length)throw new ArgumentOutOfRangeException(nameof(kind));
            var points=PaintCells(d,a,b,width,rectangle);if(points.Count==0)return;
            Undo.RecordObject(d,"배경 지형 칠하기");var map=d.cells.ToDictionary(c=>c.position,c=>c.kind);foreach(var p in points)map[p]=kind;
            d.cells=map.OrderBy(p=>p.Key.x).ThenBy(p=>p.Key.y).Select(p=>new BackgroundDocument.Cell{position=p.Key,kind=p.Value}).ToList();
            // 선/사각형 도구는 축 정렬된 연속 사각형을 만듭니다.
            var cut=new BackgroundDocument.Cut{min=new Vector2(points.Min(p=>p.x)-.5f,points.Min(p=>p.y)-.5f),max=new Vector2(points.Max(p=>p.x)+.5f,points.Max(p=>p.y)+.5f)};
            d.cuts.RemoveAll(c=>c.min.x>=cut.min.x&&c.min.y>=cut.min.y&&c.max.x<=cut.max.x&&c.max.y<=cut.max.y);d.cuts.Add(cut);
            EditorUtility.SetDirty(d);EditorSceneManager.MarkSceneDirty(d.gameObject.scene);Rebuild(d,d.dressWhilePainting);
        }
        public static void ApplyStageFraming(BackgroundDocument d,GameObject stage)
        {
            if(d.windowlessPillar){var pillar=stage.GetComponentsInChildren<MeshFilter>().First(f=>f.name=="Pillar");pillar.sharedMesh=d.windowlessPillar;}
            if(d.foregroundFloor&&!stage.transform.Find("실내 앞쪽 바닥 보완")){
                var source=stage.GetComponentsInChildren<MeshRenderer>().First(r=>r.name=="실내 바닥 연장");
                var go=Child(stage.transform,"실내 앞쪽 바닥 보완");go.AddComponent<MeshFilter>().sharedMesh=d.foregroundFloor;go.AddComponent<MeshRenderer>().sharedMaterials=source.sharedMaterials;
            }
            stage.transform.localPosition=stage.transform.localPosition*d.stageScale+d.stageOffset;
            stage.transform.localScale*=d.stageScale;
            var city=stage.transform.Find("도시 원경 (베이크)")??stage.transform.Find("도시 미리보기");
            if(city&&d.overrideCityAltitude)city.position=new Vector3(city.position.x,d.cityAltitude,city.position.z);
        }
        public static void Rebuild(BackgroundDocument d,bool detailed=true)
        {
            Guard();if(!d||!d.baseline)return;Folder(d.assetFolder+"/Generated");
            var terrain=d.Terrain;if(!terrain)terrain=Child(d.transform,"지형 결과").transform;
            foreach(Transform t in terrain.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
            int index=0;
            AssetDatabase.StartAssetEditing();try{
            foreach(var f in d.baseline.GetComponentsInChildren<MeshFilter>())
            {
                if(IsProp(f)||(!d.preserveWorld&&(Fence(f)||OldDressing(f)||SurfaceTexture(f.GetComponent<Renderer>())!=null)))continue;var g=Visual(f,terrain);Mesh current=null;
                foreach(var c in d.cuts){var bounds=g.GetComponent<Renderer>().bounds;if(bounds.max.x<=c.min.x||bounds.min.x>=c.max.x||bounds.max.z<=c.min.y||bounds.min.z>=c.max.y)continue;var next=Clip(g.GetComponent<MeshFilter>(),c.min,c.max);if(current)UnityEngine.Object.DestroyImmediate(current);current=next;g.GetComponent<MeshFilter>().sharedMesh=current;}
                if(current){g.GetComponent<MeshFilter>().sharedMesh=Store(current,d.assetFolder+"/Generated/Cut_"+index+".asset");}index++;
            }
            }finally{AssetDatabase.StopAssetEditing();}
            // JC | 2026-11-05 | 원본에는 손대지 않고 사용자가 칠한 구역만 다시 만듭니다.
            if(!d.preserveWorld||d.cuts.Count>0||d.cells.Count>0)BuildWholeSurface(d,terrain,detailed);
            if(d.borderMountains)foreach(var f in d.borderMountains.GetComponentsInChildren<MeshFilter>())Visual(f,terrain,"경계 산 · "+f.name);
            ApplyLocks(d);
            EditorSceneManager.MarkSceneDirty(d.gameObject.scene);SceneView.RepaintAll();
        }
        static void BuildPainted(BackgroundDocument d,Transform parent)
        {
            var world=SceneManager.GetSceneByPath("Assets/_ProtoType_Merge/Scenes/DHScene_3.unity");bool opened=!world.IsValid()||!world.isLoaded;
            var preview=EditorSceneManager.NewPreviewScene();var owned=new List<UnityEngine.Object>();
            try{
                if(opened)world=EditorSceneManager.OpenScene("Assets/_ProtoType_Merge/Scenes/DHScene_3.unity",OpenSceneMode.Additive);
                var sources=JcExploreDetailBake.Sources(world);string[] textures={"Road_01","tilemap_01","tilemap_04","tilemap_02"};
                var input=new GameObject("입력");SceneManager.MoveGameObjectToScene(input,preview);
                var edited=d.cells.ToDictionary(c=>c.position,c=>c.kind);
                var context=new Dictionary<Vector2Int,int>(edited);
                foreach(var c in d.cells.Where(c=>c.kind>=0))foreach(var delta in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})
                {var p=c.position+delta;if(!context.ContainsKey(p))context[p]=Sample(d,p);}
                foreach(var group in context.Where(c=>c.Value>=0).GroupBy(c=>c.Value))
                {
                    var src=sources.First(f=>f.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap")?.name==textures[group.Key]&&(group.Key!=1||f.name.EndsWith("_RaisedCube")));
                    int stride=src.name.EndsWith("Cube")?20:4;var sm=src.sharedMesh;var sv=sm.vertices;var su=sm.uv;var center=src.transform.TransformPoint((sv[0]+sv[2])*.5f);var vs=new List<Vector3>();var uv=new List<Vector2>();var tri=new List<int>();
                    foreach(var cell in group){int b=vs.Count;var target=new Vector3(cell.Key.x,center.y,cell.Key.y);for(int j=0;j<stride;j++){vs.Add(src.transform.TransformPoint(sv[j])+target-center);uv.Add(su[j]);}for(int n=0;n<stride/4;n++){int k=b+n*4;tri.AddRange(new[]{k,k+2,k+1,k,k+3,k+2});}}
                    var mesh=new Mesh{name=src.name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vs);mesh.SetUVs(0,uv);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);var go=Child(input.transform,src.name);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=src.GetComponent<Renderer>().sharedMaterial;
                }
                var o=new GameObject("드레싱");SceneManager.MoveGameObjectToScene(o,preview);var overlay=o.AddComponent<JcExploreDetailOverlay>();overlay.profile=Resources.Load<JcExploreDetailProfile>("JC_ExploreDetail_Default");overlay.GeneratePreview("Misc 배경 편집");
                int mi=0;var copier=new AssetCopier(d.assetFolder+"/Generated/Materials",false);
                AssetDatabase.StartAssetEditing();try{
                foreach(var f in input.GetComponentsInChildren<MeshFilter>().Concat(overlay.VisualRoot?overlay.VisualRoot.GetComponentsInChildren<MeshFilter>():Array.Empty<MeshFilter>()))
                {
                    var r=f.GetComponent<Renderer>();if(!r.enabled||r.forceRenderingOff)continue;
                    var mesh=PaintedOnly(f,edited);if(mesh.triangles.Length==0){UnityEngine.Object.DestroyImmediate(mesh);continue;}
                    var go=Visual(f,parent);go.GetComponent<MeshFilter>().sharedMesh=Store(mesh,d.assetFolder+"/Generated/Paint_"+mi+++".asset");
                    go.GetComponent<Renderer>().sharedMaterials=r.sharedMaterials.Select(m=>(Material)copier.Copy(m)).ToArray();
                }
                }finally{AssetDatabase.StopAssetEditing();}
            }finally{EditorSceneManager.ClosePreviewScene(preview);foreach(var o in owned)if(o)UnityEngine.Object.DestroyImmediate(o);if(opened&&world.IsValid()&&world.isLoaded)EditorSceneManager.CloseScene(world,true);}
        }
        // 주변 셀은 경계 계산에만 사용하며 원본 지형 위에 다시 출력하지 않습니다.
        static Mesh PaintedOnly(MeshFilter filter,Dictionary<Vector2Int,int> edited)
        {
            var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);var vertices=mesh.vertices;
            for(int sub=0;sub<mesh.subMeshCount;sub++){
                var source=mesh.GetTriangles(sub);var kept=new List<int>();
                for(int i=0;i<source.Length;i+=3){var p=filter.transform.TransformPoint((vertices[source[i]]+vertices[source[i+1]]+vertices[source[i+2]])/3f);
                    if(edited.TryGetValue(new Vector2Int(Mathf.RoundToInt(p.x),Mathf.RoundToInt(p.z)),out int kind)&&kind>=0){kept.Add(source[i]);kept.Add(source[i+1]);kept.Add(source[i+2]);}}
                mesh.SetTriangles(kept,sub);
            }
            var submeshes=Enumerable.Range(0,mesh.subMeshCount).Select(mesh.GetTriangles).ToArray();
            var used=submeshes.SelectMany(x=>x).Distinct().ToArray();var remap=used.Select((old,index)=>new{old,index}).ToDictionary(x=>x.old,x=>x.index);
            var normals=mesh.normals;var tangents=mesh.tangents;var colors=mesh.colors;var uv=new List<Vector4>[4];for(int channel=0;channel<4;channel++){uv[channel]=new List<Vector4>();mesh.GetUVs(channel,uv[channel]);}
            mesh.Clear();mesh.vertices=used.Select(i=>vertices[i]).ToArray();
            if(normals.Length==vertices.Length)mesh.normals=used.Select(i=>normals[i]).ToArray();if(tangents.Length==vertices.Length)mesh.tangents=used.Select(i=>tangents[i]).ToArray();if(colors.Length==vertices.Length)mesh.colors=used.Select(i=>colors[i]).ToArray();
            for(int channel=0;channel<4;channel++)if(uv[channel].Count==vertices.Length){var source=uv[channel];mesh.SetUVs(channel,used.Select(i=>source[i]).ToList());}
            mesh.subMeshCount=submeshes.Length;for(int sub=0;sub<submeshes.Length;sub++)mesh.SetTriangles(submeshes[sub].Select(i=>remap[i]).ToArray(),sub);
            mesh.RecalculateBounds();return mesh;
        }
        public static void ApplyLocks(BackgroundDocument d)
        {
            SceneVisibilityManager.instance.DisablePicking(d.Terrain.gameObject,true);d.Terrain.gameObject.hideFlags=HideFlags.NotEditable;
            foreach(Transform t in d.Buildings){var r=t.GetComponent<Renderer>();var c=r?r.bounds.center:t.position;bool locked=d.lockBuildings||(d.limitArea&&(c.x<d.editMin.x-.5f||c.x>d.editMax.x+.5f||c.z<d.editMin.y-.5f||c.z>d.editMax.y+.5f));t.gameObject.hideFlags=locked?HideFlags.NotEditable:HideFlags.None;if(locked)SceneVisibilityManager.instance.DisablePicking(t.gameObject,true);else SceneVisibilityManager.instance.EnablePicking(t.gameObject,true);}
        }
    }
}
