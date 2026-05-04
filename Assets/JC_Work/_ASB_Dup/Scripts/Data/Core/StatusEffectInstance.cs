using System;

[Serializable]
public class StatusEffectInstance
{
    public StatusEffectType effectType = StatusEffectType.none;
    public StatusEffectCategory category = StatusEffectCategory.debuff;
    public float value = 0f;
    public int remainingTurns = 1;
    public BattleCharactor source;
}
