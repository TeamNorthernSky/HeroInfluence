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
    charging   // 신규: 2턴 충전 표시용 마커(로직은 읽지 않음). 스탯/스킵/DOT 무영향.
}
