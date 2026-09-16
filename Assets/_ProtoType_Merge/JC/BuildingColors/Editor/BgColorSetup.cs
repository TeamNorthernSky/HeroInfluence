using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.BuildingColors.EditorTools
{
    public static class BgColorSetup
    {
        public const string Base="Assets/_ProtoType_Merge/JC/BuildingColors";
        [Serializable] public class Inventory {public Row[] bindings;}
        [Serializable] public class Row {public string name,key,prefab,material,source,seed;public BgPartColor[] initial;public bool palette;}
        public static string BuildTestbed()
        {
            var scene=SceneManager.GetActiveScene();
            if(scene.path!="Assets/_ProtoType_Merge/JC/JC_TestScenes/z_JC_BuildingColorTestbed.unity"||scene.isDirty||EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("저장된 건물 테스트 씬의 편집 모드에서 실행하세요.");
            if(scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BgColorRoot>(true)).Any())throw new InvalidOperationException("조절기가 이미 존재합니다. 기존 데이터를 보존하기 위해 초기 배치를 중단합니다.");
            var inventory=JsonUtility.FromJson<Inventory>(File.ReadAllText(Base+"/Data/inventory.json"));
            if(inventory.bindings.Select(b=>b.name).Distinct().Count()!=19)throw new Exception("Expected 19 buildings");
            foreach(string folder in new[]{"Definitions","Profiles","Prefabs"})if(!AssetDatabase.IsValidFolder(Base+"/"+folder))AssetDatabase.CreateFolder(Base,folder);
            foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{Base+"/Data"})){
                var path=AssetDatabase.GUIDToAssetPath(guid);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=false;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=false;importer.mipmapEnabled=true;importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            }
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(Base+"/Shaders/BgRecolor.shader");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Shader error");
            var group=new GameObject("BG_ColorModifier");var root=group.AddComponent<BgColorRoot>();
            foreach(var model in inventory.bindings.GroupBy(r=>r.name)){
                var rows=model.ToArray();var first=rows.OrderByDescending(r=>!string.IsNullOrEmpty(r.source)).First();
                var definition=ScriptableObject.CreateInstance<BgColorDefinition>();definition.buildingId=model.Key;definition.prefab=AssetDatabase.LoadAssetAtPath<GameObject>(first.prefab);definition.recolorShader=shader;definition.initial=BgColorModifier.Copy(first.initial);
                definition.classificationNote=rows.Any(r=>r.palette)?"공유 팔레트 방식입니다. 같은 색상 칸을 사용하는 부품은 함께 바뀔 수 있습니다. 파츠 영역 보기로 범위를 확인하세요.":"색역과 메시 높이를 조합한 1차 파츠 분류입니다. 장식·차양 일부가 같은 파츠에 포함될 수 있습니다.";
                definition.bindings=rows.Select(r=>new BgTextureBinding{material=AssetDatabase.LoadAssetAtPath<Material>(r.material),original=AssetDatabase.LoadAssetAtPath<Texture2D>(r.source),source=AssetDatabase.LoadAssetAtPath<Texture2D>(r.seed),maskParts=AssetDatabase.LoadAssetAtPath<Texture2D>(Base+"/Data/"+r.key+"_parts.png"),maskFloor=AssetDatabase.LoadAssetAtPath<Texture2D>(Base+"/Data/"+r.key+"_floor.png")}).ToArray();
                AssetDatabase.CreateAsset(definition,Base+"/Definitions/"+model.Key+".asset");
                var child=new GameObject(model.Key);child.transform.SetParent(group.transform);var modifier=child.AddComponent<BgColorModifier>();modifier.definition=definition;modifier.ResetInitial();
                BgColorStorage.Create(modifier,Base+"/Profiles/"+model.Key+"_Default.asset");
            }
            var set=ScriptableObject.CreateInstance<BgColorSetProfile>();AssetDatabase.CreateAsset(set,Base+"/Profiles/BG_All_Default.asset");root.profile=set;BgColorStorage.SaveSet(root,set);
            // Portable prefab contains no scene bindings. Scene instance binds only the test gallery.
            PrefabUtility.SaveAsPrefabAsset(group,Base+"/Prefabs/BG_ColorModifier.prefab");
            group.transform.SetParent(scene.GetRootGameObjects().First(g=>g.name=="JC_Environment").transform,false);
            return CompleteLayout(root);
        }
        public static string CompleteLayout(BgColorRoot root)
        {
            var scene=SceneManager.GetActiveScene();var group=root.gameObject;
            var gallery=scene.GetRootGameObjects().First(g=>g.name=="BG Buildings - 19 Types");gallery.SetActive(true);root.targetRoot=gallery.transform;
            foreach(var name in new[]{"Display - Ground and Labels","BG Color Studies - 4 x 4","Studies - Ground and Labels"}){var old=scene.GetRootGameObjects().FirstOrDefault(g=>g.name==name);if(old)old.SetActive(false);}
            var display=GameObject.Find("BG Inspector Gallery - Ground and Labels");if(!display)display=new GameObject("BG Inspector Gallery - Ground and Labels");if(display.transform.childCount>0)throw new Exception("Display already populated");var basePath="Assets/_ProtoType_Merge/JC/JC_TestScenes/BuildingColorTestbed/";
            var padMat=AssetDatabase.LoadAssetAtPath<Material>(basePath+"MAT_DisplayPad.mat");var groundMat=AssetDatabase.LoadAssetAtPath<Material>(basePath+"MAT_DisplayGround.mat");
            var buildings=gallery.transform.Cast<Transform>().SelectMany(t=>t.Cast<Transform>()).OrderBy(t=>t.name,StringComparer.Ordinal).ToArray();if(buildings.Length!=19)throw new Exception("Existing gallery count mismatch");
            for(int i=0;i<buildings.Length;i++){
                var t=buildings[i];t.gameObject.SetActive(true);var rs=t.GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var r in rs.Skip(1))bounds.Encapsulate(r.bounds);
                float x=(i%5)*7,z=(i/5)*7;t.position+=new Vector3(x-bounds.center.x,-bounds.min.y,z-bounds.center.z);
                var pad=GameObject.CreatePrimitive(PrimitiveType.Cube);pad.name="Pad "+t.name;pad.transform.SetParent(display.transform);pad.transform.position=new Vector3(x,-.05f,z);pad.transform.localScale=new Vector3(6.4f,.1f,6.4f);pad.GetComponent<Renderer>().sharedMaterial=padMat;
                var label=new GameObject(t.name+" Label");label.transform.SetParent(display.transform);label.transform.position=new Vector3(x,.12f,z-2.75f);label.transform.rotation=Quaternion.Euler(55,0,0);var text=label.AddComponent<TextMesh>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=64;text.characterSize=.13f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.14f,.16f,.18f);text.text=root.Children.First(c=>rs.Any(r=>r.sharedMaterials.Any(m=>c.definition.bindings.Any(b=>b.material==m)))).definition.buildingId;label.GetComponent<Renderer>().sharedMaterial=text.font.material;
            }
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Gallery Ground";ground.transform.SetParent(display.transform);ground.transform.position=new Vector3(14,-.2f,10.5f);ground.transform.localScale=new Vector3(38,.2f,31);ground.GetComponent<Renderer>().sharedMaterial=groundMat;
            var camera=UnityEngine.Object.FindObjectOfType<Camera>();var center=new Vector3(14,.5f,10.5f);camera.transform.rotation=Quaternion.Euler(55,0,0);camera.fieldOfView=45;camera.orthographic=false;camera.transform.position=center-camera.transform.forward*48;GameObject.Find("Camera Orbit Target").transform.position=center;
            root.RefreshTargets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Selection.activeGameObject=group;
            if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(center,camera.transform.rotation,25,false,true);
            return "19 building controllers, 20 material bindings, individual/set profiles and portable prefab; saved testbed, Play not entered";
        }
    }
}
