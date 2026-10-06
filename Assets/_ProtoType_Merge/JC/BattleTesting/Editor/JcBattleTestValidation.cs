using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ASB.Work.Battle.Core;

namespace JC.BattleTesting.Editor
{
    // JC 전용 계약 검증. Play/코루틴 전투/실제 프리팹 실행 없이 규칙과 임시 데이터의 결과를 확인합니다.
    public static class JcBattleTestValidation
    {
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed)
                throw new InvalidOperationException("비플레이 컴파일 완료 상태가 필요합니다.");
            var scene = SceneManager.GetActiveScene();
            if (scene.path != JcBattleTestSceneBuilder.Destination || scene.isDirty) throw new InvalidOperationException("저장된 JC 씬이 필요합니다.");
            var all = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true)).Where(x => x != null).ToArray();
            var session = all.OfType<JcBattleTestSession>().Single();
            int passed = 0;
            Action<bool, string> check = (ok, label) => { if (!ok) throw new InvalidOperationException("검증 실패: " + label); passed++; };
            check(session.Validate(out var error), "초기 설정: " + error);
            check(scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true))
                .Sum(x => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(x.gameObject)) == 0, "Missing Script 0");
            foreach (var component in all.Where(x => x.GetType().Name.StartsWith("Jc", StringComparison.Ordinal)))
            {
                var serialized = new SerializedObject(component);
                foreach (var field in component.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    var property = serialized.FindProperty(field.Name);
                    if (property != null && property.propertyType == SerializedPropertyType.ObjectReference &&
                        typeof(Component).IsAssignableFrom(field.FieldType) && field.FieldType.Name.StartsWith("Jc", StringComparison.Ordinal))
                        check(property.objectReferenceValue != null, "UI 제어 연결 " + component.GetType().Name + "." + field.Name);
                }
            }
            var rules = new JcYuliaEncounterRules(1, false);
            rules.AmplifierDestroyed(40002); rules.AmplifierDestroyed(40002);
            check(!rules.EvaluateAfterAction(1) && rules.DestroyedAmplifierCount == 1, "같은 증폭기 중복 파괴는 2 진입 안 함");
            rules.AmplifierDestroyed(40003);
            check(rules.EvaluateAfterAction(1) && rules.Phase == 2, "서로 다른 증폭기 2개 파괴 → 2");
            check(!rules.EvaluateAfterAction(.5001f), "HP 50% 초과는 3 진입 안 함");
            check(rules.EvaluateAfterAction(.5f) && rules.Phase == 3, "HP 50% 경계 → 3");
            var direct = new JcYuliaEncounterRules(1, false);
            direct.TryReserve(1); direct.Energy1 = 2; direct.Energy2 = 1;
            direct.AmplifierDestroyed(40002); direct.AmplifierDestroyed(40003);
            check(direct.EvaluateAfterAction(0) && direct.Phase == 3, "본체 치명상/파괴 동시 달성 시 3 우선");
            check(direct.ReservedSocket == 0 && direct.Energy1 == 0 && direct.Energy2 == 0, "3 진입 예약/에너지 초기화");
            check(!direct.EvaluateAfterAction(0), "3 진입 효과 중복 없음");
            for (int phase = 1; phase <= 3; phase++)
            {
                var fixedRules = new JcYuliaEncounterRules(phase, true);
                fixedRules.AmplifierDestroyed(40002); fixedRules.AmplifierDestroyed(40003);
                check(!fixedRules.EvaluateAfterAction(0) && fixedRules.Phase == phase, "단독 고정 페이즈 " + phase);
            }
            for (int i = 0; i < 5; i++) direct.AmplifierActionCompleted(40002);
            check(Mathf.Approximately(direct.AmplifierAttackMultiplier(40002), 2), "충전 5 공격력 +100%");
            direct.AmplifierDestroyed(40002);
            check(direct.GetCharge(40002) == 2 && Mathf.Approximately(direct.VeilDamageMultiplier, 1.5f), "파괴 -3 / 장막 +50%");
            direct.AmplifierDestroyed(40003);
            check(direct.GetCharge(40003) == 0 && Mathf.Approximately(direct.VeilDamageMultiplier, 2), "충전 하한 0 / 과부하 누적");
            check(JcYuliaEncounterRules.EvaluateResult(true, false) == BattleResult.Victory, "승패 동시 충족 승리 우선");
            check(JcYuliaEncounterRules.EvaluateResult(false, false) == BattleResult.Defeat, "아군 전멸 패배");
            var request = new JcBossActionRequest(400013);
            check(request.TryCommit() && !request.TryCommit(), "행동 확정 정확히 한 번");
            var cancelled = new JcBossActionRequest(400013); cancelled.Cancel();
            check(!cancelled.TryCommit(), "취소된 선택 실행 금지");
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var caster = Actor(preview, "JC_ValidationCaster", 100);
                var target = Actor(preview, "JC_ValidationTarget", 5);
                // 본체 → 장막 교체 시 아직 남은 행동권만 승계하고, 이미 소비한 턴은 되살리지 않습니다.
                var replacement = Actor(preview, "JC_ValidationVeil", 5);
                var flowObject = new GameObject("JC_ValidationFlow");
                SceneManager.MoveGameObjectToScene(flowObject, preview);
                var testFlow = flowObject.AddComponent<JcBattleFlowManager>();
                testFlow.Initialize(new List<BattleCharactor> { caster, target }, false);
                var queueField = typeof(JcBattleFlowManager).GetField("turnQueue", BindingFlags.Instance | BindingFlags.NonPublic);
                queueField.SetValue(testFlow, new Queue<BattleCharactor>(new[] { caster, target }));
                testFlow.ReplaceParticipant(target, replacement);
                check(((Queue<BattleCharactor>)queueField.GetValue(testFlow)).SequenceEqual(new[] { caster, replacement }), "장막이 기존 대기 순서와 행동권을 승계");
                queueField.SetValue(testFlow, new Queue<BattleCharactor>(new[] { caster }));
                testFlow.ReplaceParticipant(replacement, target);
                check(((Queue<BattleCharactor>)queueField.GetValue(testFlow)).SequenceEqual(new[] { caster }), "장막 교체가 소비한 행동권을 추가하지 않음");
                var context = new DamageContext { Caster = caster, Target = target, SkillValue = 1, IsRangedAttack = true };
                float basicDamage = JcCombatCalculator.CalculateDamage(context);
                context.SkillValue = 2;
                check(JcCombatCalculator.CalculateDamage(context) > basicDamage, "실제 피해 계산에 강화 계수 반영");
                using (caster.AddMinimumHpConstraint(1, typeof(JcBattleTestValidation)))
                {
                    caster.TakeDamage(10000);
                    check(caster.CurrentHp == 1 && !caster.IsDead, "아군 사망 방지 HP 1 / 사망 이벤트 방지");
                }
                check(caster.MinimumHpConstraintCount == 0, "사망 방지 해제 후 제약 잔존 없음");
                foreach (var ally in session.settings.allies)
                    for (int rank = 1; rank <= 8; rank++)
                    {
                        var setup = JsonUtility.FromJson<JcAllySetup>(JsonUtility.ToJson(ally));
                        setup.level = rank; setup.skillLevels = new[] { 5, 5, 5, 5 };
                        var data = JcTestDataAssembler.PrepareAlly(setup, session.catalog);
                        check(data.Skills.Count > 0 && data.Skills.All(x => x.acquireLevel <= rank), "랭크 해금 " + ally.unitKey + ":" + rank);
                        foreach (var skill in data.Skills)
                            check(Mathf.Approximately(skill.skillValue, session.catalog.GetClassSkillValueAtLevel(skill.skillIndex, 5)) &&
                                Mathf.Approximately(skill.skillSubValue, session.catalog.GetClassSkillSubValueAtLevel(skill.skillIndex, 5)), "최신 테이블 강화 계수 " + skill.skillIndex);
                        JcTestDataAssembler.ApplyAlly(caster, data);
                        check(Mathf.Approximately(caster.CurrentHp, data.Stats.HP * setup.hpRatio) && Mathf.Approximately(caster.CurrentInfluence, setup.initialIP), "HP/IP 실제 인스턴스 주입");
                        check(ReferenceEquals(caster.SourceData, data.Detached), "영속 저장소와 분리된 스냅샷");
                    }
                var description = JcTestDataAssembler.DescribeAppliedSkill(new SkillData { description = "{ClassSkillValue}% / {ClassSkillSubValue}", skillValue = 1.5f, skillSubValue = .25f });
                check(description == "150% / 25%", "설명과 시전 계수 일치 / 퍼센트 중복 없음");
                foreach (int id in new[] { 40001, 40002, 40003, 40004, 40005 })
                {
                    var enemy = JcTestDataAssembler.BuildEnemy(session.enemyTable, id, 1, 1);
                    check(enemy.Data.baseStats.HP > 0 && enemy.ExplicitSkills.Count > 0, "최신 적 데이터 " + id);
                }
                bool originalAuto = BattleRuntimeSettings.IsAutoBattle;
                float originalSpeed = BattleRuntimeSettings.BattleSpeed;
                bool testAuto = JcBattleRuntimeSettings.IsAutoBattle;
                float testSpeed = JcBattleRuntimeSettings.BattleSpeed;
                try
                {
                    JcBattleRuntimeSettings.SetAutoBattle(!originalAuto); JcBattleRuntimeSettings.SetBattleSpeed(originalSpeed + 1);
                    check(BattleRuntimeSettings.IsAutoBattle == originalAuto && BattleRuntimeSettings.BattleSpeed == originalSpeed, "본게임 자동전투/배속 상태 격리");
                }
                finally { JcBattleRuntimeSettings.SetAutoBattle(testAuto); JcBattleRuntimeSettings.SetBattleSpeed(testSpeed); }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            check(!scene.isDirty, "검증이 씬 설정을 변경하지 않음");
            return passed + "개 비플레이 계약 검사 통과 / UI 연결·페이즈·승패·4히어로 8랭크·강화 계수·HP 제약";
        }
        private static BattleCharactor Actor(Scene scene, string name, float attack)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            var actor = go.AddComponent<BattleCharactor>();
            actor.SetBaseStats(new StatBlock { HP = 100, Atk = attack, Influence = 200 });
            actor.SetLevelScaling(false); actor.RecalculateStats(false); actor.InitializeCurrentState(100, 0);
            return actor;
        }
    }
}
