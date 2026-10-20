using UnityEngine;

/// <summary>
/// 맞는 쪽(피격 유닛)의 짧은 점멸 이펙트. 기존 피격 이펙트는 공격 스킬이 정하므로 유닛 고유 반응은 여기서 낸다.
/// 체력이 줄어든 순간마다 지정 소켓 위치에 한 번 재생한다(기획 Y02 율리아 피격: 몸통에 짧은 점멸).
/// <para>
/// 소켓 하위에 붙이지 않는다 — 본 소켓은 스케일이 커서(예: 200) 이펙트 크기가 왜곡되고, 점멸은 짧아 따라갈 필요가 없다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BattleCharactor))]
public sealed class UnitHitFlashVfx : MonoBehaviour
{
    [SerializeField] private GameObject flashPrefab;
    [SerializeField] private UnitSocket socket = UnitSocket.Chest;

    private BattleCharactor _owner;
    private float _lastHp = -1f;

    private void OnEnable()
    {
        _owner = GetComponent<BattleCharactor>();
        _lastHp = _owner.CurrentHp;
        _owner.OnHpChanged += HandleHpChanged;
    }

    private void OnDisable()
    {
        if (_owner != null) _owner.OnHpChanged -= HandleHpChanged;
    }

    private void HandleHpChanged(float currentHp, float maxHp)
    {
        bool flash = ShouldFlash(_lastHp, currentHp);
        _lastHp = currentHp;
        if (!flash || flashPrefab == null) return;

        Transform anchor = ResolveSocket();
        GameObject instance = Instantiate(flashPrefab, anchor.position, Quaternion.identity);
        JC.VFX.VfxEffect effect = instance.GetComponent<JC.VFX.VfxEffect>();
        if (effect != null) effect.Play();
        else EffectFallbackRelease.Ensure(instance);
    }

    private Transform ResolveSocket()
    {
        UnitSocketHolder sockets = GetComponentInChildren<UnitSocketHolder>(true);
        Transform found = sockets != null ? sockets.GetNamedSocket(socket) : null;
        return found != null ? found : transform;
    }

    /// <summary>체력이 실제로 줄었을 때만(초기화·회복·부활 제외). 이전 값을 모르면(-1) 점멸하지 않는다.</summary>
    public static bool ShouldFlash(float previousHp, float currentHp)
    {
        return previousHp > 0f && currentHp < previousHp - 0.001f;
    }
}
