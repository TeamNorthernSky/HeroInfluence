using System;
using System.Collections.Generic;

public enum DHEventRewardSourceType
{
    MainSub = 0,
    World = 1
}

[Serializable]
public sealed class DHEventRewardTemplate
{
    private readonly List<DHEventCharacterRewardEntry> characterRewards;

    public int RewardId { get; }
    public string RewardName { get; }
    public DHEventRewardSourceType SourceType { get; }
    public int Money { get; }
    public int Medal { get; }
    public int Gem { get; }
    public int Supply { get; }
    public int Exp { get; }
    public IReadOnlyList<DHEventCharacterRewardEntry> CharacterRewards => characterRewards;

    public DHEventRewardTemplate(
        int rewardId,
        string rewardName,
        DHEventRewardSourceType sourceType,
        int money,
        int medal,
        int gem,
        int supply,
        int exp,
        IEnumerable<DHEventCharacterRewardEntry> characterRewards)
    {
        RewardId = rewardId;
        RewardName = string.IsNullOrWhiteSpace(rewardName) ? string.Empty : rewardName.Trim();
        SourceType = sourceType;
        Money = money;
        Medal = medal;
        Gem = gem;
        Supply = supply;
        Exp = exp;
        this.characterRewards = characterRewards != null
            ? new List<DHEventCharacterRewardEntry>(characterRewards)
            : new List<DHEventCharacterRewardEntry>();
    }
}

[Serializable]
public readonly struct DHEventCharacterRewardEntry
{
    public string UnitTemplateKey { get; }
    public int MaxHp { get; }
    public int Atk { get; }
    public int Def { get; }
    public int Influence { get; }
    public int Heal { get; }

    public DHEventCharacterRewardEntry(
        string unitTemplateKey,
        int maxHp,
        int atk,
        int def,
        int influence,
        int heal)
    {
        UnitTemplateKey = string.IsNullOrWhiteSpace(unitTemplateKey) ? string.Empty : unitTemplateKey.Trim();
        MaxHp = maxHp;
        Atk = atk;
        Def = def;
        Influence = influence;
        Heal = heal;
    }
}
