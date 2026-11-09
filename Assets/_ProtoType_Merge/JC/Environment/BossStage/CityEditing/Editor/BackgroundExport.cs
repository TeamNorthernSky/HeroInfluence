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
            Guard();if(delta==Vector2Int.zero)return;Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("구획 복제");Undo.RecordObject(d,"구획 복제");
            if((long)(max.x-min.x+1)*(max.y-min.y+1)>2500)throw new InvalidOperationException("구획 복제는 한 번에 2500셀 이하로 선택해 주세요.");var copied=new List<BackgroundDocument.Cell>();for(int x=min.x;x<=max.x;x++)for(int z=min.y;z<=max.y;z++){var p=new Vector2Int(x,z);var dest=p+delta;if(d.limitArea&&(dest.x<d.editMin.x||dest.x>d.editMax.x||dest.y<d.editMin.y||dest.y>d.editMax.y))throw new InvalidOperationException("복제 대상이 편집 영역 밖입니다.");copied.Add(new BackgroundDocument.Cell{position=p,kind=Sample(d,p)});}
            var map=d.cells.ToDictionary(c=>c.position,c=>c.kind);foreach(var c in copied)map[c.position+delta]=c.kind;d.cells=map.Select(p=>new BackgroundDocument.Cell{position=p.Key,kind=p.Value}).ToList();
            if(copied.Count>0)d.cuts.Add(new BackgroundDocument.Cut{min=(Vector2)(min+delta)-Vector2.one*.5f,max=(Vector2)(max+delta)+Vector2.one*.5f});
            if(!d.lockBuildings)foreach(Transform t in d.Buildings.Cast<Transform>().ToArray()){var b=t.GetComponent<Renderer>()?.bounds;if(b.HasValue&&b.Value.center.x>=min.x&&b.Value.center.x<=max.x&&b.Value.center.z>=min.y&&b.Value.center.z<=max.y){var go=UnityEngine.Object.Instantiate(t.gameObject,d.Buildings);go.transform.position+=new Vector3(delta.x,0,delta.y);Undo.RegisterCreatedObjectUndo(go,"구획 복제");}}
            Rebuild(d);Undo.CollapseUndoOperations(group);EditorUtility.SetDirty(d);
        }
    }
}
