using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// [KJ 260703] 전체 게임 상태를 세이브 슬롯 파일로 기록하는 중앙 수집 서비스.
/// 각 싱글톤에서 상태를 당겨와 GameSaveData(SO)를 채우고 JsonUtility로 저장한다.
/// 싱글톤이 null인 구역은 비운 채 경고만 남기고 저장은 계속한다(부분 저장 허용).
/// </summary>
public static class GameSaveService
{
    /// <summary>전체 게임 상태를 save_slot_{slotIndex}.json으로 저장. 실패 시 false.</summary>
    public static bool SaveToSlot(int slotIndex)
    {
        if (slotIndex < 0)
        {
            Debug.LogWarning("[GameSaveService] CurrentSlot 미설정 — 슬롯 0으로 폴백 저장합니다.");
            slotIndex = 0;
        }

        GameSaveData data = ScriptableObject.CreateInstance<GameSaveData>();
        try
        {
            Capture(data);
            string path = Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");
            File.WriteAllText(path, JsonUtility.ToJson(data));
            Debug.Log($"[GameSaveService] 슬롯 {slotIndex} 저장 완료: {path}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameSaveService] 슬롯 {slotIndex} 저장 실패: {ex.Message}");
            // [KJ 260708] 자동 저장 실패를 유저에게 알리고 재시도 기회 제공(예=재저장/아니요=닫기).
            SaveRetryModal.Show(slotIndex);
            return false;
        }
        finally
        {
            UnityEngine.Object.Destroy(data);
        }
    }

    private static void Capture(GameSaveData data)
    {
        data.hasData = true;
        data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        data.saveVersion = 1;

        CaptureUnits(data);
        CaptureEnemyUnits(data);
        CaptureEnemyGroups(data);
        CaptureWeapons(data);
        CaptureParties(data);
        CaptureMapProgress(data);
        CaptureEconomy(data);
        CaptureHqAndDay(data);
        CaptureDepartments(data);
    }

    private static void CaptureUnits(GameSaveData data)
    {
        PersistentUnitRepository repo = PersistentUnitRepository.Instance;
        if (repo == null)
        {
            Debug.LogWarning("[GameSaveService] PersistentUnitRepository 없음 — 유닛 구역 생략");
            return;
        }

        data.nextUnitIndex = repo.NextUnitIndex;
        for (int i = 0; i < repo.Units.Count; i++)
        {
            UnitPersistentDataDiskRow row = UnitPersistentDataDiskRow.From(repo.Units[i]);
            if (row != null) data.units.Add(row);
        }
    }

    private static void CaptureEnemyUnits(GameSaveData data)
    {
        PersistentEnemyRepository repo = PersistentEnemyRepository.Instance;
        if (repo == null)
        {
            Debug.LogWarning("[GameSaveService] PersistentEnemyRepository 없음 — 적 유닛 구역 생략");
            return;
        }

        data.nextEnemyUnitIndex = repo.NextUnitIndex;
        for (int i = 0; i < repo.Units.Count; i++)
        {
            EnemyUnitPersistentDataDiskRow row = EnemyUnitPersistentDataDiskRow.From(repo.Units[i]);
            if (row != null) data.enemyUnits.Add(row);
        }
    }

    private static void CaptureEnemyGroups(GameSaveData data)
    {
        EnemyGroupPersistentRepository repo = EnemyGroupPersistentRepository.Instance;
        if (repo == null)
        {
            Debug.LogWarning("[GameSaveService] EnemyGroupPersistentRepository 없음 — 적 그룹 구역 생략");
            return;
        }

        data.nextEnemySequence = repo.NextEnemySequence;
        data.enemyGroups.AddRange(repo.Enemies);
    }

    private static void CaptureWeapons(GameSaveData data)
    {
        WeaponPersistentRepository repo = WeaponPersistentRepository.Instance;
        if (repo == null)
        {
            Debug.LogWarning("[GameSaveService] WeaponPersistentRepository 없음 — 무기 구역 생략");
            return;
        }

        data.nextWeaponIndex = repo.NextWeaponIndex;
        data.weapons.AddRange(repo.Weapons);
    }

    private static void CaptureParties(GameSaveData data)
    {
        PartyPersistentRepository repo = PartyPersistentRepository.Instance;
        if (repo == null)
        {
            Debug.LogWarning("[GameSaveService] PartyPersistentRepository 없음 — 파티 구역 생략");
            return;
        }

        data.parties.AddRange(repo.Parties);
    }

