// JC | 2026-11-05 | 기존 원경 편집 기능을 폴리싱에서 재사용합니다. Misc 원본 작업본과 참조는 보존합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using JC.BattleTesting;
namespace JC.BackgroundEditing
{
    public sealed class AssetCopier
    {
        // JC | 2026-11-09 | 탐사 베이크는 정리 대상이므로 보스 가져오기·내보내기 모두 독립 복사합니다.
        public static bool IsExploreBake(string path)=>path.StartsWith("Assets/_ProtoType_Merge/JC/Environment/ExploreDetail/Baked/",StringComparison.Ordinal);
        readonly string folder;readonly bool includeBoss;readonly Dictionary<UnityEngine.Object,UnityEngine.Object> cache=new Dictionary<UnityEngine.Object,UnityEngine.Object>();int transient;
        public AssetCopier(string folder,bool includeBoss){this.folder=folder;this.includeBoss=includeBoss;BackgroundOps.Folder(folder);}
        public UnityEngine.Object Copy(UnityEngine.Object source)
        {
            if(!source)return null;if(cache.TryGetValue(source,out var hit))return hit;
            var path=AssetDatabase.GetAssetPath(source);bool local=path.StartsWith(BackgroundOps.Local,StringComparison.Ordinal)||path.StartsWith("Assets/_MiscLocal/",StringComparison.Ordinal);
            if(EditorUtility.IsPersistent(source)&&!local&&!IsExploreBake(path)&&!(includeBoss&&path.StartsWith(BackgroundOps.Boss,StringComparison.Ordinal)))return source;
            if(source is RenderTexture)return null;
            if(!(source is Mesh)&&!(source is Material)&&!(source is Texture2D))throw new InvalidOperationException("내보낼 수 없는 로컬 의존성: "+source.name+" "+path);
            string id;if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long fileID)&&!string.IsNullOrEmpty(guid))id=guid+"_"+fileID;else id="transient_"+transient++;
            string target=folder+"/"+source.GetType().Name+"_"+id+".asset";
            var clone=source is Texture2D texture&&!texture.isReadable?CopyTexture(texture):UnityEngine.Object.Instantiate(source);clone.name=source.name;clone.hideFlags=HideFlags.None;cache[source]=clone;
            if(clone is Material m)
            {
                foreach(string p in m.GetTexturePropertyNames())m.SetTexture(p,Copy(m.GetTexture(p)) as Texture);
                // 셰이더 변경 전의 비활성 텍스처 슬롯도 직렬화 의존성에 남으므로 함께 복사합니다.
                var serialized=new SerializedObject(m);var property=serialized.GetIterator();while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference&&property.objectReferenceValue is Texture textureRef)property.objectReferenceValue=Copy(textureRef);serialized.ApplyModifiedPropertiesWithoutUndo();
                if(m.HasProperty("_PlanarAvailable"))m.SetFloat("_PlanarAvailable",0);if(m.HasProperty("_FollowCanal"))m.SetFloat("_FollowCanal",0);
            }
            var existing=AssetDatabase.LoadMainAssetAtPath(target);if(existing){if(clone is Mesh sourceMesh && existing is Mesh targetMesh)BackgroundOps.CopyMesh(sourceMesh,targetMesh);else EditorUtility.CopySerialized(clone,existing);UnityEngine.Object.DestroyImmediate(clone);EditorUtility.SetDirty(existing);cache[source]=existing;return existing;}
            AssetDatabase.CreateAsset(clone,target);clone.name=source.name;EditorUtility.SetDirty(clone);return clone;
        }
        static Texture2D CopyTexture(Texture2D source)
        {
            bool srgb=UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(source.graphicsFormat);
            var rt=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,srgb?RenderTextureReadWrite.sRGB:RenderTextureReadWrite.Linear);
            var previous=RenderTexture.active;bool write=GL.sRGBWrite;
            try{GL.sRGBWrite=srgb&&QualitySettings.activeColorSpace==ColorSpace.Linear;Graphics.Blit(source,rt);RenderTexture.active=rt;
                var copy=new Texture2D(source.width,source.height,TextureFormat.RGBA32,source.mipmapCount>1,!srgb){wrapMode=source.wrapMode,filterMode=source.filterMode,anisoLevel=source.anisoLevel};
                copy.ReadPixels(new Rect(0,0,source.width,source.height),0,0);copy.Apply();return copy;
            }finally{GL.sRGBWrite=write;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
        }
        public void RemapHierarchy(GameObject root)
        {
            foreach(var f in root.GetComponentsInChildren<MeshFilter>(true))f.sharedMesh=(Mesh)Copy(f.sharedMesh);
            foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(m=>(Material)Copy(m)).ToArray();
        }
        // JC | 2026-11-09 | 배경 재출력 때도 프로필의 원본 재질과 새 복제본을 대응시켜 조절 연결을 유지합니다.
        public void BindSurfaceProfile(JcBossSurfaceProfile profile)
        {
            if(!profile)return;
            foreach(var pair in cache)
                if(pair.Key is Material original && pair.Value is Material exported)
                {
                    var source=profile.ResolveSource(original);
                    if(profile.surfaces.Any(s=>s!=null && s.source==source))profile.RegisterExported(source,exported);
                }
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssetIfDirty(profile);
        }
    }
    public static partial class BackgroundOps
    {
        public static string Export(BackgroundDocument d,bool selectResult=true)
        {
            // JC | 2026-11-16 | 도시 단위 결과가 전체 보스 배경/프로필을 덮어쓰는 경로로 들어가지 않게 합니다.
            if(d && d.IsCityTile)throw new InvalidOperationException("도시 단위 작업은 도시 프리팹 생성 또는 해당 도시에만 적용을 사용해 주세요.");
            Guard();Rebuild(d);Save(d);string dir=Boss+"Deliverables/"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");Folder(dir);
            var scene=EditorSceneManager.NewPreviewScene();GameObject output=null;
            try
            {
                output=UnityEngine.Object.Instantiate(d.stage);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(output,scene);output.name="BossStage";
                // Prefab 연결을 포함하지 않는 독립적인 최종 무대로 저장합니다.
                var old=output.transform.Find("도시 원경 (베이크)");if(!old)throw new InvalidOperationException("무대의 도시 원경 기준이 없습니다.");var pos=old.localPosition;var rot=old.localRotation;var scale=old.localScale;UnityEngine.Object.DestroyImmediate(old.gameObject);
                var city=Child(output.transform,"도시 원경 (베이크)");
                CopyCityVisuals(d,city.transform);
                city.transform.localPosition=pos;city.transform.localRotation=rot;city.transform.localScale=scale;
                // JC | 2026-11-05 | 실광 결과는 이전 색상 곱셈을 사용하지 않습니다.
                city.AddComponent<JcBossCityTime>().worldLighting=true;
                foreach(var c in output.GetComponentsInChildren<Component>(true))if(!(c is Transform)&&!(c is MeshFilter)&&!(c is MeshRenderer)&&!(c is JcBossCityTime)&&!(c is Light)&&!(c is DayNightLightController)&&!(c is UnityEngine.Rendering.Universal.UniversalAdditionalLightData))UnityEngine.Object.DestroyImmediate(c);
                ApplyStageFraming(d,output);
                var copier=new AssetCopier(dir+"/Assets",true);
                AssetDatabase.StartAssetEditing();try{copier.RemapHierarchy(output);}finally{AssetDatabase.StopAssetEditing();}
                string file=dir+"/BossStage.prefab";PrefabUtility.SaveAsPrefabAsset(output,file);AssetDatabase.SaveAssets();
                var dependencies=AssetDatabase.GetDependencies(file,true);
                // JC | 2026-11-09 | 탐사 베이크 참조가 남은 배포본은 통과시키지 않아 정리 시 재손실을 방지합니다.
                var invalid=dependencies.Where(p=>AssetCopier.IsExploreBake(p)||p.StartsWith("Assets/_MiscLocal/",StringComparison.Ordinal)||(p.StartsWith("Assets/",StringComparison.Ordinal)&&p.Contains("/Editor/"))||p==Boss+"BossCity_Source.prefab"||p.StartsWith(Boss+"SourceMeshes/",StringComparison.Ordinal)).ToArray();
                if(invalid.Length>0)throw new InvalidOperationException("로컬/편집 의존성이 남았습니다: "+string.Join("\n",invalid));
                // JC | 2026-11-09 | 결과 검증 후 저장 연결만 갱신하며 기존 표면 조절값은 변경하지 않습니다.
                copier.BindSurfaceProfile(JcBossBattleBackground.Load()?.surfaceProfile);
                File.WriteAllLines(dir+"/Dependencies.txt",dependencies);AssetDatabase.ImportAsset(dir+"/Dependencies.txt");
                AssetDatabase.ExportPackage(dependencies.Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)).ToArray(),dir+"/BossBackground.unitypackage",ExportPackageOptions.Default);
                if(selectResult){Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(file);EditorGUIUtility.PingObject(Selection.activeObject);}return file;
            }finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        public static GameObject Place(BackgroundDocument d,MeshFilter template,Vector3 position,float angle)
        {
            Guard();if(d.lockBuildings)throw new InvalidOperationException("건물 잠금을 해제해 주세요.");var go=Visual(template,d.Buildings);go.name=template.name;
            // 템플릿의 원점 차이와 원본 스케일을 보존하면서 바닥 중심으로 배치합니다.
            var bounds=go.GetComponent<Renderer>().bounds;var offset=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)-go.transform.position;
            go.transform.position=position-offset;go.transform.RotateAround(position,Vector3.up,angle);Undo.RegisterCreatedObjectUndo(go,"건물 배치");Selection.activeGameObject=go;EditorSceneManager.MarkSceneDirty(d.gameObject.scene);return go;
        }
        public static void CopyBlock(BackgroundDocument d,Vector2Int min,Vector2Int max,Vector2Int delta)
        {
            Guard();if(delta==Vector2Int.zero)return;
            if(!d||!d.baseline||!d.Terrain||!d.Buildings)throw new InvalidOperationException("도시 편집 작업본을 먼저 열어 주세요.");
            // JC | 2026-11-17 | 전체 도시(예:99×39셀)를 지원합니다. 범위 검사는 Undo/에셋 생성 전에 끝냅니다.
            var low=Vector2Int.Min(min,max);max=Vector2Int.Max(min,max);min=low;
            long sizeX=(long)max.x-min.x+1,sizeZ=(long)max.y-min.y+1;
            if(sizeX>10000||sizeZ>10000||sizeX*sizeZ>10000)throw new InvalidOperationException("구획 복제는 한 번에 10000셀 이하로 선택해 주세요.");
            var targetMin=new Vector2Int(checked(min.x+delta.x),checked(min.y+delta.y));
            var targetMax=new Vector2Int(checked(max.x+delta.x),checked(max.y+delta.y));
            if(d.limitArea&&(targetMin.x<d.editMin.x||targetMax.x>d.editMax.x||targetMin.y<d.editMin.y||targetMax.y>d.editMax.y))throw new InvalidOperationException("복제 대상이 편집 영역 밖입니다.");
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("구획 복제");
            string folder=d.assetFolder+"/Blocks/"+Guid.NewGuid().ToString("N");
            try
            {
                PrepareEditableProps(d);
                // JC | 2026-11-17 | 지형 종류만 다시 칠하지 않고 현재 메시/재질/높이/호안/교량을 그대로 영역 복제합니다.
                // 기존 칠하기 결과까지 독립 기준 프리팹에 보관하여 겹치는 복제와 후속 편집·Undo/Redo를 안전하게 지원합니다.
                var baseline=CopyBlockBaseline(d,min,max,delta,folder);
                if(!d.lockBuildings)foreach(Transform t in d.Buildings.Cast<Transform>().ToArray())
                {
                    var renderer=t.GetComponent<Renderer>();
                    if(!renderer&&!t.GetComponentInChildren<Light>(true))continue;
                    var center=renderer?renderer.bounds.center:t.position;
                    if(center.x>=min.x-.5f&&center.x<=max.x+.5f&&center.z>=min.y-.5f&&center.z<=max.y+.5f)
                    {
                        var go=UnityEngine.Object.Instantiate(t.gameObject,d.Buildings);go.transform.position+=new Vector3(delta.x,0,delta.y);Undo.RegisterCreatedObjectUndo(go,"구획 복제");
                    }
                }
                // JC | 2026-11-17 | 생성 객체 Undo가 진행 중 기록을 확정하므로 배치물 등록을 마친 뒤 문서 변경을 기록합니다.
                Undo.RegisterCompleteObjectUndo(d,"구획 복제");
                // JC | 2026-11-17 | 복제한 완성 마감을 새 기준에 포함했으므로 이후 일반 복원에서 또 전부 생성하지 않습니다.
                d.baseline=baseline;d.cells=new List<BackgroundDocument.Cell>();d.cuts=new List<BackgroundDocument.Cut>();d.preserveWorld=true;d.regenerateSurfaceDetails=false;
                Rebuild(d);EditorUtility.SetDirty(d);Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                if(AssetDatabase.IsValidFolder(folder))AssetDatabase.DeleteAsset(folder);
                Rebuild(d);throw;
            }
        }

        // JC | 2026-11-17 | 원본 작업 에셋은 수정하지 않고 새 기준에 지형만 확정합니다. 건물·가로등의 실제 배치는 기존 그룹이 소유합니다.
        static GameObject CopyBlockBaseline(BackgroundDocument d,Vector2Int min,Vector2Int max,Vector2Int delta,string folder)
        {
            var scene=EditorSceneManager.NewPreviewScene();var owned=new List<Mesh>();
            var from=(Vector2)min-Vector2.one*.5f;var to=(Vector2)max+Vector2.one*.5f;
            var offset=new Vector3(delta.x,0,delta.y);
            try
            {
                var root=new GameObject("도시 구획 복제 · 기준");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
                var terrain=Child(root.transform,"지형 결과").transform;var props=Child(root.transform,"건물 배치").transform;
                bool Overlaps(MeshFilter f,Vector2 a,Vector2 b){var bounds=f.GetComponent<Renderer>().bounds;return bounds.max.x>a.x&&bounds.min.x<b.x&&bounds.max.z>a.y&&bounds.min.z<b.y;}
                void Add(MeshFilter source,Mesh mesh,Vector3 shift)
                {
                    if(!mesh||mesh.vertexCount==0)return;
                    var copy=Visual(source,terrain);copy.GetComponent<MeshFilter>().sharedMesh=mesh;copy.transform.position+=shift;
                    copy.SetActive(source.gameObject.activeInHierarchy);
                }
                foreach(var f in d.Terrain.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(!f.sharedMesh||!f.GetComponent<MeshRenderer>())continue;
                    // 별도 산 프리팹은 Rebuild가 기존대로 설치하므로 기준에 중복 보관하지 않습니다.
                    if(d.borderMountains&&f.name.StartsWith("경계 산 · ",StringComparison.Ordinal))continue;
                    if(Overlaps(f,from+delta,to+delta)){var remaining=Clip(f,from+delta,to+delta);owned.Add(remaining);Add(f,remaining,Vector3.zero);}
                    else Add(f,f.sharedMesh,Vector3.zero);
                    if(Overlaps(f,from,to)){var copied=Clip(f,from,to,true);owned.Add(copied);Add(f,copied,offset);}
                }
                // 기존 배치 팔레트와 도시 시간대 설정도 새 기준에 유지합니다.
                foreach(var f in d.baseline.GetComponentsInChildren<MeshFilter>(true).Where(IsProp))Visual(f,props);
                var time=d.baseline.GetComponent<JcBossCityTime>();
                if(time){var copy=root.AddComponent<JcBossCityTime>();EditorUtility.CopySerialized(time,copy);copy.UseWorldLighting();}
                // JC | 2026-11-17 | 큰 구획의 메시를 하나씩 재임포트하지 않고 전체 복사 후 한 번에 반영합니다.
                AssetDatabase.StartAssetEditing();
                try{new AssetCopier(folder+"/Assets",false).RemapHierarchy(root);}
                finally{AssetDatabase.StopAssetEditing();}
                var saved=PrefabUtility.SaveAsPrefabAsset(root,folder+"/Baseline.prefab");
                if(!saved)throw new IOException("복제 지형의 기준 프리팹을 저장하지 못했습니다.");
                return saved;
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);foreach(var mesh in owned)if(mesh)UnityEngine.Object.DestroyImmediate(mesh);}
        }
    }
}
