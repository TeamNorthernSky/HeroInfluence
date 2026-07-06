using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [KJ 260703] 전체 게임 상태 저장용 데이터 컨테이너.
/// 런타임에 ScriptableObject.CreateInstance로만 생성하고 애셋으로 저장하지 않는다.
/// JsonUtility.ToJson으로 save_slot_{i}.json에 기록되며,
/// hasData/savedAt 필드명은 SaveSlotData와 일치(타이틀 슬롯 UI 호환 — FromJson&lt;SaveSlotData&gt;가 그대로 동작).
/// </summary>
public class GameSaveData : ScriptableObject
{
    // ── 메타 (SaveSlotData 호환 필드명 유지) ──
    public bool hasData;
    public string savedAt;
    public int saveVersion = 1;

    // ── 유닛/적 (기존 디스크 DTO 재사용) ──
    public int nextUnitIndex;
    public List<UnitPersistentDataDiskRow> units = new List<UnitPersistentDataDiskRow>();
    public int nextEnemyUnitIndex;
    public List<EnemyUnitPersistentDataDiskRow> enemyUnits = new List<EnemyUnitPersistentDataDiskRow>();

    // ── 적 그룹 ──
    public int nextEnemySequence;
    public List<EnemyPersistentData> enemyGroups = new List<EnemyPersistentData>();

    // ── 무기 ──
    public int nextWeaponIndex;
    public List<WeaponPersistentData> weapons = new List<WeaponPersistentData>();

    // ── 파티 ──
    public List<PartyPersistentData> parties = new List<PartyPersistentData>();

    // ── 맵 진행도 ──
    public string mapId;
    public List<string> collectedItemKeys = new List<string>();
    public List<string> completedEventKeys = new List<string>();
    public List<PartyWorldState> partyWorldStates = new List<PartyWorldState>();
    public List<EnemyWorldState> enemyWorldStates = new List<EnemyWorldState>();
    public List<OutpostProgressState> outpostStates = new List<OutpostProgressState>();
    public List<FogProgressCell> fogCells = new List<FogProgressCell>();
    public List<LevelZoneSelectionState> levelZoneSelections = new List<LevelZoneSelectionState>();

    // ── 자원 (JsonUtility가 Dictionary 미지원 → 엔트리 배열) ──
    [Serializable]
    public struct ResourceEntry
    {
        public ResourceType type;
        public int amount;
    }
    public List<ResourceEntry> resources = new List<ResourceEntry>();

    // ── HQ 시설 ──
    [Serializable]
    public struct HQLevelEntry
    {
        public HQDepartment department;
        public int level;
    }
    public List<HQLevelEntry> hqLevels = new List<HQLevelEntry>();
    public bool hqUpgradedThisTurn;

    // ── 부서 매니저 상태 [KJ 260706] ──
    // 연구소: (유닛, 스킬)별 강화 레벨 — 비장착 스킬 레벨은 여기에만 존재
    public List<LabManager.SkillLevelEntry> labSkillLevels = new List<LabManager.SkillLevelEntry>();
    // 홍보 센터: 공유 예산 풀 + 마지막 충전 day
    public int publicityCurrentPool;
    public int publicityLastChargeDay;
    // 훈련소: 유닛별 공격/체력 강화 레벨(스탯 결과와 별개인 이력)
    public List<TrainingManager.TrainingEntry> trainingEntries = new List<TrainingManager.TrainingEntry>();
    // 의무실: 이번 턴 회복/부활 사용한 유닛
    public List<int> infirmaryHealedThisTurn = new List<int>();

    // ── 턴 ──
    public int currentDay;
}
