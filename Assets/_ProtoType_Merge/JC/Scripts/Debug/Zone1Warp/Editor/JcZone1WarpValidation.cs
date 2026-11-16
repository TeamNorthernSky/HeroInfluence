// [CHEAT_ONLY / 다른 기능 재사용 금지 — 2026-10-27 사용자 요청]
// 이 파일은 1구역 플레이테스트 진입 시간을 줄이는 치트 전용입니다.
// 명시적으로 실행하는 비플레이 검증 도구입니다. 임시 객체와 singleton 교체를 사용하므로 런타임/자동 초기화/다른 기능에서 호출하지 마세요.
// 의존 방향은 치트 → 기존 게임 API만 허용합니다. 일반 게임 코드는 이 폴더를 참조하지 마세요.
// 공통 기능이 필요하면 기존 정식 시스템을 사용하세요. 치트 구현을 공용 코드로 승격하거나 복사하지 마세요.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.DebugTools.Editor
{
    // Play 진입/저장 파일 쓰기 없이 실제 탐사씬의 테이블 연결과 성장 규칙을 검사합니다.
    public static class JcZone1WarpValidation
    {
        [MenuItem("JC/Debug/1구역 이동 치트 데이터 검증")]
        public static void RunMenu() { Debug.Log(Run()); }

        public static string Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit 모드에서 실행해 주세요.");
            var preview = EditorSceneManager.NewPreviewScene();
            var savedEvent = EventScriptCatalog.Instance;
            var savedCsv = DHCsvTemplateCatalog.Instance;
            int checks = 0;
            var report = new StringBuilder();
            void Check(bool pass, string detail) { if (!pass) throw new Exception(detail); checks++; }
            try
            {
                var root = new GameObject("Warp validation (temporary)"); root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, preview);
                string scene = File.ReadAllText("Assets/_ProtoType_Merge/Scenes/DHScene_3.unity");
                var catalog = root.AddComponent<EventScriptCatalog>();
                string block = ComponentBlock(scene, catalog);
                var zone = new EventScriptZone { zoneId = 1, zoneLabel = "검증" };
                foreach (var field in typeof(EventScriptZone).GetFields())
                    if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)) field.SetValue(zone, Reference(block, field.Name, field.FieldType));
                var zone2 = new EventScriptZone { zoneId = 2, zoneLabel = "검증" };
                string zone2Block = block.Substring(block.IndexOf("- zoneId: 2", StringComparison.Ordinal));
                foreach (var field in typeof(EventScriptZone).GetFields())
                    if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)) field.SetValue(zone2, Reference(zone2Block, field.Name, field.FieldType));
                Set(catalog, "zones", new List<EventScriptZone> { zone, zone2 });
                Set(catalog, "mainSubEventRewardTable", Reference(block, "mainSubEventRewardTable", typeof(MainSubEventRewardDataTable)));
                SetInstance(typeof(EventScriptCatalog), catalog); catalog.Reload();
                var csv = root.AddComponent<DHCsvTemplateCatalog>();
                block = ComponentBlock(scene, csv);
                foreach (var field in typeof(DHCsvTemplateCatalog).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                    if (typeof(ScriptableObject).IsAssignableFrom(field.FieldType)) field.SetValue(csv, Reference(block, field.Name, field.FieldType));
                SetInstance(typeof(DHCsvTemplateCatalog), csv); csv.ReloadTemplates();

                Check(JcZone1WarpRoute.ExpPerUnit("BE161", 9999, 4) == 0, "아놀드 제외 실패");
                Check(JcZone1WarpRoute.ExpPerUnit("BE005", 75, 4) == 19, "전투별 EXP 올림 실패");
                Check(JcZone1WarpRoute.ExpPerUnit("BE005", 1, 4) + JcZone1WarpRoute.ExpPerUnit("BE006", 1, 4) == 2, "전투별 분배 실패");
                var flags = new HashSet<string>();
                Check(JcZone1WarpRoute.NextStage(flags.Contains) == 1, "최초 선택 단계 실패");
                flags.Add("Flag_MainQuest2-1Complete");
                Check(JcZone1WarpRoute.NextStage(flags.Contains) == 0, "이미 완료한 후속 분기 소급 변경 방지 실패");
                foreach (var first in JcZone1WarpRoute.Routes.Where(r => r.Stage == 1))
                    foreach (var second in JcZone1WarpRoute.Routes.Where(r => r.Stage == 2))
                    {
                        flags.Clear(); int exp = 0;
                        foreach (var route in new[] { first, second })
                        {
                            Check(!JcZone1WarpRoute.IsStageComplete(route.Stage, flags.Contains), "미수행 단계 판정 실패");
                            var row = route.ResolveCompletion(catalog, flags.Contains);
                            var tokens = DHEventEffectRuntimeManager.ParseEffects(row.TriggerEffect);
                            foreach (var effect in tokens)
                            {
                                Check(effect.kind != DHEventEffectKind.StartBattle && !effect.payload.Contains("Boss1Complete"), "점령전 완료/전투 시작 효과 혼입");
                                if (effect.kind == DHEventEffectKind.SetFlag) flags.Add(DHEventStateRepository.NormalizeFlagName(effect.payload));
                                if (effect.kind == DHEventEffectKind.Reward)
                                {
                                    Check(catalog.TryGetEventRewardTemplate(int.Parse(effect.payload), out var reward), "보상 연결 누락");
                                    Check(reward.CharacterRewards.All(r => r.Atk == 0 && r.Def == 0 && r.MaxHp == 0 && r.Heal == 0), "치트 정책 밖의 능력치 보상 추가됨");
                                }
                            }
                            Check(JcZone1WarpRoute.IsStageComplete(route.Stage, flags.Contains), "완료 단계 재선택 방지 실패");
                            if (route.Stage == 1) Check(!JcZone1WarpRoute.IsStageComplete(2, flags.Contains), "중간 정지 경계 침범");
                            Check(EnemySpawnPlanBuilder.TryBuildFromEventBattleKey(1, route.BattleKey, 1, out var plan, out var error), error);
                            int gained = JcZone1WarpRoute.ExpPerUnit(route.BattleKey, plan.Entries.Sum(e => e.Data.ExperiencePoint), 4);
                            Check(gained > 0, "진행 전투 경험치 누락"); exp += gained;
                        }
                        Check(!flags.Contains("Flag_MainQuestBoss1Complete"), "점령전 플래그 변경");
                        Check(flags.Contains("Flag_MainQuestBoss1HostageKnown") == (second.EventKey == "MainQuest2-1"), "인질 정보 경로 불일치");
                        report.AppendLine($"{first.Label} → {second.Label}: 캐릭터별 EXP +{exp} (적 Lv.1 검증)");
                    }
                // 이전 패배로 소문 플래그만 사라진 저장 상태의 기본 분기도 유효해야 합니다.
                flags.Clear(); flags.Add("Flag_MainQuest1Complete");
                foreach (var route in JcZone1WarpRoute.Routes.Where(r => r.Stage == 2))
                    Check(route.ResolveCompletion(catalog, flags.Contains) != null, "기존 진행 기본 경로 누락");

                foreach (var first in JcZone1WarpRoute.Zone2Routes.Where(r => r.Stage == 3))
                foreach (var second in JcZone1WarpRoute.Zone2Routes.Where(r => r.Stage == 4))
                {
                    flags.Clear();
                    flags.Add("Flag_MainQuestBoss1Complete");
                    Check(JcZone1WarpRoute.NextStage(flags.Contains, 1) == 0, "귀환 시 완료 분기 재실행");
                    foreach (var route in new[] { first, second })
                    {
                        Check(JcZone1WarpRoute.NextStage(flags.Contains, 2) == route.Stage, "2구역 선택 단계 오류");
                        if (route.ChoiceFlag != null) flags.Add(route.ChoiceFlag);
                        Check(EnemySpawnPlanBuilder.TryBuildFromEventBattleKey(2, route.ResolveBattleKey(flags.Contains), 1, out _, out var error), error);
                        foreach (var token in DHEventEffectRuntimeManager.ParseEffects(route.ResolveCompletion(catalog, flags.Contains).TriggerEffect))
                        {
                            if (token.kind == DHEventEffectKind.SetFlag) flags.Add(token.payload);
                            if (token.kind == DHEventEffectKind.Reward) Check(catalog.TryGetEventRewardTemplate(int.Parse(token.payload), out _), "2구역 보상 없음");
                        }
                    }
                    Check(JcZone1WarpRoute.NextStage(flags.Contains, 2) == 0 && !flags.Contains("Flag_MainQuestBoss2Complete"), "2구역 보스 직전 경계 오류");
                }
                var units = root.AddComponent<PersistentUnitRepository>();
                var lookup = (Dictionary<int, UnitPersistentData>)typeof(PersistentUnitRepository).GetField("unitLookup", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(units);
                for (int i = 1; i <= 4; i++)
                {
                    string key = (10000+i).ToString();
                    Check(csv.TryGetPlayerUnitTemplate(key, out var template), "히어로 템플릿 누락");
                    var stats = UnitStatCalculator.CalculateIngameStats(template.BaseStats, template.LevelupStats, 1, default, default, csv.GetUnitGrowthTemplates());
                    var data = new UnitPersistentData(i, key, 1, template.BaseStats, template.LevelupStats, 0, 0, default, stats, stats.HP, maxExp: 0);
                    lookup[i] = data;
                    PersistentUnitRepository.SimulateExpProgress(data, 1000, out int final, out int remaining, out int maximum);
                    Check(units.AddExp(i, 1000), "경험치 적용 실패");
                    Check(data.Level == final && data.Exp == remaining && data.MaxExp == maximum, "다중 레벨업/잔여 EXP 불일치");
                    Check(SkillUnlockResolver.GetUnlockCandidates(int.Parse(key), 1, final).Count > 0, "레벨에 따른 스킬 해금 누락");
                    units.ApplyEventRewardStats(i, 0, 0, 0, 5, 0);
                    units.ApplyEventRewardStats(i, 0, 0, 0, -7, 0);
                    Check(data.CurrentInfluence == 0, "IP 증감 순서/하한 처리 실패");
                }
                int highest = csv.GetUnitGrowthTemplates().First(r => r.Rank == "SSS").Level;
                foreach (var unit in lookup.Values)
                {
                    Check(units.ApplyLevelUp(unit.UnitIndex, highest - unit.Level), "랭크 상승 실패");
                    Check(UnitRankLookup.ResolveRank(csv.GetUnitGrowthTemplates(), unit.Level) == "SSS", "최고 랭크 미도달");
                    Check(csv.TryGetPlayerUnitTemplate(unit.UnitTemplateKey, out var hero) && csv.GetCurrentClassSkills(hero.ClassIndex, unit.Level).Count == 4, "4개 스킬 해금 실패");
                }
                // JC | 2026-11-16 | 제보된 6칸/4명 진형과 재사용을 실제 치트 멤버 수집·성장 함수로 회귀 검증합니다.
                VerifySparseParty(csv, units, lookup, highest, Check);
                var shader = Resources.Load<Shader>("JcZone1WarpCells");
                Check(shader != null && !ShaderUtil.ShaderHasError(shader), "착지 셀 셰이더 오류");
                report.AppendLine($"PASS: {checks} checks. Play/디스크 저장 없이 임시 프리뷰 객체에서 검증.");
                return report.ToString();
            }
            finally
            {
                SetInstance(typeof(EventScriptCatalog), savedEvent);
                SetInstance(typeof(DHCsvTemplateCatalog), savedCsv);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        // JC | 2026-11-16 | 프리뷰 저장소만 변경합니다. Play, 실제 파티·저장·모달·카메라에는 접근하지 않습니다.
        static void VerifySparseParty(DHCsvTemplateCatalog csv, PersistentUnitRepository units,
            Dictionary<int, UnitPersistentData> lookup, int highest, Action<bool, string> check)
        {
            var collect = typeof(JcZone1WarpCheat).GetMethod("CollectPartyMembers", BindingFlags.Static | BindingFlags.NonPublic);
            var rank = typeof(JcZone1WarpCheat).GetMethod("ApplyPartyRank", BindingFlags.Static | BindingFlags.NonPublic);
            var members = new List<int>();
            var party = new PartyPersistentData("cheat-validation", new[] { 0, 1, 0, 2, 4, 3 }, new[] { 4, 5, 6, 1, 2, 3 });
            string originalFormation = JsonUtility.ToJson(party);
            for (int i = 1; i <= 4; i++)
            {
                check(csv.TryGetPlayerUnitTemplate((10000 + i).ToString(), out var template), "빈 슬롯 검사 템플릿 누락");
                var stats = UnitStatCalculator.CalculateIngameStats(template.BaseStats, template.LevelupStats, 1, default, default, csv.GetUnitGrowthTemplates());
                lookup[i] = new UnitPersistentData(i, template.UnitKey, 1, template.BaseStats, template.LevelupStats,
                    0, 0, default, stats, i == 4 ? 0 : stats.HP - 2, isIncapacitated: i == 4);
            }
            for (int run = 0; run < 5; run++)
            {
                // 저장/복원으로 새 파티 인스턴스가 되어도 빈 슬롯 규칙은 같고, 이전 실행의 멤버는 누적되지 않아야 합니다.
                var restored = JsonUtility.FromJson<PartyPersistentData>(originalFormation);
                collect.Invoke(null, new object[] { restored, units, members });
                check(members.SequenceEqual(new[] { 1, 2, 4, 3 }), "빈 슬롯 제외 또는 반복 사용 멤버 초기화 실패");
                check(JcZone1WarpRoute.ExpPerUnit("BE005", 100, members.Count) == 25, "실제 4명 경험치를 6칸으로 분배함");
                bool changed = (bool)rank.Invoke(null, new object[] { units, members, highest });
                check(changed == (run == 0), "최고 랭크 반복 사용의 중복 성장/부작용");
                foreach (int id in members)
                {
                    var unit = lookup[id];
                    check(unit.Level == highest, "빈 슬롯 포함 파티의 만렙 적용 실패");
                    check(id == 4 ? unit.CurrentHp == 0 && unit.IsIncapacitated : unit.IngameStats.HP - unit.CurrentHp == 2,
                        "만렙 치트가 기존 부상/행동불능을 변경함");
                }
                check(JsonUtility.ToJson(restored) == originalFormation, "치트가 원본 진형을 변경함");
            }
            collect.Invoke(null, new object[] { new PartyPersistentData("changed", new[] { 0, 4 }), units, members });
            check(members.SequenceEqual(new[] { 4 }), "다른 파티 재사용 시 이전 멤버 잔존");
            foreach (int[] invalid in new[] { new[] { 0, 0 }, new[] { 1, 999 }, new[] { 1, 1 }, new[] { -1, 2 } })
            {
                bool rejected = false;
                try { collect.Invoke(null, new object[] { new PartyPersistentData("invalid", invalid), units, members }); }
                catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { rejected = true; }
                check(rejected, "빈 파티·누락·중복·음수 유닛을 정상 파티로 처리함");
            }
            check(JsonUtility.ToJson(party) == originalFormation, "실패 경로에서 원본 진형 변경");
        }

        static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static void SetInstance(Type type, object value) => type.GetProperty("Instance").GetSetMethod(true).Invoke(null, new[] { value });
        static string ComponentBlock(string scene, MonoBehaviour component)
        {
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(component)));
            foreach (string block in Regex.Split(scene, @"(?m)^--- "))
                if (block.Contains("guid: " + guid)) return block;
            throw new Exception("탐사씬 컴포넌트 연결 누락: " + component.GetType().Name);
        }
        static UnityEngine.Object Reference(string block, string field, Type type)
        {
            var match = Regex.Match(block, @"\b" + field + @":\s*\{fileID: \d+, guid: ([a-f0-9]+)");
            return match.Success ? AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(match.Groups[1].Value), type) : null;
        }
    }
}
