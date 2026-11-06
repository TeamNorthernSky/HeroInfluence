// JC | 2026-11-05 | 기존 원경 편집 기능을 폴리싱에서 재사용합니다. Misc 원본 작업본과 참조는 보존합니다.
using System;
using System.IO;
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
        public static BgColorDefinition Definition(Renderer renderer)
        {
            if(colorDefinitions==null)colorDefinitions=AssetDatabase.FindAssets("t:BgColorDefinition",new[]{"Assets/_ProtoType_Merge/JC/BuildingColors/Definitions","Assets/_ProtoType_Merge/DH/AlphaAsset/AIAsset/Building/JC_Mod"}).Select(g=>AssetDatabase.LoadAssetAtPath<BgColorDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
            var exact=colorDefinitions.FirstOrDefault(d=>d.prefab&&d.prefab.GetComponentsInChildren<MeshFilter>(true).Any(f=>f.sharedMesh&&f.sharedMesh.name==mesh.name&&f.sharedMesh.vertexCount==mesh.vertexCount));if(exact)return exact; return colorDefinitions.FirstOrDefault(d=>renderer.name.StartsWith(d.buildingId,StringComparison.Ordinal)&&!AssetDatabase.GetAssetPath(d).Contains("/JC_Mod/"))??colorDefinitions.FirstOrDefault(d=>(d.meshes??Array.Empty<BgMeshBinding>()).Any(b=>(b.original&&b.original.name==mesh.name&&b.original.vertexCount==mesh.vertexCount)||(b.separated&&b.separated.name==mesh.name&&b.separated.vertexCount==mesh.vertexCount)));
        }
        public static void ApplyBuildingProfile(BackgroundDocument doc,Renderer renderer,BgColorSetProfile set,bool random)
        {
            Guard();var def=Definition(renderer);if(!def)throw new InvalidOperationException("이 건물에 연결되는 기존 색상 정의가 없습니다. 원본 색상을 유지합니다.");
            var options=set?set.buildings.Where(e=>e.buildingId==def.buildingId&&(!random||e.weight>0)).ToArray():Array.Empty<BgColorSetEntry>();
            var entry=options.FirstOrDefault(e=>string.IsNullOrEmpty(e.variantId))??options.FirstOrDefault();
            if(random&&options.Length>0){float value=UnityEngine.Random.value*options.Sum(e=>e.weight);entry=options.Last();foreach(var e in options){value-=e.weight;if(value<=0){entry=e;break;}}}
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
                var modifier=temp.AddComponent<BgColorModifier>();modifier.definition=def;modifier.parts=entry!=null?def.Remap(entry.parts,entry.partIds):BgColorModifier.Copy(def.Defaults);if(doc.blueHighWindows&&def.buildingId.StartsWith("BGHigh")){int window=Array.IndexOf(def.Ids,"window");if(window>=0)modifier.parts[window]=def.Defaults[window];}modifier.partIds=(string[])def.Ids.Clone();modifier.Rebind();modifier.ApplyNow();
                Undo.RecordObjects(new UnityEngine.Object[]{renderer,filter,renderer.gameObject},"기존 건물 색상 적용");Folder(doc.assetFolder+"/Colors");var result=(Material[])original.Clone();
                for(int i=0;i<result.Length;i++)if(indices[i]>=0)
                {
                    var block=new MaterialPropertyBlock();r.GetPropertyBlock(block,i);var texture=block.GetTexture("_BaseMap");if(!texture)throw new InvalidOperationException("색상 텍스처 생성 실패: "+modifier.Status);
                    var mat=new Material(original[i]);mat.name=def.bindings[indices[i]].material.name+" Local "+(entry?.variantId??"default");
                    mat.SetTexture("_BaseMap",SaveTexture(texture,doc.assetFolder+"/Colors/Color.png",null));if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",Color.white);
                    var mask=def.bindings[indices[i]].maskParts;if(mask&&mat.HasProperty("_EmissionMap")){var processor=new Material(Shader.Find("Hidden/JC/Boss Window Mask"));try{mat.SetTexture("_EmissionMap",SaveTexture(mask,doc.assetFolder+"/Colors/Windows.png",processor));}finally{UnityEngine.Object.DestroyImmediate(processor);}mat.SetColor("_EmissionColor",Color.white);mat.EnableKeyword("_EMISSION");}
                    string path=AssetDatabase.GenerateUniqueAssetPath(doc.assetFolder+"/Colors/Building.mat");AssetDatabase.CreateAsset(mat,path);result[i]=mat;
                }
                filter.sharedMesh=f.sharedMesh;renderer.sharedMaterials=result;EditorSceneManager.MarkSceneDirty(doc.gameObject.scene);
            }finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        static Texture2D SaveTexture(Texture source,string path,Material processor)
        {
            path=AssetDatabase.GenerateUniqueAssetPath(path);int w=Mathf.Min(source.width,4096),h=Mathf.Min(source.height,4096);var rt=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;bool write=GL.sRGBWrite;
            try{GL.sRGBWrite=QualitySettings.activeColorSpace==ColorSpace.Linear;if(processor)Graphics.Blit(source,rt,processor);else Graphics.Blit(source,rt);RenderTexture.active=rt;var img=new Texture2D(w,h,TextureFormat.RGBA32,false);img.ReadPixels(new Rect(0,0,w,h),0,0);img.Apply();File.WriteAllBytes(path,img.EncodeToPNG());UnityEngine.Object.DestroyImmediate(img);AssetDatabase.ImportAsset(path);return AssetDatabase.LoadAssetAtPath<Texture2D>(path);}finally{GL.sRGBWrite=write;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
        }
    }
}
