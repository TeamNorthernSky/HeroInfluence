// [CHEAT_ONLY / 다른 기능 재사용 금지 — 2026-10-27 사용자 요청]
// 이 파일은 1구역 플레이테스트 진입 시간을 줄이는 치트 전용입니다.
// 정상 전투·퀘스트·보상 처리의 대체 API가 아닙니다. 전원 생존 가정, 아놀드 EXP 제외, 보상 일부 생략 등 치트 정책을 포함합니다.
// 의존 방향은 치트 → 기존 게임 API만 허용합니다. 일반 게임 코드는 이 폴더를 참조하지 마세요.
// 공통 기능이 필요하면 기존 정식 시스템을 사용하세요. 치트 구현을 공용 코드로 승격하거나 복사하지 마세요.

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace JC.DebugTools
{
    public sealed partial class JcZone1WarpCheat
    {
        bool Flag(string key) => flags.TryGetValue(DHEventStateRepository.NormalizeFlagName(key), out bool value) && value;

        void RebuildPlan()
        {
            flags.Clear(); steps.Clear();
            foreach (var f in originalFlags) flags[f.Key] = f.Value;
            var catalog = EventScriptCatalog.Instance;
            foreach (var route in selected)
            {
                if (JcZone1WarpRoute.NextStage(Flag) != route.Stage)
                    throw new InvalidOperationException("이미 완료된 단계는 다시 선택할 수 없습니다.");
                if (route.Stage == 2 && !JcZone1WarpRoute.IsStageComplete(1, Flag))
                    throw new InvalidOperationException("앞 단계의 분기를 먼저 선택해 주세요.");
                if (FindEvent(route.EventKey) == null || MapProgressRepository.Instance.IsMainEventCompleted(route.EventKey))
                    throw new InvalidOperationException($"진행할 수 없는 이벤트입니다: {route.EventKey}");
                // 대화 엔진과 같은 우선순위: 첫 일치 행. 루미나/네코밍이 소년 미동행 기본행보다 우선합니다.
                DHEventBranchTemplate completion = route.ResolveCompletion(catalog, Flag);
                var step = new JcZone1WarpStep { Route = route, Effects = DHEventEffectRuntimeManager.ParseEffects(completion.TriggerEffect) };
                foreach (var effect in step.Effects)
                {
                    switch (effect.kind)
                    {
                        case DHEventEffectKind.SetFlag: flags[DHEventStateRepository.NormalizeFlagName(effect.payload)] = true; break;
                        case DHEventEffectKind.DisableFlag: flags[DHEventStateRepository.NormalizeFlagName(effect.payload)] = false; break;
                        case DHEventEffectKind.Reward:
                            if (!int.TryParse(effect.payload, out int rewardId) || !catalog.TryGetEventRewardTemplate(rewardId, out var reward))
                                throw new InvalidOperationException($"보상 데이터를 찾지 못했습니다: {effect.payload}");
                            step.Rewards.Add(reward);
                            break;
                        case DHEventEffectKind.DisableEvent:
                            var disabled = FindEvent(effect.payload);
                            if (disabled != null && !string.IsNullOrEmpty(disabled.ReplacementEnemyGroupKey))
                            {
                                if (FindFirstObjectByType<EnemySpawnController>() == null || DHCsvTemplateCatalog.Instance == null ||
                                    !DHCsvTemplateCatalog.Instance.TryGetEnemyGroupTemplate(disabled.ReplacementEnemyGroupKey, out _))
                                    throw new InvalidOperationException("반대 분기의 대체 적 생성 데이터가 준비되지 않았습니다.");
                            }
                            break;
                        default: throw new InvalidOperationException($"완료 노드에 허용되지 않은 효과가 있습니다: {effect.rawToken}");
                    }
                }
                if (!Flag($"Flag_{route.EventKey}Complete")) throw new InvalidOperationException("완료 플래그가 없는 분기입니다.");
                if (!catalog.TryGetBattleEnemyGroupTemplate(1, route.BattleKey, out var group))
                    throw new InvalidOperationException($"전투 그룹 누락: {route.BattleKey}");
                int zoneLevel = 1;
                if (!MapProgressRepository.Instance.TryGetZoneEnemyLevel("1", out zoneLevel))
                {
                    int sum = 0;
                    foreach (int index in partyData.UnitIndices)
                    {
                        if (!PersistentUnitRepository.Instance.TryGetUnit(index, out var data)) throw new InvalidOperationException("파티 유닛 누락");
                        sum += data.Level;
                    }
                    zoneLevel = Mathf.Max(1, sum / partyData.UnitIndices.Count);
                }
                int enemyLevel = Mathf.Max(1, zoneLevel);
                plannedZoneEnemyLevel = enemyLevel;
                if (group.MinLevel > 0) enemyLevel = Mathf.Max(enemyLevel, group.MinLevel);
                if (group.MaxLevel > 0) enemyLevel = Mathf.Min(enemyLevel, group.MaxLevel);
                if (!EnemySpawnPlanBuilder.TryBuildFromEventBattleKey(1, route.BattleKey, enemyLevel, out var spawn, out string error))
                    throw new InvalidOperationException(error);
                float total = 0;
                foreach (var enemy in spawn.Entries) total += Mathf.Max(0, enemy.Data.ExperiencePoint);
                step.ExperiencePerUnit = JcZone1WarpRoute.ExpPerUnit(route.BattleKey, total, partyData.UnitIndices.Count);
                foreach (var reward in step.Rewards) step.ExperiencePerUnit = checked(step.ExperiencePerUnit + Mathf.Max(0, reward.Exp));
                steps.Add(step);
            }
        }

        MainEventObject FindEvent(string key)
        {
            foreach (var e in FindObjectsByType<MainEventObject>(FindObjectsSortMode.None))
                if (e.ZoneId == 1 && string.Equals(e.EventKey, key, StringComparison.OrdinalIgnoreCase)) return e;
            return null;
        }

        void ValidateUnchanged()
        {
            if (party == null || party.IsMoving || party.GetCurrentGrid() != originalGrid || DHGameEndState.IsEnding ||
                ModalManager.Top != ui.gameObject || SceneFadeController.HideCursorDuringTransition)
                throw new InvalidOperationException("탐사 상태가 변경되었습니다. 취소 후 다시 열어 주세요.");
            foreach (var f in DHEventStateRepository.Instance.Flags)
            {
                bool old = originalFlags.TryGetValue(f.flagName, out var value) && value;
                if (old != f.value) throw new InvalidOperationException("분기 상태가 변경되었습니다. 취소 후 다시 열어 주세요.");
            }
            foreach (var f in originalFlags)
                if (DHEventStateRepository.Instance.GetFlag(f.Key) != f.Value)
                    throw new InvalidOperationException("분기 상태가 변경되었습니다. 취소 후 다시 열어 주세요.");
        }

        // 치트 목적지 확정 경로에서만 호출합니다. 정식 전투 종료/보상 지급/퀘스트 완료 함수로 사용하지 마세요.
        void ApplyPlan()
        {
            var repository = PersistentUnitRepository.Instance;
            var effects = DHEventEffectRuntimeManager.EnsureInstance();
            if (steps.Count > 0)
                MapProgressRepository.Instance.EnsureZoneEnemyLevel("1", plannedZoneEnemyLevel);
            foreach (var step in steps)
            {
                DHEventStateRepository.Instance.SetNumericValue("Flag_BattleResult", 1f);
                // 전체 전투 종료 루틴은 대화/씬 전환을 시작하므로 호출하지 않습니다.
                foreach (int index in partyData.UnitIndices)
                {
                    repository.ApplyEventRewardStats(index, 0, 0, 0, 5, 0);
                    repository.AddExp(index, step.ExperiencePerUnit);
                }
                foreach (var reward in step.Rewards)
                    foreach (var entry in reward.CharacterRewards)
                        foreach (int index in partyData.UnitIndices)
                            if (repository.TryGetUnit(index, out var unit) && unit.UnitTemplateKey == entry.UnitTemplateKey)
                                repository.ApplyEventRewardStats(index, 0, 0, 0, entry.Influence, 0);
                foreach (var token in step.Effects)
                    if (token.kind != DHEventEffectKind.Reward) effects.ExecuteToken(token);
                // 선택한 경로의 승리는 대체 적을 생성하지 않습니다. 반대 경로의 Disable은 기존 효과가 처리합니다.
                FindEvent(step.Route.EventKey)?.CompleteAfterEventBattleVictory();
                MapProgressRepository.Instance.MarkMainEventCompleted(step.Route.EventKey);
            }
            MoveRangeController.NotifyOccupancyChanged();
        }

        string BuildSummary()
        {
            var text = new StringBuilder();
            if (selected.Count == 0) text.AppendLine("추가 완료 처리 없음");
            else foreach (var route in selected) text.AppendLine("✓ " + route.Label);
            int exp = 0;
            foreach (var step in steps) exp += step.ExperiencePerUnit;
            text.AppendLine($"\n캐릭터별 추가 EXP {exp} · 승리 IP +{steps.Count * 5}");
            text.AppendLine("분기 보상 IP 증감도 적용 · 아놀드 EXP 제외");
            foreach (int index in partyData.UnitIndices)
            {
                if (!PersistentUnitRepository.Instance.TryGetUnit(index, out var data)) continue;
                int next = PersistentUnitRepository.SimulateFinalLevel(data, exp);
                string name = data.UnitTemplateKey;
                if (DHCsvTemplateCatalog.Instance.TryGetPlayerUnitTemplate(name, out var template)) name = template.UnitName;
                text.Append($"\n{name}: Lv.{data.Level} → {next}");
                if (int.TryParse(data.UnitTemplateKey, out int key))
                {
                    var skills = SkillUnlockResolver.GetUnlockCandidates(key, data.Level, next);
                    if (skills.Count > 0) text.Append($" · 스킬 해금/강화 {skills.Count}개");
                }
            }
            text.AppendLine("\n\n목적지 클릭 시 적용합니다. Esc / 취소는 변경 없이 종료합니다.");
            return text.ToString();
        }
    }
}
