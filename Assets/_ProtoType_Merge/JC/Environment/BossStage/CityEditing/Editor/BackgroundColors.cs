// JC | 2026-11-05 | 기존 원경 편집 기능을 폴리싱에서 재사용합니다. Misc 원본 작업본과 참조는 보존합니다.
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using JC.BuildingColors;
namespace JC.BackgroundEditing
{
    public static partial class BackgroundOps
    {
        static BgColorDefinition[] colorDefinitions;
        // JC | 2026-11-17 | DHScene_3와 TutorialExploreScene의 BgColorRoot가 실제 참조하는 최신 팔레트를 공유합니다.
        public const string ExploreColorPalettePath="Assets/_ProtoType_Merge/JC/BuildingColors/Profiles/BG_All_JC_ToneStudy_Bright_260929.asset";
        public static BgColorSetProfile ExploreColorPalette()
        {
            var profile=AssetDatabase.LoadAssetAtPath<BgColorSetProfile>(ExploreColorPalettePath);
            if(!profile)throw new InvalidOperationException("탐사씬 건물 색상 팔레트를 찾을 수 없습니다: "+ExploreColorPalettePath);
            return profile;
        }
        // JC | 2026-11-17 | 선택 상태와 무관하게 편집 가능한 건물 전체를 처리하며 가로등·잠금은 공통 필터에서 제외합니다.
        public static int ShuffleBuildingVariants(BackgroundDocument doc)
        {
            Guard();if(!doc||!doc.Buildings)throw new InvalidOperationException("도시 편집 작업본을 먼저 열어 주세요.");
            return ApplyBuildingProfiles(doc,new[]{doc.Buildings.gameObject},ExploreColorPalette(),true);
        }
        static bool EditableBuilding(BackgroundDocument doc,Renderer renderer)
        {
            if(doc.lockBuildings||!renderer.transform.IsChildOf(doc.Buildings)||SceneVisibilityManager.instance.IsPickingDisabled(renderer.gameObject))return false;
            for(var t=renderer.transform;t&&t!=doc.transform;t=t.parent)if((t.gameObject.hideFlags&HideFlags.NotEditable)!=0)return false;
            return Definition(renderer);
        }
        public static BgColorDefinition Definition(Renderer renderer)
        {
            // JC | 2026-11-17 | 선택 가능한 배치물 중 가로등·광원·메시 없는 객체는 건물 색상 대상이 아닙니다.
            if(!renderer||!renderer.TryGetComponent<MeshFilter>(out var filter)||!filter.sharedMesh||IsStreetlight(filter))return null;
            if(colorDefinitions==null)colorDefinitions=AssetDatabase.FindAssets("t:BgColorDefinition",new[]{"Assets/_ProtoType_Merge/JC/BuildingColors/Definitions","Assets/_ProtoType_Merge/DH/AlphaAsset/AIAsset/Building/JC_Mod"}).Select(g=>AssetDatabase.LoadAssetAtPath<BgColorDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            var mesh=filter.sharedMesh;
            var exact=colorDefinitions.FirstOrDefault(d=>d.prefab&&d.prefab.GetComponentsInChildren<MeshFilter>(true).Any(f=>f.sharedMesh&&f.sharedMesh.name==mesh.name&&f.sharedMesh.vertexCount==mesh.vertexCount));if(exact)return exact; return colorDefinitions.FirstOrDefault(d=>renderer.name.StartsWith(d.buildingId,StringComparison.Ordinal)&&!AssetDatabase.GetAssetPath(d).Contains("/JC_Mod/"))??colorDefinitions.FirstOrDefault(d=>(d.meshes??Array.Empty<BgMeshBinding>()).Any(b=>(b.original&&b.original.name==mesh.name&&b.original.vertexCount==mesh.vertexCount)||(b.separated&&b.separated.name==mesh.name&&b.separated.vertexCount==mesh.vertexCount)));
        }
        public static void ApplyBuildingProfile(BackgroundDocument doc,Renderer renderer,BgColorSetProfile set,bool random)
            => ApplyBuildingProfile(doc,renderer,set,random,null);

        // JC | 2026-11-17 | 같은 입력 재질·바리에이션은 한 번만 구워 전체 선택 시 동일한 텍스처를 반복 생성하지 않습니다.
        sealed class BuildingColorResult { public Mesh mesh; public Material[] materials; }
        public static int ApplyBuildingProfiles(BackgroundDocument doc,IEnumerable<GameObject> selection,BgColorSetProfile set,bool random)
        {
            Guard();
            var renderers=selection.Where(g=>g).SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).Distinct()
                .Where(r=>EditableBuilding(doc,r)).ToArray();
            if(renderers.Length==0)throw new InvalidOperationException("색상 정의가 있는 선택 가능한 건물을 선택해 주세요. 가로등과 잠긴 건물은 제외됩니다.");
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("건물 색상 바리에이션");
            var cache=new Dictionary<string,BuildingColorResult>();
            try{foreach(var renderer in renderers)ApplyBuildingProfile(doc,renderer,set,random,cache);Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);return renderers.Length;}
            catch{Undo.RevertAllDownToGroup(group);throw;}
        }
        static void ApplyBuildingProfile(BackgroundDocument doc,Renderer renderer,BgColorSetProfile set,bool random,Dictionary<string,BuildingColorResult> cache)
        {
            Guard();var def=Definition(renderer);if(!def)throw new InvalidOperationException("이 건물에 연결되는 기존 색상 정의가 없습니다. 원본 색상을 유지합니다.");
            // JC | 2026-11-17 | 탐사 BgColorRoot처럼 명명된 바리에이션만 추첨합니다. 없는 종류는 같은 팔레트의 기본색을 적용합니다.
            var entries=set?(set.buildings??Array.Empty<BgColorSetEntry>()).Where(e=>e!=null&&e.buildingId==def.buildingId).ToArray():Array.Empty<BgColorSetEntry>();
            var options=entries.Where(e=>!string.IsNullOrEmpty(e.variantId)&&e.weight>0).OrderBy(e=>e.variantId,StringComparer.Ordinal).ToArray();
            var entry=entries.FirstOrDefault(e=>string.IsNullOrEmpty(e.variantId));
            if(random&&options.Length>0){float value=UnityEngine.Random.value*options.Sum(e=>e.weight);entry=options.Last();foreach(var e in options){value-=e.weight;if(value<=0){entry=e;break;}}}
            string key=def.GetInstanceID()+":"+(entry?.variantId??"default")+":"+renderer.GetComponent<MeshFilter>().sharedMesh.GetInstanceID()+":"+string.Join(",",renderer.sharedMaterials.Select(m=>m?m.GetInstanceID():0));
            if(cache!=null&&cache.TryGetValue(key,out var cached)){var target=renderer.GetComponent<MeshFilter>();Undo.RecordObjects(new UnityEngine.Object[]{renderer,target},"건물 색상 바리에이션");target.sharedMesh=cached.mesh;renderer.sharedMaterials=cached.materials;EditorSceneManager.MarkSceneDirty(doc.gameObject.scene);return;}
            var scene=EditorSceneManager.NewPreviewScene();var original=renderer.sharedMaterials;var filter=renderer.GetComponent<MeshFilter>();
            try
            {
                var temp=new GameObject("색상 계산");SceneManager.MoveGameObjectToScene(temp,scene);var f=temp.AddComponent<MeshFilter>();f.sharedMesh=filter.sharedMesh;
                var binding=def.meshes?.FirstOrDefault(b=>(b.original&&b.original.name==f.sharedMesh.name)||(b.separated&&b.separated.name==f.sharedMesh.name));if(binding!=null)f.sharedMesh=binding.original;
                var r=temp.AddComponent<MeshRenderer>();var indices=original.Select(m=>Array.FindIndex(def.bindings,b=>b.material&&(m.name==b.material.name||m.name.StartsWith(b.material.name+" ",StringComparison.Ordinal)))).ToArray();
                                for(int i=0;i<indices.Length;i++)if(indices[i]<0){var tex=original[i].HasProperty("_BaseMap")?original[i].GetTexture("_BaseMap"):null;indices[i]=Array.FindIndex(def.bindings,b=>tex&&b.material&&b.material.HasProperty("_BaseMap")&&b.material.GetTexture("_BaseMap")==tex);}
                var reference=def.prefab?def.prefab.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(x=>x.sharedMesh&&x.sharedMesh.name==f.sharedMesh.name):null;
                if(reference){var refs=reference.GetComponent<Renderer>().sharedMaterials;for(int i=0;i<indices.Length&&i<refs.Length;i++)if(indices[i]<0)indices[i]=Array.FindIndex(def.bindings,b=>b.material==refs[i]);}
                if(def.bindings.Length==1&&original.Length==1)indices[0]=0; if(indices.All(i=>i<0))throw new InvalidOperationException("건물 재질과 기존 색상 정의를 연결할 수 없습니다.");r.sharedMaterials=original.Select((m,i)=>indices[i]>=0?def.bindings[indices[i]].material:m).ToArray();
                // JC | 2026-11-17 | 팔레트에 정의된 창문색도 그대로 사용하고 구형 청색 강제를 덮어씌우지 않습니다.
                var modifier=temp.AddComponent<BgColorModifier>();modifier.definition=def;modifier.parts=entry!=null?def.Remap(entry.parts,entry.partIds):BgColorModifier.Copy(def.Defaults);if(entry==null&&doc.blueHighWindows&&def.buildingId.StartsWith("BGHigh")){int window=Array.IndexOf(def.Ids,"window");if(window>=0)modifier.parts[window]=def.Defaults[window];}modifier.partIds=(string[])def.Ids.Clone();modifier.Rebind();modifier.ApplyNow();
                Undo.RecordObjects(new UnityEngine.Object[]{renderer,filter,renderer.gameObject},"기존 건물 색상 적용");Folder(doc.assetFolder+"/Colors");var result=(Material[])original.Clone();
                for(int i=0;i<result.Length;i++)if(indices[i]>=0)
                {
                    var block=new MaterialPropertyBlock();r.GetPropertyBlock(block,i);var texture=block.GetTexture("_BaseMap");if(!texture)throw new InvalidOperationException("색상 텍스처 생성 실패: "+modifier.Status);
                    var mat=new Material(original[i]);mat.name=def.bindings[indices[i]].material.name+" Local "+(entry?.variantId??"default");
                    mat.SetTexture("_BaseMap",SaveTexture(texture,doc.assetFolder+"/Colors/Color.png",null));if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",Color.white);
                    // JC | 2026-11-17 | 존재하지 않던 셰이더 대신 색상 정의의 창문 마스크(B)를 직접 추출합니다.
                    var mask=def.bindings[indices[i]].maskParts;if(mask&&mat.HasProperty("_EmissionMap")){mat.SetTexture("_EmissionMap",SaveTexture(mask,doc.assetFolder+"/Colors/Windows.png",null,true));mat.SetColor("_EmissionColor",Color.white);mat.EnableKeyword("_EMISSION");}
                    // JC | 2026-11-17 | CreateAsset이 재질 이름을 파일명으로 바꾸므로 건물 정의·바리에이션 식별 이름을 복원합니다.
                    string path=AssetDatabase.GenerateUniqueAssetPath(doc.assetFolder+"/Colors/Building.mat");string materialName=mat.name;AssetDatabase.CreateAsset(mat,path);mat.name=materialName;EditorUtility.SetDirty(mat);result[i]=mat;
                }
                filter.sharedMesh=f.sharedMesh;renderer.sharedMaterials=result;EditorSceneManager.MarkSceneDirty(doc.gameObject.scene);
                if(cache!=null)cache[key]=new BuildingColorResult{mesh=f.sharedMesh,materials=result};
            }finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        static Texture2D SaveTexture(Texture source,string path,Material processor,bool windowMask=false)
        {
            path=AssetDatabase.GenerateUniqueAssetPath(path);int w=Mathf.Min(source.width,4096),h=Mathf.Min(source.height,4096);var rt=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;bool write=GL.sRGBWrite;
            try{GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;if(processor)Graphics.Blit(source,rt,processor);else Graphics.Blit(source,rt);RenderTexture.active=rt;var img=new Texture2D(w,h,TextureFormat.RGBA32,false);try{img.ReadPixels(new Rect(0,0,w,h),0,0);
                // JC | 2026-11-17 | RGB를 같은 창문 가중치로 저장해 벽·지붕 마스크가 발광으로 섞이지 않게 합니다.
                if(windowMask){var pixels=img.GetPixels32();for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(pixels[i].b,pixels[i].b,pixels[i].b,255);img.SetPixels32(pixels);}
                img.Apply();File.WriteAllBytes(path,img.EncodeToPNG());}finally{UnityEngine.Object.DestroyImmediate(img);}AssetDatabase.ImportAsset(path);return AssetDatabase.LoadAssetAtPath<Texture2D>(path);}finally{GL.sRGBWrite=write;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
        }
    }
}
