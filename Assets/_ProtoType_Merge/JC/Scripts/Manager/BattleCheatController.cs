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
            Debug.Log($"[BattleCheat] Shift+{(enemySide ? "9" : "0")} 발동. {(enemySide ? "적" : "아군")} {killed}체 사망. 다음 턴 자동 판정 대기.");
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
