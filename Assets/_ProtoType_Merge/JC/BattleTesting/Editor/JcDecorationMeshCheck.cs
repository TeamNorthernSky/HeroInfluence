using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace JC.BattleTesting.Editor
{
    // Level-loaded decorations may never appear in the editor scene snapshot.
    public sealed class JcDecorationMeshCheck : IPreprocessBuildWithReport
    {
        const string Catalog = "Assets/_ProtoType_Merge/DH/Prefabs/PrefabCatalog/DecorativeBuildingPrefabCatalog.asset";
        const string Folder = "Assets/_ProtoType_Merge/JC/BattleTesting/Resources/JC_Encounters";
        public int callbackOrder => 0;

        static IEnumerable<Mesh> CatalogMeshes()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DecorativeObjectPrefabCatalog>(Catalog);
            if (!catalog) throw new BuildFailedException("탐사 장식 카탈로그 누락: " + Catalog);
            if (!catalog.TryGetDecorativeObjectPrefab("Streetlight", out var lamp) ||
                !lamp.GetComponentsInChildren<MeshFilter>(true).Any(filter => filter.sharedMesh))
                throw new BuildFailedException("Streetlight 외형 누락: 필드 환경 FBX 패키지의 모델 연결을 확인하세요.");
            return catalog.DecorativeObjectPrefabs.Where(e => e.Prefab)
                .SelectMany(e => e.Prefab.GetComponentsInChildren<MeshFilter>(true))
                .Select(f => f.sharedMesh).Where(m => m && !m.isReadable).Distinct();
        }

        [MenuItem("JC/전투 테스트/필드 장식 메시 캐시 갱신")]
        public static void RefreshCache()
        {
            var source = AssetDatabase.LoadAssetAtPath<JcDynamicBattleSource>(Folder + "/DecorationMeshes.asset");
            if (!source)
            {
                source = ScriptableObject.CreateInstance<JcDynamicBattleSource>();
                AssetDatabase.CreateAsset(source, Folder + "/DecorationMeshes.asset");
            }
            var existing = Resources.Load<JcDynamicBattleSource>("JC_Encounters/DynamicSource");
            source.parts = CatalogMeshes().Where(m => !existing || !existing.FindReadable(m))
                .Select(m => new JcDynamicBattleSource.Part { originalMesh = m, mesh = m, name = m.name }).ToArray();
            JcEncounterBackgroundBuilder.EnsureDynamicReadable(source);
            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssetIfDirty(source);
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            var regular = Resources.Load<JcDynamicBattleSource>("JC_Encounters/DynamicSource");
            var extra = Resources.Load<JcDynamicBattleSource>("JC_Encounters/DecorationMeshes");
            foreach (var mesh in CatalogMeshes())
                if (!(regular && regular.FindReadable(mesh)) && !(extra && extra.FindReadable(mesh)))
                    throw new BuildFailedException("전투 장식 메시 누락: " + mesh.name + " · JC/전투 테스트/필드 장식 메시 캐시 갱신");
        }
    }
}
