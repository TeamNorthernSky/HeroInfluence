public enum StatusEffectType
{
    none,
    attack_up,
    attack_down,
    defense_up,
    defense_down,
    poison,
    bleed,
    taunt,
    stun,
    healBan,
    damage_taken_down = 11, // 방어 코어: 다음 자기 턴 시작까지 최종 피해 감소
    charging = 10,  // 신규: 2턴 충전 표시용 마커(로직은 읽지 않음). 스탯/스킵/DOT 무영향.
    guarded = 12    // 대신 맞기: source가 보호자, 1회 소비 또는 보호자의 다음 턴 시작에 만료.
}
