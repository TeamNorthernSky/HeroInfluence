using System.Collections.Generic;

/// <summary>
/// [JC 260628] 출전 진형 ↔ 전투 슬롯 ↔ 표시 순서의 단일 규약(공용 유틸).
///
/// • 전투 슬롯(1-base): 1·2·3 = 후열(back), 4·5·6 = 전열(front).
///   (PlayerSpawner 기준: Grid_0_x = 후열, Grid_1_x = 전열)
/// • 출전 진형 grid(2×3): 0·1·2 = 전열 프레임, 3·4·5 = 후열 프레임.
/// • "표시 순서"(전력평가 / 탐사 HeroBtn / 전투 결과)는 전열(슬롯4·5·6) → 후열(슬롯1·2·3)
///   = 진형 grid 인덱스 오름차순으로 통일한다.
///
/// 이 규약을 쓰는 곳: SortieController(JC), CombatPromptService(DH),
/// ExplorationHeroBoxController(JC), BattleResultPanel(KJ).
/// 슬롯/순서 규약을 바꿀 때는 여기 한 곳만 고치면 된다.
/// </summary>
public static class PartyFormation
{
    public const int SlotCount = 6;

    /// <summary>진형 grid 인덱스 → 전투 슬롯(1-base). 전열 grid0-2→슬롯4-6, 후열 grid3-5→슬롯1-3.</summary>
    public static int GridToSlot(int grid) => grid < 3 ? grid + 4 : grid - 2;

    /// <summary>전투 슬롯(1-base) → 진형 grid 인덱스. GridToSlot의 역함수.</summary>
    public static int SlotToGrid(int slot) => slot >= 4 ? slot - 4 : slot + 2;

    /// <summary>slot-resolved 희소 배열(index = 슬롯-1)을 전열→후열 순으로 압축(빈칸 제거).
    /// 입력 예: CombatContext.CombatParty.UnitIndices (전력평가·전투결과).</summary>
    public static List<int> PackFrontFirst(IReadOnlyList<int> slotArray)
    {
        var result = new List<int>(SlotCount);
        for (int g = 0; g < SlotCount; g++)
        {
            int idx = GridToSlot(g) - 1; // grid 순(front-first) → 배열 index
            if (slotArray != null && idx >= 0 && idx < slotArray.Count && slotArray[idx] > 0)
                result.Add(slotArray[idx]);
        }
        return result;
    }

    /// <summary>병렬 (unitIndices, unitSlots)를 전열→후열 순으로 정렬한 unitIndex 리스트(빈칸 제거).
    /// 입력 예: PartyPersistentData.UnitIndices + UnitSlots (탐사 HeroBtn).
    /// unitSlots가 없으면 위치+1을 슬롯으로 간주(레거시 폴백).</summary>
    public static List<int> OrderFrontFirst(IReadOnlyList<int> unitIndices, IReadOnlyList<int> unitSlots)
    {
        var result = new List<int>(SlotCount);
        if (unitIndices == null) return result;
        for (int g = 0; g < SlotCount; g++)
        {
            int slot = GridToSlot(g);
            for (int i = 0; i < unitIndices.Count; i++)
            {
                int unit = unitIndices[i];
                if (unit <= 0) continue;
                int s = (unitSlots != null && i < unitSlots.Count) ? unitSlots[i] : (i + 1);
                if (s == slot) { result.Add(unit); break; }
            }
        }
        return result;
    }
}
