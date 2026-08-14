using UnityEngine;

// 2턴 충전(기모으기) 예약 + 에너지(응축) 상태. BattleCharactor가 단일 진실원본.
// - charging(StatusEffectType)은 표시용 마커 전용. 로직은 IsCharging(예약 페이로드)로 판정한다.
// - 전투 시작 리셋은 BattleCharactor.PersistenceEquipment.cs의 MarkInitializedFromDataPipeline에서 수행.
public partial class BattleCharactor
{
    // ── 에너지(응축) ─────────────────────────────
    public int EnergyStack { get; private set; }
    public void AddEnergyStack(int amount = 1) { EnergyStack = Mathf.Max(0, EnergyStack + amount); }
    public void ResetEnergyStack() { EnergyStack = 0; }

    // ── 2턴 충전(기모으기) 예약: 이 유닛이 단일 소유(진실원본) ─────────────
    private bool hasCharge;
    private Vector2Int reservedChargeCoords;      // 대상 '타일' 고정(유닛 추적 아님)
    private SkillData reservedChargeSkill;
    private const int ChargingMarkerTurns = 99;   // 엔진 자동감소 방지 센티넬. 로직은 이 마커를 읽지 않음.

    public bool IsCharging => hasCharge;          // 진실원본(마커 아님)
    public SkillData ReservedChargeSkill => reservedChargeSkill;

    // ── 포탑 사이클: 발사 직후 1턴 휴식 예약 ─────────────
    private bool pendingRest;
    public bool HasPendingRest => pendingRest;
    public void SetPendingRest(bool value) { pendingRest = value; }

    public void BeginCharge(SkillData skill, Vector2Int targetCoords)
    {
        hasCharge = true;
        reservedChargeSkill = skill;
        reservedChargeCoords = targetCoords;
        ApplyStatusEffect(new StatusEffectInstance
        {
            effectType = StatusEffectType.charging,
            category = StatusEffectCategory.debuff,
            value = 0f,
            remainingTurns = ChargingMarkerTurns,
            source = this
        });
    }

    public void ClearCharge()   // 소비/취소/리셋 공용 단일 API
    {
        hasCharge = false;
        reservedChargeSkill = null;
        RemoveStatusEffect(StatusEffectType.charging);
    }

    /// <summary>고정 타일의 현재 점유자(비었으면 null). 재타게팅하지 않는다.</summary>
    public BattleCharactor ResolveChargeOccupant()
    {
        if (!hasCharge)
        {
            return null;
        }

        ASB.Work.BattleGrid.BattleGridManager gm = ASB.Work.BattleGrid.BattleGridManager.Instance;
        if (gm != null && gm.TryGetCell(reservedChargeCoords, out ASB.Work.BattleGrid.GridCell cell) && cell != null)
        {
            return cell.OccupyingUnit;
        }

        return null;
    }
}