    private static void CaptureMapProgress(GameSaveData data)
    {
        MapProgressRepository repo = MapProgressRepository.Instance;
        if (repo == null)
        {
            Debug.LogWarning("[GameSaveService] MapProgressRepository 없음 — 맵 진행도 구역 생략");
            return;
        }

        data.mapId = repo.MapId;
        data.collectedItemKeys.AddRange(repo.CollectedItemKeys);
        data.completedEventKeys.AddRange(repo.CompletedEventKeys);
        data.partyWorldStates.AddRange(repo.PartyWorldStates);
        data.enemyWorldStates.AddRange(repo.EnemyWorldStates);
        data.outpostStates.AddRange(repo.OutpostStates);
        data.fogCells.AddRange(repo.FogCells);
        data.levelZoneSelections.AddRange(repo.LevelZoneSelections);
        data.heroUnionStates.AddRange(repo.HeroUnionStates);
        data.gateStates.AddRange(repo.GateStates);
        data.zoneThreatStates.AddRange(repo.ZoneThreatStates);
    }

    private static void CaptureEconomy(GameSaveData data)
    {
        EconomyManager economy = GameManager.Instance != null ? GameManager.Instance.Economy : null;
        if (economy == null)
        {
            Debug.LogWarning("[GameSaveService] EconomyManager 없음 — 자원 구역 생략");
            return;
        }

        foreach (KeyValuePair<ResourceType, int> pair in economy.GetAll())
            data.resources.Add(new GameSaveData.ResourceEntry { type = pair.Key, amount = pair.Value });
    }

    private static void CaptureHqAndDay(GameSaveData data)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogWarning("[GameSaveService] GameManager 없음 — HQ/턴 구역 생략");
            return;
        }

        data.currentDay = gm.CurrentDay;

        HQStateManager hq = gm.HQ;
        if (hq == null)
        {
            Debug.LogWarning("[GameSaveService] HQStateManager 없음 — HQ 구역 생략");
            return;
        }

        foreach (HQDepartment d in Enum.GetValues(typeof(HQDepartment)))
            data.hqLevels.Add(new GameSaveData.HQLevelEntry { department = d, level = hq.GetLevel(d) });
        data.hqUpgradedThisTurn = hq.UpgradedThisTurn;
    }

    // [KJ 260706] 부서 매니저 내부 상태 — 결과가 유닛/무기에 반영되지 않는 것들만.
    private static void CaptureDepartments(GameSaveData data)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null)
            return; // CaptureHqAndDay에서 이미 경고

        if (gm.Lab != null)
            data.labSkillLevels.AddRange(gm.Lab.Entries);
        else
            Debug.LogWarning("[GameSaveService] LabManager 없음 — 스킬 강화 구역 생략");

        if (gm.Publicity != null)
        {
            data.publicityCurrentPool = gm.Publicity.CurrentPool;
            data.publicityLastChargeDay = gm.Publicity.LastChargeDay;
        }
        else
            Debug.LogWarning("[GameSaveService] PublicityManager 없음 — 홍보 구역 생략");

        if (gm.Training != null)
            data.trainingEntries.AddRange(gm.Training.Entries);
        else
            Debug.LogWarning("[GameSaveService] TrainingManager 없음 — 훈련 구역 생략");

        if (gm.Infirmary != null)
            data.infirmaryHealedThisTurn.AddRange(gm.Infirmary.HealedUnitsThisTurn);
        else
            Debug.LogWarning("[GameSaveService] InfirmaryManager 없음 — 의무실 구역 생략");

        // [KJ 260706] 공방 보유 매핑 — 무기 인스턴스/장착과 별개인 소유권 정보.
        if (gm.Workshop != null)
            data.workshopWeaponEntries.AddRange(gm.Workshop.Entries);
        else
            Debug.LogWarning("[GameSaveService] WorkshopManager 없음 — 공방 보유 구역 생략");

        // [KJ 260706] 거점 방문 상태 — 복원은 탐사씬 한정(DH 협의).
        HQVisitState visit = HQVisitState.Instance;
        if (visit != null)
        {
            foreach (KeyValuePair<string, HashSet<string>> pair in visit.VisitingBySource)
                data.hqVisitSources.Add(new GameSaveData.VisitEntry
                {
                    source = pair.Key,
                    partyIds = new List<string>(pair.Value)
                });
        }
        else
            Debug.LogWarning("[GameSaveService] HQVisitState 없음 — 방문 상태 구역 생략");
    }
}
