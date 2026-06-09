using System.Collections.Generic;

public class BattleRewardPlan
{
    public BattleResult Result;
    public List<UnitRewardPreview> UnitPreviews = new List<UnitRewardPreview>();
}

public class UnitRewardPreview
{
    public int UnitIndex;
    public string UnitName;
    public int OldLevel;
    public int NewLevel;
    public int GainedExp;

    public bool HasLevelUp => NewLevel > OldLevel;
}
