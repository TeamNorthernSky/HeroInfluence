using UnityEngine;

// [JC 신설 260513] 전투 종료 후 적 인스턴스 처리 정책.
// 이벤트/기획 시스템이 CombatContext.SetEnemyDirective로 주입.
// 없으면 BattleResult 기반 기본 정책 적용.
public enum PostCombatEnemyAction
{
    KeepInPlace,        // 위치 유지 (Defeat / Escape / 자연 종료 기본)
    RemoveFromPool,     // 풀에서 영구 제거 (Victory 기본)
    MoveToGrid,         // 다른 위치로 이동 (도주·텔레포트 등)
    ReplaceWithPrefab,  // prefab 교체 (강화 페이즈 등)
}

[System.Serializable]
public class PostCombatEnemyDirective
{
    public PostCombatEnemyAction Action;
    public Vector2Int TargetGrid;
    public string PrefabKey;

    public static PostCombatEnemyDirective DefaultFor(BattleResult result)
    {
        return result == BattleResult.Victory
            ? new PostCombatEnemyDirective { Action = PostCombatEnemyAction.RemoveFromPool }
            : new PostCombatEnemyDirective { Action = PostCombatEnemyAction.KeepInPlace };
    }
}
