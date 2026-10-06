using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.BattleTesting.Editor
{
    // 원본 대응: TmpBattleScene의 씬 객체와 기존 직렬화 연결.
    // 씬 전체 환경을 새 씬에 복사한 뒤, 해당 복사본의 제어 컴포넌트/참조만 JC 대응 객체로 바꿉니다.
    // 원본 씬·프리팹·데이터 SO는 저장하거나 수정하지 않습니다.
    public static class JcBattleTestSceneBuilder
    {
        public const string Destination = "Assets/_ProtoType_Merge/JC/JC_TestScenes/JC_BattleTestScene.unity";
        private const string Module = "Assets/_ProtoType_Merge/JC/BattleTesting/";
        private const string Tables = "Assets/_ProtoType_Merge/ASB/Data/Tables/H.I 알파 전투 밸런스 데이터 테이블 V5.4/";
        public static string Create()
        {
            if (EditorApplication.isPlaying || EditorUtility.scriptCompilationFailed || EditorApplication.isCompiling) throw new InvalidOperationException("비플레이/컴파일 완료 상태에서만 생성할 수 있습니다.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("현재 씬에 미저장 변경이 있습니다.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Destination) != null) throw new InvalidOperationException("기존 테스트 씬을 덮어쓰지 않습니다.");
            AssetDatabase.CopyAsset("Assets/_ProtoType_Merge/Scenes/TmpBattleScene.unity", Destination);
            Scene scene = EditorSceneManager.OpenScene(Destination, OpenSceneMode.Single);
            // 원본 DataManager/CSVDataLoad의 폐기 참조는 JC 복사본에서만 제거합니다(사용자 승인).
            foreach (var dataObject in scene.GetRootGameObjects().Where(x => x.name == "DataManager"))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(dataObject);
            var behaviours = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true)).Where(x => x != null).ToArray();
            var replacements = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            foreach (var old in behaviours)
            {
                // 공용 GridCell/BattleCharactor 등록부는 유지하고 표시만 JC 연결기로 분리합니다.
                if (old is ASB.Work.BattleGrid.BattleGridManager) continue;
                Type replacementType = typeof(JcBattleTestSession).Assembly.GetTypes().FirstOrDefault(t => t.Name == "Jc" + old.GetType().Name && typeof(MonoBehaviour).IsAssignableFrom(t));
                if (replacementType == null) continue;
                var added = old.gameObject.AddComponent(replacementType);
                var oldSerialized = new SerializedObject(old);
                var newSerialized = new SerializedObject(added);
                var originalProperty = oldSerialized.GetIterator();
                while (originalProperty.NextVisible(true))
                    if (originalProperty.propertyPath != "m_Script" && newSerialized.FindProperty(originalProperty.propertyPath) != null)
                        newSerialized.CopyFromSerializedProperty(originalProperty);
                newSerialized.ApplyModifiedPropertiesWithoutUndo();
                replacements.Add(old, added);
            }
            foreach (var component in scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Component>(true)).Where(x => x != null))
            {
                if (replacements.ContainsKey(component)) continue;
                var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script" && property.objectReferenceValue != null && replacements.TryGetValue(property.objectReferenceValue, out var replacement))
                        property.objectReferenceValue = replacement;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var original in replacements.Keys) UnityEngine.Object.DestroyImmediate(original);
            foreach (var old in behaviours.Where(x => x != null && (x is BattleSceneManager || x is PlayerSpawner || x.GetType().Name == "CombatSceneEntryController" || x.GetType().Name == "BattleSceneNameLabelInstaller"))) UnityEngine.Object.DestroyImmediate(old);
            var root = new GameObject("JC_BattleTest");
            var session = root.AddComponent<JcBattleTestSession>();
            session.playerPlace = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).First(x => x.name == "PlayerPlace");
            session.enemyPlace = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).First(x => x.name == "EnemyPlace");
            session.flow = Find<JcBattleFlowManager>(scene); session.execution = Find<JcBattleManager>(scene);
            session.blackboard = Find<JcEncounterBlackboard>(scene) ?? root.AddComponent<JcEncounterBlackboard>();
            session.spawner = Find<JcEnemySpawner>(scene) ?? session.enemyPlace.gameObject.AddComponent<JcEnemySpawner>();
            session.catalog = Find<DHCsvTemplateCatalog>(scene) ?? new GameObject("JC_OriginalDataCatalog").AddComponent<DHCsvTemplateCatalog>();
            Set(session.catalog, "playerUnitDataTable", AssetDatabase.LoadAssetAtPath<PlayerUnitDataTable>(Tables + "PlayerUnitDataTable.asset"));
            Set(session.catalog, "playerWeaponDataTable", AssetDatabase.LoadAssetAtPath<PlayerWeaponDataTable>(Tables + "PlayerWeaponDataTable.asset"));
            Set(session.catalog, "classSkillDataTable", AssetDatabase.LoadAssetAtPath<ClassSkillDataTable>(Tables + "ClassSkillDataTable.asset"));
            Set(session.catalog, "unitGrowthExpDataTable", AssetDatabase.LoadAssetAtPath<UnitGrowthExpDataTable>(Tables + "UnitGrowthExpDataTable.asset"));
            SetBool(session.catalog, "dontDestroyOnLoad", false); SetBool(session.catalog, "loadOnAwake", true);
            session.catalog.ReloadTemplates();
            session.enemyTable = AssetDatabase.LoadAssetAtPath<EnemyUnit1SectorDataTable>("Assets/_ProtoType_Merge/ASB/Data/Tables/2구역 전투 테이블 V1.3/EnemyUnit1SectorDataTable.asset");
            session.unitPrefabs = AssetDatabase.LoadAssetAtPath<BattleUnitPrefabCatalog>("Assets/ASB_Work/Prefab/BattleUnitPrefabCatalog.asset");
            const string enemies = "Assets/Resources/prefab/BattlePrefab/EnemyUnit/";
            session.yuliaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(enemies + "Unit_VillanMadonna_40001.prefab");
            session.auxiliaryPrefabs = new[] { "Unit_VillanAmplifier_40002.prefab", "Unit_VillanAmplifier_40003.prefab", "Unit_VillanWheel_40005.prefab" }.Select(x => AssetDatabase.LoadAssetAtPath<GameObject>(enemies + x)).ToArray();
            // 임시 외형은 기존 율리아 모델을 그대로 참조합니다. 별도 독립 유닛의 수치/스킬은 세션이 적용합니다.
            var veil = (GameObject)PrefabUtility.InstantiatePrefab(session.yuliaPrefab);
            veil.name = "JC_YuliaVeil_Temporary";
            session.veilPrefab = PrefabUtility.SaveAsPrefabAsset(veil, Module + "JC_YuliaVeil_Temporary.prefab");
            UnityEngine.Object.DestroyImmediate(veil);
            var full = ScriptableObject.CreateInstance<JcEnemyPreset>(); full.fullEncounter = true;
            AssetDatabase.CreateAsset(full, Module + "JC_YuliaFullEncounter.asset");
            var solo = ScriptableObject.CreateInstance<JcEnemyPreset>();
            AssetDatabase.CreateAsset(solo, Module + "JC_YuliaSoloTest.asset");
            session.settings.enemyPreset = full;
            session.profile = ScriptableObject.CreateInstance<JcBattleTestProfile>();
            session.profile.settings = JsonUtility.FromJson<JcBattleTestSettings>(JsonUtility.ToJson(session.settings));
            AssetDatabase.CreateAsset(session.profile, Module + "JC_BattleTest_Default.asset");
            SetBool(session.flow, "autoStartOnInitialize", false); SetBool(session.spawner, "spawnOnStart", false);
            foreach (var actor in new[] { session.playerPlace, session.enemyPlace }.SelectMany(x => x.GetComponentsInChildren<BattleCharactor>(true)).ToArray()) UnityEngine.Object.DestroyImmediate(actor.gameObject);
            BattleLogicalSlotMap.TryCreate(session.playerPlace, out var slots, out _);
            foreach (var setup in session.settings.allies)
            {
                session.unitPrefabs.TryGetPlayerPrefab(setup.unitKey, out var prefab); slots.TryResolve(setup.slot, out var slot);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                go.name = "JC_Preview_" + setup.unitKey; go.transform.SetParent(slot.Cell.transform);
                go.transform.SetPositionAndRotation(slot.WorldPosition, slot.WorldRotation);
            }
            EditorUtility.SetDirty(session);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets(); Selection.activeGameObject = root;
            JcBattleTestSceneWiring.Apply();
            JcYuliaVfxSetup.Apply();
            return Destination;
        }
        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<T>(true)).FirstOrDefault();
        private static void Set(UnityEngine.Object target, string name, UnityEngine.Object value)
        { var s = new SerializedObject(target); var p = s.FindProperty(name); if (p == null) throw new InvalidOperationException(name); p.objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetBool(UnityEngine.Object target, string name, bool value)
        { var s = new SerializedObject(target); var p = s.FindProperty(name); if (p == null) throw new InvalidOperationException(name); p.boolValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
