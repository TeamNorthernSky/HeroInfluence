using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace JC.BattleTesting.Editor
{
    public static partial class JcEncounterBackgroundBuilder
    {
        [MenuItem("JC/전투 테스트/동적 배경 공통 입력 수집")]
        public static void BuildDynamicSource()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("공통 입력 수집은 편집 모드에서 실행해 주세요.");
            var scene=SceneManager.GetSceneByPath(SourceScene);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(!opened&&scene.isDirty)throw new InvalidOperationException("탐사씬의 미저장 변경을 먼저 확인해 주세요.");
            JcDynamicBattleSource captured=null;
            try
            {
                if(opened)scene=EditorSceneManager.OpenScene(SourceScene,OpenSceneMode.Additive);
                var overlay=EnvironmentDetail.JcExploreDetailOverlay.Ensure(scene);
                if(overlay!=null)
                {
                    if(overlay.IsPreview)throw new InvalidOperationException("탐사 드레싱 미리보기를 먼저 확정하거나 취소해 주세요.");
                    // 입력 수집은 저장된 베이크만 사용하며 탐사 외형을 재생성하지 않습니다.
                    overlay.Refresh();
                }
                captured=JcDynamicBattleGeometry.Capture(scene);
                string folder=Folder+"/DynamicSourceData";EnsureFolder(folder);var context=new ExportContext(folder);
                for(int i=0;i<captured.parts.Length;i++)
                {
                    var part=captured.parts[i];string id=Hash128.Compute(GetStablePath(part.renderer.transform)).ToString().Substring(0,12);
                    part.originalMesh=EditorUtility.IsPersistent(part.mesh)?part.mesh:null;
                    part.mesh=context.Mesh(part.mesh,id);part.materials=context.Materials(part.renderer,id);
                }
                var result=LoadOrCreate<JcDynamicBattleSource>(Folder+"/DynamicSource.asset");
                result.parts=captured.parts;result.roads=captured.roads;result.sourceScene=SourceScene;
                // 도로는 수집된 보강 재질을 그대로 사용합니다.
                foreach(var part in result.parts)foreach(var material in part.materials)if(material!=null&&material.HasProperty("_BaseMap")&&material.GetTexture("_BaseMap")!=null&&material.GetTexture("_BaseMap").name=="Road_01")result.roadMaterial=material;
                if(result.roadMaterial==null)throw new InvalidOperationException("공통 도로 재질이 없습니다.");
                EnsureDynamicReadable(result);
                EditorUtility.SetDirty(result);AssetDatabase.SaveAssetIfDirty(result);
                var preset=LoadOrCreate<JcEnemyPreset>(Folder+"/Arnold_Dynamic.asset");preset.encounterKey="Arnold_Dynamic";preset.eventBattleKey="BE161";preset.eventZoneId=1;preset.dynamicBackground=true;preset.enemyLevel=2;
                EditorUtility.SetDirty(preset);AssetDatabase.SaveAssetIfDirty(preset);
                Debug.Log($"[JC 동적 배경] 공통 외형 {result.parts.Length}, 도로 {result.roads.Length}. 위치별 완성 배경은 생성하지 않았습니다.");
            }
            finally{if(captured!=null)Object.DestroyImmediate(captured);if(opened&&scene.IsValid())EditorSceneManager.CloseScene(scene,true);}
        }
        [MenuItem("JC/전투 테스트/튜토리얼 배경 외형 캐시 갱신")]
        public static void BuildTutorialReadableCache()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("편집 모드에서 실행해 주세요.");
            const string path = "Assets/_ProtoType_Merge/Scenes/TutorialExploreScene.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("튜토리얼 탐사씬의 미저장 변경을 먼저 확인해 주세요.");
            JcDynamicBattleSource captured = null;
            try
            {
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                captured = JcDynamicBattleGeometry.Capture(scene, true);
                // 런타임 외형과 색상은 매번 실제 탐사씬에서 수집합니다. 여기에는 CPU 읽기용 메시만 저장합니다.
                captured.parts = captured.parts.Where(p => !p.mesh.isReadable).GroupBy(p => p.originalMesh).Select(g => g.First()).ToArray();
                string folder = Folder + "/TutorialDynamicSourceData";
                EnsureFolder(folder);
                EnsureDynamicReadable(captured, folder);
                var result = LoadOrCreate<JcDynamicBattleSource>(Folder + "/TutorialDynamicSource.asset");
                result.parts = captured.parts;
                result.sourceScene = path;
                EditorUtility.SetDirty(result);
                AssetDatabase.SaveAssetIfDirty(result);
                Debug.Log($"[JC 튜토리얼 배경] 읽기용 메시 {result.parts.Length}개 저장");
            }
            finally
            {
                if (captured != null) Object.DestroyImmediate(captured);
                if (opened && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }
        public static void EnsureDynamicReadable(JcDynamicBattleSource source, string outputFolder = null)
        {
            foreach(var part in source.parts)
            {
                var original=part.mesh;if(original.isReadable)continue;
                var mesh=new Mesh{name=original.name+"_DynamicReadable",indexFormat=original.indexFormat};
                mesh.vertices=original.vertices;mesh.normals=original.normals;mesh.tangents=original.tangents;mesh.colors=original.colors;
                for(int channel=0;channel<8;channel++){var uv=new System.Collections.Generic.List<Vector4>();original.GetUVs(channel,uv);if(uv.Count>0)mesh.SetUVs(channel,uv);}
                mesh.subMeshCount=original.subMeshCount;for(int sub=0;sub<original.subMeshCount;sub++)mesh.SetIndices(original.GetIndices(sub),original.GetTopology(sub),sub);
                mesh.RecalculateBounds();string path=(outputFolder ?? Folder+"/DynamicSourceData")+"/Readable_"+Hash128.Compute(AssetDatabase.GetAssetPath(original)+original.name)+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing==null){AssetDatabase.CreateAsset(mesh,path);part.mesh=mesh;}else{EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);AssetDatabase.SaveAssetIfDirty(existing);part.mesh=existing;}
            }
        }
    }
}
