using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace JC.BuildingColors.EditorTools
{
    // Explicit, repeatable data upgrade. Never run from an import callback or inspector repaint.
    public static class BgColorMigration
    {
        const string Root=BgColorSetup.Base;
        [Serializable] public sealed class Inventory {public Row[] bindings;}
        [Serializable] public sealed class Row {public string name,key,source,seed,derivedSeed;public string[] partIds,partNames;public BgPartColor[] initial;public bool palette,geometryAtlas;public int atlasColumns,atlasRows;}
        // Coverage-only upgrade. Scene drafts, saved profiles and anchors are untouched.
        public static string UpgradeRegions(string inventoryName="inventory_v3.json")
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!="Assets/_ProtoType_Merge/JC/JC_TestScenes/z_JC_BuildingColorTestbed.unity")throw new InvalidOperationException("건물 테스트 씬의 편집 모드에서 실행하세요.");
            var rows=JsonUtility.FromJson<Inventory>(File.ReadAllText(Root+"/Data/"+inventoryName)).bindings;
            foreach(var r in rows){
                foreach(string suffix in new[]{"parts","floor","extra","extra2","atlas"}){
                    string path=Root+"/Data/"+r.key+"_"+suffix+".png";
                    if(!File.Exists(path))continue;
                    var i=(TextureImporter)AssetImporter.GetAtPath(path);bool color=suffix=="atlas";int max=r.geometryAtlas?4096:2048;var filter=r.palette&&color?FilterMode.Point:FilterMode.Bilinear;
                    if(i.sRGBTexture!=color||i.alphaSource!=TextureImporterAlphaSource.FromInput||i.alphaIsTransparency||!i.mipmapEnabled||i.maxTextureSize!=max||i.textureCompression!=TextureImporterCompression.Uncompressed||i.wrapMode!=TextureWrapMode.Clamp||i.filterMode!=filter){
                        i.sRGBTexture=color;i.alphaSource=TextureImporterAlphaSource.FromInput;i.alphaIsTransparency=false;i.mipmapEnabled=true;i.maxTextureSize=max;i.textureCompression=TextureImporterCompression.Uncompressed;i.wrapMode=TextureWrapMode.Clamp;i.filterMode=filter;i.SaveAndReimport();
                    }
                }
            }
            foreach(var group in rows.GroupBy(r=>r.name)){
                var d=AssetDatabase.LoadAssetAtPath<BgColorDefinition>(Root+"/Definitions/"+group.Key+".asset");var rr=group.ToArray();var meshes=new List<BgMeshBinding>();
                for(int j=0;j<rr.Length;j++){
                    var r=rr[j];var b=d.bindings[j];b.maskParts=Tex(r.key,"parts");b.maskFloor=Tex(r.key,"floor");b.maskExtra=Tex(r.key,"extra");b.maskExtra2=Tex(r.key,"extra2");
                    if(r.geometryAtlas||r.palette){b.source=Tex(r.key,"atlas");meshes.AddRange(BuildMeshes(d,b,r.key,r.geometryAtlas?r.atlasColumns:4,r.geometryAtlas?r.atlasRows:4));}
                }
                d.meshes=meshes.ToArray();d.classificationNote="지붕·식생·바닥은 실제 메시 부품의 범위로 제한합니다. 창문 색역은 건물 외벽 안에서만 검출하며, 관련 없는 소품은 원본 색상을 유지합니다. 파츠 영역 보기로 범위를 확인하세요.";
                if(rr.Any(r=>r.geometryAtlas))d.classificationNote+=" 서로 다른 파츠가 공유하던 UV도 분리했습니다.";
                if(d.buildingId=="BGFactory002")d.classificationNote+=" 원본 모델에서도 옥상 일부가 위에서 보이지 않는 면 문제가 있습니다.";
                EditorUtility.SetDirty(d);AssetDatabase.SaveAssetIfDirty(d);
            }
            var root=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BgColorRoot>(true)).Single();
            foreach(var c in root.Children)c.InvalidatePreview();root.RefreshTargets();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);SceneView.RepaintAll();
            return "Updated 19 building regions; drafts, profiles and anchors preserved.";
        }
        public static string UpgradeAccents()
        {
            UpgradeRegions("inventory_v4.json");
            var rows=JsonUtility.FromJson<Inventory>(File.ReadAllText(Root+"/Data/inventory_v4.json")).bindings;
            var defs=new Dictionary<string,BgColorDefinition>();
            foreach(var group in rows.GroupBy(r=>r.name)){
                var row=group.OrderByDescending(r=>r.partIds.Length).First();var d=AssetDatabase.LoadAssetAtPath<BgColorDefinition>(Root+"/Definitions/"+group.Key+".asset");
                var oldIds=(string[])d.Ids.Clone();var oldDefaults=BgColorModifier.Copy(d.Defaults);
                d.initial=BgColorModifier.Copy(row.initial);d.partIds=row.partIds;d.partNames=row.partNames;d.defaults=BgColorModifier.Copy(d.initial);d.defaults=d.Remap(oldDefaults,oldIds);
                d.classificationNote+=Array.IndexOf(d.Ids,"accent")>=0?" 보조 도장·소품은 기존 파츠 밖의 올리브색 도장과 소품입니다. 공유 팔레트 건물의 올리브색 하단·층간 띠도 이 항목에서 조절합니다.":" 이 건물에는 기존 파츠 밖의 추가 올리브색 영역이 없습니다.";
                EditorUtility.SetDirty(d);AssetDatabase.SaveAssetIfDirty(d);defs.Add(d.buildingId,d);
            }
            foreach(var guid in AssetDatabase.FindAssets("t:BgColorProfile",new[]{Root+"/Profiles"})){
                var p=AssetDatabase.LoadAssetAtPath<BgColorProfile>(AssetDatabase.GUIDToAssetPath(guid));if(!defs.TryGetValue(p.buildingId,out var d))continue;
                p.parts=d.Remap(p.parts,p.partIds);p.partIds=(string[])d.Ids.Clone();EditorUtility.SetDirty(p);AssetDatabase.SaveAssetIfDirty(p);
            }
            foreach(var guid in AssetDatabase.FindAssets("t:BgColorSetProfile",new[]{Root+"/Profiles"})){
                var p=AssetDatabase.LoadAssetAtPath<BgColorSetProfile>(AssetDatabase.GUIDToAssetPath(guid));
                foreach(var e in p.buildings??Array.Empty<BgColorSetEntry>())if(defs.TryGetValue(e.buildingId,out var d)){e.parts=d.Remap(e.parts,e.partIds);e.partIds=(string[])d.Ids.Clone();}
                EditorUtility.SetDirty(p);AssetDatabase.SaveAssetIfDirty(p);
            }
            string path=Root+"/Prefabs/BG_ColorModifier.prefab";var prefab=PrefabUtility.LoadPrefabContents(path);
            try{foreach(var c in prefab.GetComponentsInChildren<BgColorModifier>(true))c.EnsureParts();PrefabUtility.SaveAsPrefabAsset(prefab,path);}finally{PrefabUtility.UnloadPrefabContents(prefab);}
            var root=UnityEngine.Object.FindObjectOfType<BgColorRoot>();foreach(var c in root.Children){c.EnsureParts();c.InvalidatePreview();EditorUtility.SetDirty(c);}root.RefreshTargets();EditorSceneManager.MarkSceneDirty(root.gameObject.scene);EditorSceneManager.SaveScene(root.gameObject.scene);SceneView.RepaintAll();
            int accents=defs.Values.Count(d=>Array.IndexOf(d.Ids,"accent")>=0);
            return $"Added independent accent control to {accents} buildings; existing IDs/values retained in drafts, individual profiles, set profiles and portable prefab.";
        }
        // Shop-specific semantic correction. Capture drafts before changing the
        // definitions: ExecuteAlways may otherwise remap them during reimport.
        public static string UpgradeStoreParts()
        {
            return UpgradeSelectedParts("inventory_v5.json",new[]{"BGStore001","BGStore003"});
        }
        public static string UpgradeStore45Parts()
        {
            return UpgradeSelectedParts("inventory_v6.json",new[]{"BGStore004","BGStore005"});
        }
        public static string UpgradeHighParts()
        {
            return UpgradeSelectedParts("inventory_v8.json",new[]{"BGHigh001","BGHigh002","BGHigh003"});
        }
        public static string UpgradeHigh001UpperParts()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!="Assets/_ProtoType_Merge/JC/JC_TestScenes/z_JC_BuildingColorTestbed.unity")throw new InvalidOperationException("건물 테스트 씬의 편집 모드에서 실행하세요.");
            var row=JsonUtility.FromJson<Inventory>(File.ReadAllText(Root+"/Data/inventory_v9.json")).bindings.Single(r=>r.name=="BGHigh001");
            var d=AssetDatabase.LoadAssetAtPath<BgColorDefinition>(Root+"/Definitions/BGHigh001.asset");
            d.meshes=BuildMeshes(d,d.bindings[0],row.key).ToArray();
            int floor=Array.IndexOf(d.Ids,"roof_floor");d.initial[floor]=row.initial[Array.IndexOf(row.partIds,"roof_floor")];
            d.classificationNote="상단 유리 띠는 창문, 주변 벽과 상부 베이지 블록은 벽으로 조절합니다. 하부 옥상 단은 지붕·차양, 가장 높은 평면만 옥상 바닥면입니다. 옥상 바닥의 기준색은 실제 원본 표면에서 추출했습니다. 정문 손잡이는 원본 색상을 유지합니다.";
            EditorUtility.SetDirty(d);AssetDatabase.SaveAssetIfDirty(d);
            var root=UnityEngine.Object.FindObjectOfType<BgColorRoot>();foreach(var c in root.Children.Where(c=>c.definition==d))c.InvalidatePreview();root.RefreshTargets();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);SceneView.RepaintAll();
            return "High001 upper regions and roof-floor source anchor updated; target colors and saved profiles unchanged.";
        }
        public static string UpgradeUvRepairs()
        {
            return UpgradeSelectedParts("inventory_v10.json",new[]{"BGStore002","BGOffice002","BGOffice001","BGRowHouse002","BGFactory002"});
        }
        public static string UpgradeHigh001WallExclusions()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("편집 모드에서 실행하세요.");
            var d=AssetDatabase.LoadAssetAtPath<BgColorDefinition>(Root+"/Definitions/BGHigh001.asset");
            d.meshes=BuildMeshes(d,d.bindings[0],"BGHigh001_2").ToArray();
            d.classificationNote="벽은 건물 외벽만 조절합니다. 안테나의 회색 받침·봉·노란 끝 장식, 현관문·문틀·노란 포인트와 흰 창틀은 원본 색상을 유지합니다. 안테나 아래 낮은 바닥판과 하부 옥상 단은 지붕·차양, 가장 높은 옥상 평면은 옥상 바닥면입니다.";
            EditorUtility.SetDirty(d);AssetDatabase.SaveAssetIfDirty(d);
            foreach(var c in Resources.FindObjectsOfTypeAll<BgColorModifier>())if(c.definition==d&&c.gameObject.scene.IsValid()&&!EditorUtility.IsPersistent(c)){c.InvalidatePreview();c.Rebind();c.ApplyNow();}
            SceneView.RepaintAll();
            return "High001 wall and roof exclusions updated; colors, toggles, profiles and unsaved scene state retained.";
        }
        public static string UpgradeRowHouseCentralBlock()
        {
            return UpgradeSelectedParts("inventory_v12.json",new[]{"BGRowHouse002"},false);
        }
        public static string UpgradeRims()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!="Assets/_ProtoType_Merge/JC/JC_TestScenes/z_JC_BuildingColorTestbed.unity")throw new InvalidOperationException("건물 테스트 씬의 편집 모드에서 실행하세요.");
            var rows=JsonUtility.FromJson<Inventory>(File.ReadAllText(Root+"/Data/inventory_v7.json")).bindings;
            foreach(var group in rows.Where(r=>Array.IndexOf(r.partIds,"roof_floor")>=0).GroupBy(r=>r.name)){
                var d=AssetDatabase.LoadAssetAtPath<BgColorDefinition>(Root+"/Definitions/"+group.Key+".asset");var rr=rows.Where(r=>r.name==group.Key).ToArray();var meshes=new List<BgMeshBinding>();
                for(int j=0;j<rr.Length;j++){
                    var r=rr[j];var b=d.bindings[j];
                    foreach(string suffix in new[]{"parts","floor","extra","extra2","atlas"}){
                        string path=Root+"/Data/"+r.key+"_"+suffix+".png";if(!File.Exists(path))continue;
                        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                        var importer=(TextureImporter)AssetImporter.GetAtPath(path);int max=r.geometryAtlas?4096:2048;
                        if(importer.maxTextureSize!=max){importer.maxTextureSize=max;importer.SaveAndReimport();}
                    }
                    if(r.palette||r.geometryAtlas){b.source=Tex(r.key,"atlas");meshes.AddRange(BuildMeshes(d,b,r.key,r.geometryAtlas?r.atlasColumns:4,r.geometryAtlas?r.atlasRows:4));}
                }
                d.meshes=meshes.ToArray();
                if(!d.classificationNote.Contains("옥상 테두리 전체"))d.classificationNote+=" 옥상 테두리 전체(윗면·안쪽 면 포함)는 지붕·차양으로 조절합니다.";
                if(d.buildingId=="BGRowHouse002"){
                    d.partNames[Array.IndexOf(d.Ids,"accent")]="보조도장";
                    d.classificationNote="창문은 유리 면 전체를 조절합니다. 보조도장은 중앙 세로 도장면과 하단·창틀의 올리브색입니다. 현관 램프와 문손잡이·옥상 설비는 원본 색상을 유지합니다. 옥상 테두리 전체(윗면·안쪽 면 포함)는 지붕·차양으로 조절합니다.";
                }
                EditorUtility.SetDirty(d);AssetDatabase.SaveAssetIfDirty(d);
            }
            var root=UnityEngine.Object.FindObjectOfType<BgColorRoot>();foreach(var c in root.Children)c.InvalidatePreview();root.RefreshTargets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);SceneView.RepaintAll();
            return "Parapets and RowHouse002 coverage updated; all draft/profile values retained.";
        }
        static string UpgradeSelectedParts(string inventoryName,string[] buildings,bool saveScene=true)
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!="Assets/_ProtoType_Merge/JC/JC_TestScenes/z_JC_BuildingColorTestbed.unity")throw new InvalidOperationException("건물 테스트 씬의 편집 모드에서 실행하세요.");
            var root=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BgColorRoot>(true)).Single();
            var rows=JsonUtility.FromJson<Inventory>(File.ReadAllText(Root+"/Data/"+inventoryName)).bindings.Where(r=>buildings.Contains(r.name)).ToArray();
            var drafts=root.Children.Where(c=>rows.Any(r=>r.name==c.definition.buildingId)).ToDictionary(c=>c.definition.buildingId,c=>StoreEntry(c));
            string prefabPath=Root+"/Prefabs/BG_ColorModifier.prefab";
            var prefab=PrefabUtility.LoadPrefabContents(prefabPath);Dictionary<string,BgColorSetEntry> prefabDrafts;
            try{prefabDrafts=prefab.GetComponentsInChildren<BgColorModifier>(true).Where(c=>drafts.ContainsKey(c.definition.buildingId)).ToDictionary(c=>c.definition.buildingId,c=>StoreEntry(c));}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            var defs=new Dictionary<string,BgColorDefinition>();
            foreach(var group in rows.GroupBy(r=>r.name)){
                var rr=group.ToArray();var row=rr.OrderByDescending(r=>r.partIds.Length).First();
                var d=AssetDatabase.LoadAssetAtPath<BgColorDefinition>(Root+"/Definitions/"+group.Key+".asset");
                var oldIds=(string[])d.Ids.Clone();var oldDefaults=BgColorModifier.Copy(d.Defaults);
                d.initial=BgColorModifier.Copy(row.initial);d.partIds=(string[])row.partIds.Clone();d.partNames=(string[])row.partNames.Clone();d.defaults=BgColorModifier.Copy(d.initial);
                d.defaults=RemapStoreParts(oldDefaults,oldIds,d);
                if(d.buildingId=="BGOffice001"&&Array.IndexOf(oldIds,"roof_trim")<0)d.defaults[Array.IndexOf(d.Ids,"roof_trim")]=new BgPartColor{hue=0,lightness=1,saturation=0};
                for(int j=0;j<rr.Length;j++){
                    var r=rr[j];var b=d.bindings[j];
                    foreach(string suffix in new[]{"parts","floor","extra","extra2","atlas"}){
                        string path=Root+"/Data/"+r.key+"_"+suffix+".png";
                        if(!File.Exists(path))continue;
                        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                        var importer=(TextureImporter)AssetImporter.GetAtPath(path);bool color=suffix=="atlas";int max=r.geometryAtlas?4096:2048;
                        importer.sRGBTexture=color;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=false;importer.mipmapEnabled=true;importer.maxTextureSize=max;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=r.palette&&color?FilterMode.Point:FilterMode.Bilinear;importer.SaveAndReimport();
                    }
                    b.maskParts=Tex(r.key,"parts");b.maskFloor=Tex(r.key,"floor");b.maskExtra=Tex(r.key,"extra");b.maskExtra2=Tex(r.key,"extra2");
                    if(r.palette||r.geometryAtlas){b.source=Tex(r.key,"atlas");d.meshes=BuildMeshes(d,b,r.key,r.geometryAtlas?r.atlasColumns:4,r.geometryAtlas?r.atlasRows:4).ToArray();}
                    else if(r.name=="BGFactory002")d.meshes=BuildMeshes(d,b,r.key,1,1).ToArray();
                }
                d.classificationNote=d.buildingId=="BGStore001"?
                    "간판은 지붕·차양과 별도로 조절합니다. 파츠 영역 보기로 조절 범위를 확인하세요.":
                    "보조도장: 창 아래 하단 띠와 문 주변의 올리브색 도장면. 간판과 햄버거는 각각 조절합니다. 보조도장 2: 진입차단봉 금속 포인트·측면 화단 테두리·기존 소품 포인트. 화분 몸체와 토양은 기존 항목을 사용합니다.";
                if(d.buildingId=="BGStore004")d.classificationNote="보조도장: 건물 하단 띠와 문·창 주변의 올리브색 도장면. 우체통과 적재 박스는 각각 조절합니다. 상단 간판 띠·상호 표기·진입차단봉에는 보조도장이 적용되지 않습니다.";
                if(d.buildingId=="BGStore005")d.classificationNote="상단 간판·측면 입간판·측면 출입구를 각각 조절합니다. 경고 도장은 안전봉·보호대·리프트 받침·전면 연석의 노란 부분이며 검은 줄무늬는 유지합니다. 이 영역들은 보조도장·소품에서 제외됩니다.";
                if(d.buildingId=="BGHigh001")d.classificationNote="층간 테두리·상단 모서리·안테나 받침은 지붕·차양으로 함께 조절합니다. 상부 단과 최상단 옥상면은 옥상 바닥면으로 조절합니다. 정문 손잡이는 원본 색상을 유지합니다.";
                if(d.buildingId=="BGHigh002")d.classificationNote="보조도장: 중앙 현관 차양과 건물 하단 테두리. 현관의 두 램프는 모든 팔레트 편집에서 제외하고 원본 색상을 유지합니다.";
                if(d.buildingId=="BGHigh003")d.classificationNote="보조도장: 현관 차양과 건물 하단 테두리. 식생 줄기는 모든 팔레트 편집에서 제외하고 원본 색상을 유지합니다.";
                if(d.buildingId=="BGOffice001")d.classificationNote="흰 옥상 테두리를 옥상 테두리 항목에서 별도로 조절합니다. 원본 표면의 기준색을 사용하며 지붕·차양, 옥상 바닥과 독립적입니다.";
                if(d.buildingId=="BGOffice002"||d.buildingId=="BGStore002")d.classificationNote="보조도장은 건물 하단 테두리입니다. 이전 보조도장 소품 영역은 원본 색상을 유지합니다. 하단 테두리는 별도 UV 페이지로 분리했습니다.";
                if(d.buildingId=="BGRowHouse002")d.classificationNote="창문·벽·중앙 보조도장·하단 띠를 면 단위로 구분하고 전용 UV 여백을 확보했습니다. 현관 램프·손잡이·옥상 설비는 원본 색상을 유지합니다. 건물 형상은 변경하지 않습니다.";
                if(d.buildingId=="BGRowHouse002"&&Array.IndexOf(d.Ids,"central_block")>=0)d.classificationNote="중앙 블록은 정면 세로 돌출부의 앞면·양쪽 측면·윗부분을 함께 조절합니다. 블록 안의 유리는 창문, 아래 현관 차양은 지붕·차양, 건물 하단 띠는 보조도장입니다. 현관 램프·손잡이·옥상 설비는 원본 색상을 유지합니다.";
                if(d.buildingId=="BGFactory002")d.classificationNote="편집용 복제 메시에서 아래로 뒤집혔던 천장 6면의 방향과 법선을 교정했습니다. 옥상 바닥면으로 색상을 조절합니다. 원본 비교에서는 원본 모델의 결함도 그대로 표시됩니다.";
                EditorUtility.SetDirty(d);AssetDatabase.SaveAssetIfDirty(d);defs.Add(d.buildingId,d);
            }
            foreach(var guid in AssetDatabase.FindAssets("t:BgColorProfile",new[]{Root+"/Profiles"})){
                var p=AssetDatabase.LoadAssetAtPath<BgColorProfile>(AssetDatabase.GUIDToAssetPath(guid));if(!defs.TryGetValue(p.buildingId,out var d))continue;
                p.parts=RemapStoreParts(p.parts,p.partIds,d);p.partIds=(string[])d.Ids.Clone();EditorUtility.SetDirty(p);AssetDatabase.SaveAssetIfDirty(p);
            }
            foreach(var guid in AssetDatabase.FindAssets("t:BgColorSetProfile",new[]{Root+"/Profiles"})){
                var p=AssetDatabase.LoadAssetAtPath<BgColorSetProfile>(AssetDatabase.GUIDToAssetPath(guid));bool changed=false;
                foreach(var e in p.buildings??Array.Empty<BgColorSetEntry>())if(defs.TryGetValue(e.buildingId,out var d)){
                    e.parts=RemapStoreParts(e.parts,e.partIds,d);e.partIds=(string[])d.Ids.Clone();changed=true;
                }
                if(changed){EditorUtility.SetDirty(p);AssetDatabase.SaveAssetIfDirty(p);}
            }
            prefab=PrefabUtility.LoadPrefabContents(prefabPath);
            try{foreach(var c in prefab.GetComponentsInChildren<BgColorModifier>(true))if(prefabDrafts.TryGetValue(c.definition.buildingId,out var before)){
                c.parts=RemapStoreParts(before.parts,before.partIds,c.definition);c.partIds=(string[])c.definition.Ids.Clone();EditorUtility.SetDirty(c);
            }PrefabUtility.SaveAsPrefabAsset(prefab,prefabPath);}finally{PrefabUtility.UnloadPrefabContents(prefab);}
            foreach(var c in root.Children)if(drafts.TryGetValue(c.definition.buildingId,out var before)){
                c.parts=RemapStoreParts(before.parts,before.partIds,c.definition);c.partIds=(string[])c.definition.Ids.Clone();c.InvalidatePreview();EditorUtility.SetDirty(c);
            }
            root.RefreshTargets();EditorSceneManager.MarkSceneDirty(scene);if(saveScene)EditorSceneManager.SaveScene(scene);SceneView.RepaintAll();
            return string.Join(", ",buildings)+": selected parts updated; scene/profile/prefab values migrated independently.";
        }
        static BgColorSetEntry StoreEntry(BgColorModifier c)=>new BgColorSetEntry{buildingId=c.definition.buildingId,parts=BgColorModifier.Copy(c.parts),partIds=(string[])c.partIds.Clone()};
        public static BgPartColor[] RemapStoreParts(BgPartColor[] values,string[] ids,BgColorDefinition d)
        {
            var mapped=d.Remap(values,ids);
            if(d.buildingId=="BGStore001"&&Array.IndexOf(ids,"sign_face")<0)mapped[Array.IndexOf(d.Ids,"sign_face")]=values[Array.IndexOf(ids,"roof")];
            if(d.buildingId=="BGStore003"&&Array.IndexOf(ids,"sign_symbol")<0){
                var old=values[Array.IndexOf(ids,"accent")];
                foreach(string id in new[]{"sign_face","sign_symbol","accent2"})mapped[Array.IndexOf(d.Ids,id)]=old;
                // The old accent never covered the olive wall band. Start that
                // newly exposed surface at its own original reference color.
                int accent=Array.IndexOf(d.Ids,"accent");mapped[accent]=d.initial[accent];
            }
            if(d.buildingId=="BGStore004"&&Array.IndexOf(ids,"mailbox")<0){
                var old=values[Array.IndexOf(ids,"accent")];foreach(string id in new[]{"mailbox","crates"})mapped[Array.IndexOf(d.Ids,id)]=old;
                int accent=Array.IndexOf(d.Ids,"accent");mapped[accent]=d.initial[accent];
            }
            if(d.buildingId=="BGStore005"&&Array.IndexOf(ids,"warning_paint")<0){
                var old=values[Array.IndexOf(ids,"accent")];foreach(string id in new[]{"sign_face","standing_sign","side_entrance"})mapped[Array.IndexOf(d.Ids,id)]=old;
                int warning=Array.IndexOf(d.Ids,"warning_paint");mapped[warning]=d.initial[warning];
            }
            return mapped;
        }
        public static string Upgrade()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!="Assets/_ProtoType_Merge/JC/JC_TestScenes/z_JC_BuildingColorTestbed.unity")throw new InvalidOperationException("건물 테스트 씬의 편집 모드에서 실행하세요.");
            var root=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<BgColorRoot>(true)).Single();
            var rows=JsonUtility.FromJson<Inventory>(File.ReadAllText(Root+"/Data/inventory_v2.json")).bindings;
            var defs=new Dictionary<string,BgColorDefinition>();
            var previousDefaults=new Dictionary<string,Dictionary<string,BgPartColor>>();
            if(!AssetDatabase.IsValidFolder(Root+"/Meshes"))AssetDatabase.CreateFolder(Root,"Meshes");
            foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{Root+"/Data"})){
                string path=AssetDatabase.GUIDToAssetPath(guid);bool atlas=(path.EndsWith("_atlas.png")||path.EndsWith("_seed.png"));
                var i=(TextureImporter)AssetImporter.GetAtPath(path);i.sRGBTexture=atlas;i.alphaSource=TextureImporterAlphaSource.FromInput;i.alphaIsTransparency=false;i.mipmapEnabled=true;i.maxTextureSize=2048;i.textureCompression=TextureImporterCompression.Uncompressed;i.wrapMode=TextureWrapMode.Clamp;i.filterMode=atlas?FilterMode.Point:FilterMode.Bilinear;i.SaveAndReimport();
            }
            foreach(var group in rows.GroupBy(r=>r.name)){
                var rr=group.ToArray();var row=rr.OrderByDescending(r=>r.partIds.Length).First();var d=AssetDatabase.LoadAssetAtPath<BgColorDefinition>(Root+"/Definitions/"+group.Key+".asset");if(!d)throw new Exception(group.Key);
                previousDefaults[d.buildingId]=Enumerable.Range(0,d.Ids.Length).ToDictionary(i=>d.Ids[i],i=>d.Defaults[i]);
                // Existing anchors stay byte-for-byte unchanged; added roles have independent anchors.
                d.initial=BgColorModifier.Copy(row.initial);d.partIds=row.partIds;d.partNames=row.partNames;d.defaults=BgColorModifier.Copy(d.initial);
                int roof=Array.IndexOf(d.partIds,"roof_floor");if(roof>=0)d.defaults[roof].saturation=0;
                var meshes=new List<BgMeshBinding>();
                for(int j=0;j<rr.Length;j++){
                    var r=rr[j];var b=d.bindings[j];b.maskParts=Tex(r.key,"parts");b.maskFloor=Tex(r.key,"floor");b.maskExtra=Tex(r.key,"extra");b.maskExtra2=Tex(r.key,"extra2");
                    if(!string.IsNullOrEmpty(r.derivedSeed))b.source=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Data/"+r.derivedSeed);
                    if(r.palette){b.source=Tex(r.key,"atlas");meshes.AddRange(BuildMeshes(d,b,r.key));}
                }
                d.meshes=meshes.ToArray();d.classificationNote="메시 부품과 UV 경계를 기준으로 옥상 바닥·차양·화분·토양을 분리했습니다. 파츠 영역 보기로 범위를 확인할 수 있습니다. 옥상 바닥의 초기 채도는 0입니다.";
                if(roof<0)d.classificationNote="파츠 영역 보기로 조절 범위를 확인할 수 있습니다. 경사진 지붕은 지붕·차양 항목에서 조절합니다.";
                if(d.buildingId=="BGFactory002")d.classificationNote+=" 원본 모델에서도 옥상 일부가 위에서 보이지 않는 면 문제가 있습니다. 색상 분리와 별도의 모델 상태입니다.";
                EditorUtility.SetDirty(d);AssetDatabase.SaveAssetIfDirty(d);defs.Add(d.buildingId,d);
            }
            int profiles=0,sets=0;
            foreach(var guid in AssetDatabase.FindAssets("t:BgColorProfile")){
                var p=AssetDatabase.LoadAssetAtPath<BgColorProfile>(AssetDatabase.GUIDToAssetPath(guid));if(!defs.TryGetValue(p.buildingId,out var d))continue;
                if(!BgColorDefinition.ValidValues(p.parts,p.partIds))throw new Exception("잘못된 프로필: "+p.name);
                p.parts=Remap(p.parts,p.partIds,d,previousDefaults[d.buildingId]);p.partIds=(string[])d.Ids.Clone();EditorUtility.SetDirty(p);AssetDatabase.SaveAssetIfDirty(p);profiles++;
            }
            foreach(var guid in AssetDatabase.FindAssets("t:BgColorSetProfile")){
                var p=AssetDatabase.LoadAssetAtPath<BgColorSetProfile>(AssetDatabase.GUIDToAssetPath(guid));bool changed=false;
                foreach(var e in p.buildings??Array.Empty<BgColorSetEntry>())if(defs.TryGetValue(e.buildingId,out var d)){
                    if(!BgColorDefinition.ValidValues(e.parts,e.partIds))throw new Exception("잘못된 전체 프로필: "+p.name);
                    e.parts=Remap(e.parts,e.partIds,d,previousDefaults[d.buildingId]);e.partIds=(string[])d.Ids.Clone();changed=true;
                }
                if(changed){EditorUtility.SetDirty(p);AssetDatabase.SaveAssetIfDirty(p);sets++;}
            }
            foreach(var c in root.Children){c.parts=Remap(c.parts,c.partIds,c.definition,previousDefaults[c.definition.buildingId]);c.EnsureParts();c.InvalidatePreview();EditorUtility.SetDirty(c);}
            // Upgrade the portable prefab from its own values, never from the scene's unsaved draft.
            string prefabPath=Root+"/Prefabs/BG_ColorModifier.prefab";var prefab=PrefabUtility.LoadPrefabContents(prefabPath);
            try{foreach(var c in prefab.GetComponentsInChildren<BgColorModifier>(true)){c.parts=Remap(c.parts,c.partIds,c.definition,previousDefaults[c.definition.buildingId]);c.EnsureParts();}PrefabUtility.SaveAsPrefabAsset(prefab,prefabPath);}finally{PrefabUtility.UnloadPrefabContents(prefab);}
            root.RefreshTargets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);SceneView.RepaintAll();
            return $"Updated {defs.Count} definitions, {profiles} independent profiles, {sets} collections, {root.Children.Length} scene drafts; original files unchanged.";
        }
        static BgPartColor[] Remap(BgPartColor[] values,string[] ids,BgColorDefinition d,Dictionary<string,BgPartColor> oldDefaults)
        {
            var mapped=d.Remap(values,ids);var sourceIds=ids!=null&&ids.Length==values.Length?ids:BgColorDefinition.LegacyIds;
            for(int i=5;i<d.Ids.Length;i++){int source=Array.IndexOf(sourceIds,d.Ids[i]);if(source<0||source>=values.Length||!oldDefaults.TryGetValue(d.Ids[i],out var old))continue;var p=values[source];if(p.hue==old.hue&&p.lightness==old.lightness&&p.saturation==old.saturation&&p.ignoreSourceColorAndShading==old.ignoreSourceColorAndShading)mapped[i]=d.Defaults[i];}
            return mapped;
        }
        static Texture2D Tex(string key,string suffix)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Data/"+key+"_"+suffix+".png");
        [Serializable] sealed class MeshRepair {public int[] flipTriangles;}
        static Vector2[][] ReadUvOverrides(string key)
        {
            string path=Root+"/Data/"+key+"_uv.bytes";if(!File.Exists(path))return null;
            using(var reader=new BinaryReader(File.OpenRead(path))){var result=new Vector2[reader.ReadInt32()][];
                for(int i=0;i<result.Length;i++){result[i]=new Vector2[reader.ReadInt32()];for(int j=0;j<result[i].Length;j++)result[i][j]=new Vector2(reader.ReadSingle(),reader.ReadSingle());}
                if(reader.BaseStream.Position!=reader.BaseStream.Length)throw new Exception("UV 데이터 길이 불일치: "+key);return result;
            }
        }
        static IEnumerable<BgMeshBinding> BuildMeshes(BgColorDefinition d,BgTextureBinding binding,string key,int columns=4,int rows=4)
        {
            var result=new List<BgMeshBinding>();int number=0;var customUvs=ReadUvOverrides(key);
            string repairPath=Root+"/Data/"+key+"_repair.json";var flips=File.Exists(repairPath)?new HashSet<int>(JsonUtility.FromJson<MeshRepair>(File.ReadAllText(repairPath)).flipTriangles):new HashSet<int>();
            using(var rd=new BinaryReader(File.OpenRead(Root+"/Data/"+key+"_roles.bytes"))){int expected=rd.ReadInt32();
                foreach(var renderer in d.prefab.GetComponentsInChildren<MeshRenderer>(true)){
                    if(!renderer.sharedMaterials.Contains(binding.material))continue;var source=renderer.GetComponent<MeshFilter>().sharedMesh;
                    var vertices=source.vertices;var normals=source.normals;var tangents=source.tangents;var colors=source.colors;var channels=new List<Vector4>[8];for(int k=0;k<8;k++){channels[k]=new List<Vector4>();source.GetUVs(k,channels[k]);}
                    var map=new List<int>();var roles=new List<int>();var indices=new List<int[]>();var flipNormals=new List<bool>();var mappedUvs=new List<Vector2>();
                    for(int sub=0;sub<source.subMeshCount;sub++){
                        var tris=source.GetTriangles(sub);int[] semantic=null;Vector2[] uvOverride=null;
                        if(sub<renderer.sharedMaterials.Length&&renderer.sharedMaterials[sub]==binding.material){int count=rd.ReadInt32();if(count*3!=tris.Length)throw new Exception("메시 삼각형 순서 불일치: "+key);semantic=new int[count];for(int k=0;k<count;k++)semantic[k]=rd.ReadInt32();if(customUvs!=null){uvOverride=customUvs[number];if(uvOverride.Length!=tris.Length)throw new Exception("UV 정점 수 불일치: "+key);}number++;}
                        int offset=map.Count;for(int k=0;k<tris.Length;k++){map.Add(tris[k]);roles.Add(semantic==null?15:semantic[k/3]);flipNormals.Add(semantic!=null&&flips.Contains(k/3));mappedUvs.Add(uvOverride!=null?uvOverride[k]:(Vector2)channels[0][tris[k]]);}
                        var mapped=Enumerable.Range(offset,tris.Length).ToArray();for(int k=0;k<tris.Length;k+=3)if(flipNormals[offset+k]){int temp=mapped[k+1];mapped[k+1]=mapped[k+2];mapped[k+2]=temp;}indices.Add(mapped);
                    }
                    var mesh=new Mesh{name=source.name+"_BGPartUV",indexFormat=map.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=map.Select(v=>vertices[v]).ToArray();
                    if(normals.Length==vertices.Length)mesh.normals=map.Select((v,i)=>flipNormals[i]?-normals[v]:normals[v]).ToArray();if(tangents.Length==vertices.Length)mesh.tangents=map.Select((v,i)=>{var q=tangents[v];if(flipNormals[i])q.w=-q.w;return q;}).ToArray();if(colors.Length==vertices.Length)mesh.colors=map.Select(v=>colors[v]).ToArray();
                    for(int k=0;k<8;k++)if(channels[k].Count==vertices.Length){var uv=map.Select(v=>channels[k][v]).ToList();if(k==0)for(int v=0;v<uv.Count;v++){var q=uv[v];if(customUvs!=null){q.x=mappedUvs[v].x;q.y=mappedUvs[v].y;}else{q.x=(Mathf.Clamp01(q.x)+roles[v]%columns)/columns;q.y=(Mathf.Clamp01(q.y)+roles[v]/columns)/rows;}uv[v]=q;}mesh.SetUVs(k,uv);}
                    mesh.subMeshCount=indices.Count;for(int sub=0;sub<indices.Count;sub++)mesh.SetTriangles(indices[sub],sub,false);mesh.bounds=source.bounds;
                    string path=Root+"/Meshes/"+key+"_"+result.Count+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(existing){// Mesh setters invalidate the native vertex buffers; CopySerialized alone leaves stale UVs in the renderer.
                    existing.Clear();existing.indexFormat=mesh.indexFormat;existing.vertices=mesh.vertices;existing.normals=mesh.normals;existing.tangents=mesh.tangents;existing.colors=mesh.colors;
                    for(int channel=0;channel<8;channel++){var values=new List<Vector4>();mesh.GetUVs(channel,values);if(values.Count>0)existing.SetUVs(channel,values);}
                    existing.subMeshCount=mesh.subMeshCount;for(int sub=0;sub<mesh.subMeshCount;sub++)existing.SetTriangles(mesh.GetTriangles(sub),sub,false);existing.bounds=mesh.bounds;
                    UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;EditorUtility.SetDirty(mesh);}else AssetDatabase.CreateAsset(mesh,path);AssetDatabase.SaveAssetIfDirty(mesh);result.Add(new BgMeshBinding{original=source,separated=mesh});
                }
                if(number!=expected||rd.BaseStream.Position!=rd.BaseStream.Length)throw new Exception("역할 데이터 불일치: "+key);
            }
            return result;
        }
    }
}
