using System;
using System.IO;
using UnityEngine;

/// <summary>
/// [KJ 260703] 전체 게임 상태를 세이브 슬롯 파일로 기록하는 중앙 수집 서비스.
/// [KJ 260714] 저장 데이터는 DH 턴 시작 스냅샷(DHTurnStartSnapshotStore)에서 받아온다.
/// 일반 저장은 마지막 스냅샷을 기록한다. 전투 복귀 자동 저장만 결과 반영 직후 새 스냅샷을 만든다.
/// 후속 채팅이 있으면 첫 대사 실행 전 상태와 재개 정보를 함께 기록한다.
/// </summary>
public static class GameSaveService
{
#if UNITY_EDITOR
    // Editor verification records JSON without touching real save slots.
    internal static Func<int, string, bool> WriteOverride;
#endif
    /// <summary>전체 게임 상태를 save_slot_{slotIndex}.json으로 저장. 실패 시 false.</summary>
    public static bool SaveToSlot(int slotIndex)
        => SaveToSlot(slotIndex, null);

    /// <summary>Capture the committed battle outcome, not the earlier turn-start state.</summary>
    public static bool SaveBattleReturnToSlot(int slotIndex, GameSaveData.BattleReturnCheckpoint checkpoint)
    {
        if (TutorialSessionPolicy.BlocksSaving) return false;
        if (slotIndex < 0 || DHGameStateRestoreService.IsRestoring) return false;
        var store = DHTurnStartSnapshotStore.EnsureInstance();
        var section = BattleReturnSnapshotSection.Ensure(store);
        if (!store.CaptureFromRuntime()) return false;
        section.SetCheckpoint(checkpoint);
        return SaveToSlot(slotIndex, checkpoint);
    }

    private static bool SaveToSlot(int slotIndex, GameSaveData.BattleReturnCheckpoint checkpoint)
    {
        if (TutorialSessionPolicy.BlocksSaving)
        {
            Debug.Log("[GameSaveService] 튜토리얼에서는 저장하지 않습니다.");
            return false;
        }
        if (slotIndex < 0)
        {
            Debug.LogWarning("[GameSaveService] CurrentSlot 미설정 — 슬롯 0으로 폴백 저장합니다.");
            slotIndex = 0;
        }

        // [KJ 260714] 턴 시작 스냅샷이 없으면 저장 불가(재시도해도 무의미) — day1 최초 진입/턴 시작 전.
        // 자동 재캡처는 "턴 시작 시점" 의미를 깨므로 하지 않는다.
        DHTurnStartSnapshotStore snapshot = DHTurnStartSnapshotStore.Instance;
        if (snapshot == null || !snapshot.HasSnapshot)
        {
            Debug.LogWarning("[GameSaveService] 턴 시작 스냅샷 없음 — 저장을 건너뜁니다.");
            return false;
        }

        GameSaveData data = ScriptableObject.CreateInstance<GameSaveData>();
        try
        {
            if (!Capture(data, snapshot))
            {
                Debug.LogWarning($"[GameSaveService] 슬롯 {slotIndex} 저장 실패: 스냅샷 수집 실패.");
                return false;
            }

            // The snapshot section also preserves replay metadata for ordinary saves/retries.
            if (checkpoint != null) data.battleReturnCheckpoint = checkpoint;
            string path = Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");
            string json = JsonUtility.ToJson(data);
#if UNITY_EDITOR
            if (WriteOverride != null)
            {
                if (!WriteOverride(slotIndex, json)) return false;
            }
            else
#endif
                File.WriteAllText(path, json);
            Debug.Log($"[GameSaveService] 슬롯 {slotIndex} 저장 완료: {path}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameSaveService] 슬롯 {slotIndex} 저장 실패: {ex.Message}");
            return false;
        }
        finally
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(data);
            else UnityEngine.Object.DestroyImmediate(data);
        }
    }

    // [KJ 260714] 리포지토리 직접 수집(구 Capture* 메서드) → 턴 시작 스냅샷 채우기로 대체.
    // hasData/savedAt/saveVersion 및 모든 상태 섹션은 FillGameSaveData가 설정한다.
    private static bool Capture(GameSaveData data, DHTurnStartSnapshotStore snapshot)
    {
        if (!snapshot.FillGameSaveData(data))
            return false;

        // [KJ 260714, F008] 채팅 분기 플래그 — 스냅샷 미포함이라 별도 캡처(내용 확장 시 이 파일 무수정).
        return true;
    }
}
