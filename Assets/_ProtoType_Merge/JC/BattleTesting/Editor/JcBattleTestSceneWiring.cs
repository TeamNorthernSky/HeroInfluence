using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ASB.Work.BattleGrid;

namespace JC.BattleTesting.Editor
{
    // 원본 대응: TmpBattleScene의 직렬화된 UI/입력/전투 흐름 연결.
    // 새 씬을 복사하지 않고 현재 JC 씬의 기존 배치·사용자 설정을 유지하여 연결만 보완합니다.
    public static class JcBattleTestSceneWiring
    {
        public static string Apply(bool allowOwnUnsavedChanges = false)
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorUtility.scriptCompilationFailed || scene.path != JcBattleTestSceneBuilder.Destination)
                throw new InvalidOperationException("저장된 JC 테스트 씬의 비플레이 상태에서만 연결합니다.");
            if (scene.isDirty && !allowOwnUnsavedChanges) throw new InvalidOperationException("미저장 씬을 자동 저장하지 않습니다.");
            var before = Behaviours(scene);
            var replacements = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
            var pairs = new Dictionary<Type, Type> {
                {typeof(AutoBattleToggleButton), typeof(JcAutoBattleToggleButton)},
                {typeof(BattleSpeedToggleButton), typeof(JcBattleSpeedToggleButton)},
                {typeof(BattleEnemyInfoTooltip), typeof(JcBattleEnemyInfoTooltip)},
                {typeof(TargetingVisualController), typeof(JcTargetingVisualController)}
            };
            foreach (var old in before)
                if (pairs.TryGetValue(old.GetType(), out var type))
                    replacements.Add(old, CopyComponent(old, type));
            var registry = before.OfType<BattleGridManager>().Single();
            var presentation = before.OfType<JcBattleGridManager>().SingleOrDefault();
            if (presentation == null) presentation = (JcBattleGridManager)CopyComponent(registry, typeof(JcBattleGridManager));
            // 원본 등록부는 공용 유닛/타깃 조회에 계속 사용하고 표시 갱신의 중복 실행만 막습니다.
            var registryData = new SerializedObject(registry);
            registryData.FindProperty("useBattleTilePresentation").boolValue = false;
            registryData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(registry);
            foreach (var component in Components(scene))
            {
                var data = new SerializedObject(component);
                var property = data.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.name != "m_Script" &&
                        property.objectReferenceValue != null && replacements.TryGetValue(property.objectReferenceValue, out var next))
                        property.objectReferenceValue = next;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (var old in replacements.Keys) UnityEngine.Object.DestroyImmediate(old);
            var all = Behaviours(scene);
            var session = all.OfType<JcBattleTestSession>().Single();
            // 원본 InputHandler.Awake의 FindFirstObjectByType 대응: 원본 씬에서 미배치된 선택 표시 제어를 JC 입력에 배치합니다.
            if (!all.OfType<JcTargetingVisualController>().Any())
                all.OfType<JcInputHandler>().Single().gameObject.AddComponent<JcTargetingVisualController>();
            all = Behaviours(scene);
            var sourceTypes = new[] { typeof(JcBattleFlowManager), typeof(JcBattleManager), typeof(JcInputHandler),
                typeof(JcAutoBattleController), typeof(JcTargetingVisualController), typeof(JcBattleTurnBanner),
                typeof(JcEncounterBlackboard), typeof(JcEnemySpawner), typeof(JcSkillButtonController) };
            var targets = sourceTypes.ToDictionary(t => t, t => all.Single(x => x.GetType() == t));
            foreach (var component in all.Where(x => x.GetType().Name.StartsWith("Jc", StringComparison.Ordinal)))
            {
                var data = new SerializedObject(component);
                foreach (var field in component.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    if (targets.TryGetValue(field.FieldType, out var target))
                    {
                        var property = data.FindProperty(field.Name);
                        if (property != null && property.propertyType == SerializedPropertyType.ObjectReference)
                            property.objectReferenceValue = target;
                    }
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(component);
            }
            if (!session.Validate(out var error)) throw new InvalidOperationException(error);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("JC 씬 저장 실패");
            return "UI 연결 완료 / 교체 " + replacements.Count + " / 원본 그리드 등록부 유지 / 턴 안내 연결";
        }
        private static Component CopyComponent(Component original, Type type)
        {
            var added = original.gameObject.AddComponent(type);
            var source = new SerializedObject(original);
            var destination = new SerializedObject(added);
            var property = source.GetIterator();
            while (property.NextVisible(true))
            {
                var next = destination.FindProperty(property.propertyPath);
                if (property.propertyPath == "m_Script" || next == null) continue;
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.type != next.type) continue;
                destination.CopyFromSerializedProperty(property);
            }
            destination.ApplyModifiedPropertiesWithoutUndo();
            return added;
        }
        private static MonoBehaviour[] Behaviours(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true)).Where(x => x != null).ToArray();
        private static Component[] Components(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(x => x.GetComponentsInChildren<Component>(true)).Where(x => x != null).ToArray();
    }
}
