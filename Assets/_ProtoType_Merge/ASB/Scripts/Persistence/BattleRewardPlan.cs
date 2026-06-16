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
    public int CurrentClassSkillId;
    public List<int> UnlockCandidateSkillIds = new List<int>();

    public float OldInfluence;
    public float NewInfluence;
    public float InfluenceDelta => NewInfluence - OldInfluence;

    public bool HasLevelUp => NewLevel > OldLevel;
    public bool HasSkillSelection => UnlockCandidateSkillIds != null && UnlockCandidateSkillIds.Count > 0;
}
