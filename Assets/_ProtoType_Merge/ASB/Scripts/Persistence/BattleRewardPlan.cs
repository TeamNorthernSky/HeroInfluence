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
    // 저장 없이 계산한 결과창 표시값. 기존 탐사 preview는 미설정 상태를 유지할 수 있다.
    public bool HasExpPreview;
    public int OldExp;
    public int OldMaxExp;
    public int NewExp;
    public int NewMaxExp;
    public int CurrentClassSkillId;
    public List<int> UnlockCandidateSkillIds = new List<int>();

    public float OldInfluence;
    public float NewInfluence;
    public float InfluenceDelta => NewInfluence - OldInfluence;

    public bool HasLevelUp => NewLevel > OldLevel;
    public bool HasSkillSelection => UnlockCandidateSkillIds != null && UnlockCandidateSkillIds.Count > 0;
}
