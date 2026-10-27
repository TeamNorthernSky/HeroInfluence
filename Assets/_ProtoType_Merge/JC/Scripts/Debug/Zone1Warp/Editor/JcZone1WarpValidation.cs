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
                Set(catalog, "zones", new List<EventScriptZone> { zone });
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
