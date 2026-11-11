using System.Collections.Generic;
using UnityEngine;

public class HeroUnionRegistry : MonoBehaviour
{
    public event System.Action Changed;
    // JC | 2026-11-10 | 기존 Changed 소비자를 유지하며 창문 갱신에 필요한 대상/재구축/등록부 수명 알림만 추가합니다.
    public event System.Action<HeroUnionUnit> HeroUnionRegistered;
    public event System.Action<HeroUnionUnit> HeroUnionUnregistered;
    public event System.Action<HeroUnionRegistry> Rebuilt;
    public static event System.Action<HeroUnionRegistry> LifecycleChanged;
    private readonly List<HeroUnionUnit> heroUnions = new List<HeroUnionUnit>();

    public IReadOnlyList<HeroUnionUnit> HeroUnions => heroUnions;

    private void Awake()
    {
        RefreshSceneHeroUnions();
    }

    // JC | 2026-11-10 | 등록부 지연 생성·교체 때만 구독을 재확인하도록 수명 변화를 알립니다.
    private void OnEnable() => LifecycleChanged?.Invoke(this);
    private void OnDisable() => LifecycleChanged?.Invoke(this);
    private void OnDestroy() => LifecycleChanged?.Invoke(this);

    public void Register(HeroUnionUnit heroUnion)
    {
        if (heroUnion == null || heroUnions.Contains(heroUnion))
            return;

        heroUnions.Add(heroUnion);
        Changed?.Invoke();
        // JC | 2026-11-10 | 기존 알림 뒤에 실제 추가된 협회만 전달합니다.
        HeroUnionRegistered?.Invoke(heroUnion);
    }

    public void Unregister(HeroUnionUnit heroUnion)
    {
        if (heroUnion == null)
            return;

        if (heroUnions.Remove(heroUnion))
        {
            Changed?.Invoke();
            // JC | 2026-11-10 | 비활성화 해제와 파괴를 구분하는 소비자가 대상을 확인할 수 있게 합니다.
            HeroUnionUnregistered?.Invoke(heroUnion);
        }
    }

    [ContextMenu("Refresh Scene HeroUnions")]
    public void RefreshSceneHeroUnions()
    {
        heroUnions.Clear();

        HeroUnionUnit[] sceneHeroUnions = FindObjectsByType<HeroUnionUnit>(FindObjectsSortMode.None);
        for (int i = 0; i < sceneHeroUnions.Length; i++)
        {
            HeroUnionUnit heroUnion = sceneHeroUnions[i];
            if (heroUnion == null)
                continue;

            Register(heroUnion);
        }
        Changed?.Invoke();
        // JC | 2026-11-10 | 결과가 비어 있어도 재구축 완료를 통지하고 기존 Changed 계약은 보존합니다.
        Rebuilt?.Invoke(this);
    }

    public bool TryGetByZoneId(string zoneId, out HeroUnionUnit heroUnion)
    {
        string normalizedZoneId = NormalizeZoneId(zoneId);
        heroUnion = null;

        for (int i = 0; i < heroUnions.Count; i++)
        {
            HeroUnionUnit candidate = heroUnions[i];
            if (candidate == null)
                continue;

            if (!string.Equals(candidate.ZoneId, normalizedZoneId, System.StringComparison.Ordinal))
                continue;

            heroUnion = candidate;
            return true;
        }

        return false;
    }

    public bool TryGetClaimedByZoneId(string zoneId, out HeroUnionUnit heroUnion)
    {
        if (TryGetByZoneId(zoneId, out heroUnion) && heroUnion != null && heroUnion.IsClaimedByHero)
            return true;

        heroUnion = null;
        return false;
    }

    public bool TryGetFirstClaimed(out HeroUnionUnit heroUnion)
    {
        heroUnion = null;

        for (int i = 0; i < heroUnions.Count; i++)
        {
            HeroUnionUnit candidate = heroUnions[i];
            if (candidate == null || !candidate.IsClaimedByHero)
                continue;

            heroUnion = candidate;
            return true;
        }

        return false;
    }

    public HeroUnionUnit GetClosestHeroUnion(Vector2Int grid)
    {
        HeroUnionUnit closest = null;
        int closestDistance = int.MaxValue;

        for (int i = 0; i < heroUnions.Count; i++)
        {
            HeroUnionUnit heroUnion = heroUnions[i];
            if (heroUnion == null)
                continue;

            int distance = GridManager.GridDistance(grid, heroUnion.GetCurrentGrid());
            if (distance >= closestDistance)
                continue;

            closest = heroUnion;
            closestDistance = distance;
        }

        return closest;
    }

    public HeroUnionUnit GetClosestClaimedHeroUnion(Vector2Int grid)
    {
        HeroUnionUnit closest = null;
        int closestDistance = int.MaxValue;

        for (int i = 0; i < heroUnions.Count; i++)
        {
            HeroUnionUnit heroUnion = heroUnions[i];
            if (heroUnion == null || !heroUnion.IsClaimedByHero)
                continue;

            int distance = GridManager.GridDistance(grid, heroUnion.GetCurrentGrid());
            if (distance >= closestDistance)
                continue;

            closest = heroUnion;
            closestDistance = distance;
        }

        return closest;
    }

    private static string NormalizeZoneId(string zoneId)
    {
        string normalized = MapProgressKey.NormalizeSegment(zoneId);
        return string.IsNullOrWhiteSpace(normalized) ? "zone_001" : normalized;
    }
}
