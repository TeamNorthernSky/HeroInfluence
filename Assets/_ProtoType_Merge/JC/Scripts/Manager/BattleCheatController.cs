using UnityEngine;

/// <summary>
/// [JC 신설 260512] 전투씬 치트키 컨트롤러.
/// Shift+9: 모든 적 BattleCharactor 즉시 사망 (다음 턴 종료 시 BattleFlowManager가 Victory 판정)
/// Shift+0: 모든 아군 BattleCharactor 즉시 사망 (다음 턴 종료 시 Defeat 판정)
/// 적·아군 구분: BattleCharactor 부모 체인에서 PlayerPlace/EnemyPlace 이름으로 결정.
/// </summary>
[DisallowMultipleComponent]
public class BattleCheatController : MonoBehaviour
{
    [SerializeField] private bool logCheat = true;

    private void Update()
    {
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)) return;

        if (Input.GetKeyDown(KeyCode.Alpha9)) KillAll(enemySide: true);
        else if (Input.GetKeyDown(KeyCode.Alpha0)) KillAll(enemySide: false);
    }

    private void KillAll(bool enemySide)
    {
        int killed = 0;
        var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var mb = all[i];
            if (mb == null) continue;
            if (mb.GetType().FullName != "BattleCharactor") continue;

            if (!IsOnSide(mb.transform, enemySide)) continue;

            var isDeadProp = mb.GetType().GetProperty("IsDead",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (isDeadProp != null && isDeadProp.GetValue(mb) is bool dead && dead) continue;

            var maxHpProp = mb.GetType().GetProperty("MaxHp",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            float maxHp = 99999f;
            if (maxHpProp != null && maxHpProp.GetValue(mb) is float mhp) maxHp = Mathf.Max(mhp, 1f);

            var takeDamage = mb.GetType().GetMethod("TakeDamage",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (takeDamage != null)
            {
                takeDamage.Invoke(mb, new object[] { maxHp });
                killed++;
            }
        }

        if (logCheat)
            Debug.Log($"[BattleCheat] Shift+{(enemySide ? "9" : "0")} 발동. {(enemySide ? "적" : "아군")} {killed}체 사망.");

        // [JC 260616] 치트 사망은 BattleManager 정상 데미지 파이프라인을 우회(TakeDamage 직접 호출)하므로,
        // 턴 경계 밖(적 행동 중 / 플레이어 행동 resolved 직후 등)에서 죽이면 BattleFlowManager가
        // 승리·패배 판정 체크포인트(GetNextUnit==null / TryEndBattleImmediately)를 놓쳐 전투가 동결될 수 있다.
        // 사망(IsDead) 반영 직후 강제로 종료 평가를 한 번 트리거해 보강한다. (ASB 코어 비침습 — reflection)
        if (killed > 0)
            StartCoroutine(ForceBattleEndEvaluationNextFrame());
    }

    /// <summary>
    /// 한 프레임 양보(사망 OnDied 정리 대기) 후 BattleFlowManager의 종료 평가를 강제 호출한다.
    /// 이미 한쪽 진영이 전멸(IsBattleOver)이면 내부적으로 CompleteBattle → OnBattleEnded가 발화되어
    /// 정상 종료/씬 복귀 흐름을 탄다. 전멸이 아니면 아무 일도 하지 않는다(가드 내장).
    /// </summary>
    private System.Collections.IEnumerator ForceBattleEndEvaluationNextFrame()
    {
        yield return null;

        var bfm = FindBattleFlowManager();
        if (bfm == null)
        {
            if (logCheat) Debug.LogWarning("[BattleCheat] BattleFlowManager 미발견 — 강제 종료 평가 생략");
            yield break;
        }

        const System.Reflection.BindingFlags flags =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public;

        var method = bfm.GetType().GetMethod("TryEndBattleImmediately", flags, null, System.Type.EmptyTypes, null);
        if (method != null)
        {
            object ended = method.Invoke(bfm, null);
            if (logCheat) Debug.Log($"[BattleCheat] 강제 종료 평가 트리거 → battleEnded={ended}");
        }
        else if (logCheat)
        {
            Debug.LogWarning("[BattleCheat] BattleFlowManager.TryEndBattleImmediately 미발견 — 강제 종료 평가 실패");
        }
    }

    private static MonoBehaviour FindBattleFlowManager()
    {
        var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var mb = all[i];
            if (mb != null && mb.GetType().FullName == "BattleFlowManager") return mb;
        }
        return null;
    }

    private static bool IsOnSide(Transform t, bool enemySide)
    {
        Transform cur = t.parent;
        while (cur != null)
        {
            if (enemySide && cur.name == "EnemyPlace") return true;
            if (!enemySide && cur.name == "PlayerPlace") return true;
            cur = cur.parent;
        }
        return false;
    }
}
