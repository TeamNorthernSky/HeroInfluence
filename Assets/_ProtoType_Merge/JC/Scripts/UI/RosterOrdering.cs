using System.Collections.Generic;

/// <summary>
/// [JC 260703] 로스터 정렬 단일 출처. HeroListController(편성)·LobbyRosterView(로비 뷰) 공용.
/// 순서 = 방문파티 멤버(파티 UnitIndices 순 = 진형순서 반영) → 비방문 파티 → 무소속.
/// 기존 HeroListController.ResolveOrderedUnits/AppendPartyUnits/IsUnitInAnyParty를 1:1 이관.
/// </summary>
public static class RosterOrdering
{
    // [JC 수정 260512] 머지 사이클: 파티 책임이 PartyPersistentRepository로 이관됨
    public static List<int> ResolveOrderedUnits(PersistentUnitRepository repo, HQVisitState visitState, bool visitingOnly)
    {
        List<int> ordered = new List<int>();
        HashSet<int> seen = new HashSet<int>();
        PartyPersistentRepository partyRepo = PartyPersistentRepository.Instance;

        // 1) 방문중 파티의 멤버 (파티 레지스트리 순)
        if (partyRepo != null && visitState != null && visitState.HasVisitingParty)
        {
            for (int p = 0; p < partyRepo.Parties.Count; p++)
            {
                PartyPersistentData party = partyRepo.Parties[p];
                if (party == null) continue;
                if (!visitState.IsPartyVisiting(party.PartyId)) continue;

                AppendPartyUnits(party, repo, ordered, seen);
            }
        }

        // 2) 비방문 파티 멤버 (파티 등록 순)
        // [JC 260615] 본부 상주 = 방문 파티(1) + 무소속(3, 본부 잔류). 비방문 파티(탐사 나간)만
        // visitingOnly에서 제외한다. 무소속(파티 미편성)은 항상 포함 — 출전에서 떼어낸 영웅 등.
        if (partyRepo != null && !visitingOnly)
        {
            for (int p = 0; p < partyRepo.Parties.Count; p++)
            {
                PartyPersistentData party = partyRepo.Parties[p];
                if (party == null) continue;
                if (visitState != null && visitState.IsPartyVisiting(party.PartyId)) continue;

                AppendPartyUnits(party, repo, ordered, seen);
            }
        }

        // [JC 260615] visitingOnly: 비방문 파티 멤버를 "무소속"으로 오인하지 않도록 전체 파티 소속 집합 구성.
        HashSet<int> allPartyMembers = null;
        if (visitingOnly && partyRepo != null)
        {
            allPartyMembers = new HashSet<int>();
            for (int p = 0; p < partyRepo.Parties.Count; p++)
            {
                PartyPersistentData party = partyRepo.Parties[p];
                if (party == null) continue;
                for (int u = 0; u < party.UnitIndices.Count; u++)
                    if (party.UnitIndices[u] > 0) allPartyMembers.Add(party.UnitIndices[u]);
            }
        }

        // 3) 무소속 유닛 (어느 파티에도 편성되지 않은 본부 잔류 영웅)
        for (int u = 0; u < repo.Units.Count; u++)
        {
            UnitPersistentData unit = repo.Units[u];
            if (unit == null || seen.Contains(unit.UnitIndex)) continue;
            if (allPartyMembers != null && allPartyMembers.Contains(unit.UnitIndex)) continue; // 비방문 파티 멤버 제외
            ordered.Add(unit.UnitIndex);
            seen.Add(unit.UnitIndex);
        }

        return ordered;
    }

    private static void AppendPartyUnits(PartyPersistentData party, PersistentUnitRepository repo, List<int> ordered, HashSet<int> seen)
    {
        for (int u = 0; u < party.UnitIndices.Count; u++)
        {
            int unitIndex = party.UnitIndices[u];
            if (unitIndex <= 0 || seen.Contains(unitIndex)) continue;
            if (!repo.ContainsUnit(unitIndex)) continue;
            ordered.Add(unitIndex);
            seen.Add(unitIndex);
        }
    }

    // [JC 260615] 깃발(파티 편성) 판정: 방문 여부 무관, 어느 파티든 UnitIndices에 있으면 true
    public static bool IsUnitInAnyParty(int unitIndex)
    {
        PartyPersistentRepository partyRepo = PartyPersistentRepository.Instance;
        if (partyRepo == null) return false;
        for (int p = 0; p < partyRepo.Parties.Count; p++)
        {
            PartyPersistentData party = partyRepo.Parties[p];
            if (party == null) continue;
            for (int u = 0; u < party.UnitIndices.Count; u++)
            {
                if (party.UnitIndices[u] == unitIndex) return true;
            }
        }
        return false;
    }
}
