using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JC.BattleTesting
{
    public sealed partial class JcBattleTestSession
    {
        [Header("인카운터 배경")]
        [Tooltip("탐사 배경을 표시하는 동안 숨길 기존 임시 배경 루트입니다. 카메라·조명·UI·그리드는 지정하지 않습니다.")]
        public GameObject[] temporaryBackgroundRoots = Array.Empty<GameObject>();
        private readonly Dictionary<GameObject, bool> temporaryBackgroundStates = new Dictionary<GameObject, bool>();
        private GameObject encounterBackgroundInstance;
        private GameObject pendingDynamicBackground;
        public string DynamicBackgroundReport => encounterBackgroundInstance != null ? encounterBackgroundInstance.GetComponent<JcDynamicBattleOutput>()?.Report : null;

        private void PrepareDynamicBackground()
        {
            ClearPendingDynamicBackground();
            if(settings.enemyPreset==null||!settings.enemyPreset.dynamicBackground)return;
            cameraDraftA.Resolve(CameraFramingCenter,out var cameraA,out _,out _);
            cameraDraftB.Resolve(CameraFramingCenter,out var cameraB,out _,out _);
            pendingDynamicBackground=JcDynamicBattleGeometry.Build(settings.dynamicBackground.source,settings.dynamicBackground,transform,new[]{cameraA,cameraB});
            pendingDynamicBackground.SetActive(false);
        }
        private void ClearPendingDynamicBackground()
        {
            if(pendingDynamicBackground!=null){pendingDynamicBackground.SetActive(false);Destroy(pendingDynamicBackground);pendingDynamicBackground=null;}
        }
        private GameObject ownedEventCatalog;
        private EnemySpawnPlan preparedEventPlan;

        private void EnsureEventCatalog()
        {
            if (settings.enemyPreset == null || !settings.enemyPreset.IsEventBattle) return;
            if (EventScriptCatalog.Instance != null)
            {
                if (ownedEventCatalog == null || EventScriptCatalog.Instance.gameObject != ownedEventCatalog)
                    throw new InvalidOperationException("다른 씬의 이벤트 카탈로그가 남아 있습니다. 테스트씬을 직접 실행해 주세요.");
                return;
            }
            var source = Resources.Load<GameObject>("JC_Encounters/EventCatalog");
            if (source == null) throw new InvalidOperationException("탐사 인카운터 생성 메뉴를 먼저 실행해 주세요. 이벤트 카탈로그 연결이 없습니다.");
            // 원본 컴포넌트와 SO 참조를 재사용합니다. 프리팹은 DontDestroyOnLoad를 꺼서 저장소와 분리합니다.
            ownedEventCatalog = Instantiate(source, transform);
            ownedEventCatalog.SetActive(true);
        }

        private void ValidateEventPreset(JcEnemyPreset preset)
        {
            if (preset.enemyLevel < 1 || preset.eventZoneId < 1 || string.IsNullOrWhiteSpace(preset.encounterKey))
                throw new InvalidOperationException("인카운터 키·구역·레벨을 확인해 주세요.");
            if(preset.dynamicBackground)
            {
                if(settings.dynamicBackground.source==null)settings.dynamicBackground.source=Resources.Load<JcDynamicBattleSource>("JC_Encounters/DynamicSource");
                JcDynamicBattleGeometry.Validate(settings.dynamicBackground.source,settings.dynamicBackground);
            }
            else
            {
            if (preset.background == null || preset.background.visualPrefab == null)
                throw new InvalidOperationException(preset.encounterKey + ": 추출 배경이 없습니다.");
            if (preset.background.fitBattleRoad && preset.background.fittedVisualPrefab == null)
                throw new InvalidOperationException(preset.encounterKey + ": 도로 보정 배경을 생성해 주세요.");
            if (preset.background.encounterKey != preset.encounterKey)
                throw new InvalidOperationException("전투와 배경의 인카운터 키가 다릅니다.");
            }
            if (!Application.isPlaying) return; // Edit Mode에서는 싱글톤을 바꾸지 않습니다. 추출 도구에서 데이터별 검증을 수행합니다.
            if (!EnemySpawnPlanBuilder.TryBuildFromEventBattleKey(preset.eventZoneId, preset.eventBattleKey, preset.enemyLevel, out preparedEventPlan, out var error))
                throw new InvalidOperationException(error);
            if (preparedEventPlan.Scenario != null && preparedEventPlan.Scenario.ScenarioType != BattleScenarioType.None)
                throw new InvalidOperationException("현재 고정 인카운터 테스트는 일반 섬멸전만 지원합니다. 특수 시나리오를 무시하고 시작하지 않습니다.");
            if (preparedEventPlan.Count == 0 || preparedEventPlan.Entries.Select(x => x.CombatSlot).Distinct().Count() != preparedEventPlan.Count)
                throw new InvalidOperationException("적 슬롯이 비어 있거나 중복되었습니다.");
            foreach (var entry in preparedEventPlan.Entries)
            {
                if (!enemySlots.TryResolve(entry.CombatSlot, out _)) throw new InvalidOperationException("적 슬롯 오류: " + entry.CombatSlot);
                var prefab = ResolveEventEnemyPrefab(entry);
                if (prefab == null || prefab.GetComponent<BattleCharactor>() == null)
                    throw new InvalidOperationException("기존 전투 프리팹 연결 누락: " + entry.Data.Index);
                if (!entry.HasExplicitSkills) throw new InvalidOperationException("이벤트 스킬 누락: " + entry.Data.Index);
            }
        }

        private GameObject ResolveEventEnemyPrefab(EnemySpawnEntry entry)
        {
            // 본게임 EnemySpawner와 같은 우선순위: 공유 카탈로그 인덱스 → 명시 Resources 경로.
            if (unitPrefabs.TryGetEnemyPrefab(entry.Data.Index, out var prefab)) return prefab;
            if (unitPrefabs.TryGetEnemyPrefab(entry.PrefabKey, out prefab)) return prefab;
            return entry.PrefabKey.Contains("/") ? Resources.Load<GameObject>(entry.PrefabKey) : null;
        }

        private void SpawnEventEnemy(EnemySpawnEntry entry)
        {
            enemySlots.TryResolve(entry.CombatSlot, out var slot);
            var go = Instantiate(ResolveEventEnemyPrefab(entry), slot.WorldPosition, slot.WorldRotation * Quaternion.Euler(0, 180, 0), slot.Cell.transform);
            go.name = appliedPreset.encounterKey + "_" + entry.Data.Name;
            ReplaceInstanceComponent<EnemyScript, JcEnemyScript>(go);
            if (go.GetComponent<BossController>() != null) ReplaceInstanceComponent<BossController, JcBossController>(go);
            if (go.GetComponent<EncounterParticipant>() != null) ReplaceInstanceComponent<EncounterParticipant, JcEncounterParticipant>(go);
            foreach (var legacy in go.GetComponentsInChildren<CharactorScript>(true)) DestroyImmediate(legacy);
            var actor = go.GetComponent<BattleCharactor>();
            actor.IsPlayer = false;
            go.GetComponent<JcEnemyScript>().Initialize(entry.Data);
            actor.BindPersistentEnemySourceData(new EnemyUnitPersistentData(810000 + entry.CombatSlot, entry.SourceUnitKey,
                appliedPreset.enemyLevel, entry.Data.baseStats, entry.Data.baseStats, entry.Data.baseStats.HP, 100));
            actor.SetTemplateIndex(entry.Data.Index); actor.SetDisplayName(entry.Data.Name);
            actor.availableSkills = entry.ExplicitSkills.Select(JcTestDataAssembler.Copy).ToList();
            actor.SetClassSkillIndex(actor.availableSkills[0].skillIndex); actor.ResolveSelectedSkill(false);
            actor.MarkInitializedFromDataPipeline(true);
            actor.AssignToCell(slot.Cell);
            execution.ApplyBattleSpeedToUnit(actor);
            runtimeUnits.Add(actor); unitSlots[actor] = entry.CombatSlot;
        }

        private void ApplyEncounterBackground(JcEncounterBackground background)
        {
            RestoreEncounterBackground();
            if(appliedPreset!=null&&appliedPreset.dynamicBackground)
            {
                encounterBackgroundInstance=pendingDynamicBackground;pendingDynamicBackground=null;
                if(encounterBackgroundInstance==null)throw new InvalidOperationException("동적 배경 준비 결과가 없습니다.");
                encounterBackgroundInstance.SetActive(true);
            }
            else
            {
                if (background == null) return;
                encounterBackgroundInstance = background.CreateInstance(transform);
            }
            foreach (var root in temporaryBackgroundRoots)
                if (root != null)
                {
                    temporaryBackgroundStates[root] = root.activeSelf;
                    root.SetActive(false);
                }
        }

        private void RestoreEncounterBackground()
        {
            if (encounterBackgroundInstance != null)
            {
                encounterBackgroundInstance.SetActive(false);
                Destroy(encounterBackgroundInstance);
            }
            foreach (var entry in temporaryBackgroundStates) if (entry.Key != null) entry.Key.SetActive(entry.Value);
            temporaryBackgroundStates.Clear();
        }
    }
}
