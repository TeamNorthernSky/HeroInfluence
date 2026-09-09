public enum DHWorldEventStatusType
{
    CurrentIP = 0,
    CurrentHP = 1,
    MaxIP = 2,
    MaxHP = 3,
    Atk = 4
}

public enum DHWorldEventRewardType
{
    CurrentIP = 0,
    Money = 1,
    Crystal = 2,
    Medal = 3,
    Supply = 4
}

public static class DHWorldEventCodeMap
{
    public static bool TryGetResourceType(int code, out ResourceType resourceType)
    {
        switch (code)
        {
            case 0:
                resourceType = ResourceType.Money;
                return true;
            case 1:
                resourceType = ResourceType.Chip;
                return true;
            case 2:
                resourceType = ResourceType.Crystal;
                return true;
            case 3:
                resourceType = ResourceType.Supply;
                return true;
            default:
                resourceType = default;
                return false;
        }
    }

    public static bool TryGetStatusType(int code, out DHWorldEventStatusType statusType)
    {
        switch (code)
        {
            case 0:
                statusType = DHWorldEventStatusType.CurrentIP;
                return true;
            case 1:
                statusType = DHWorldEventStatusType.CurrentHP;
                return true;
            case 2:
                statusType = DHWorldEventStatusType.MaxIP;
                return true;
            case 3:
                statusType = DHWorldEventStatusType.MaxHP;
                return true;
            case 4:
                statusType = DHWorldEventStatusType.Atk;
                return true;
            default:
                statusType = default;
                return false;
        }
    }

    public static bool TryGetRewardType(int code, out DHWorldEventRewardType rewardType)
    {
        switch (code)
        {
            case 0:
                rewardType = DHWorldEventRewardType.CurrentIP;
                return true;
            case 1:
                rewardType = DHWorldEventRewardType.Money;
                return true;
            case 2:
                rewardType = DHWorldEventRewardType.Crystal;
                return true;
            case 3:
                rewardType = DHWorldEventRewardType.Medal;
                return true;
            case 4:
                rewardType = DHWorldEventRewardType.Supply;
                return true;
            default:
                rewardType = default;
                return false;
        }
    }

    public static bool TryGetRewardResourceType(DHWorldEventRewardType rewardType, out ResourceType resourceType)
    {
        switch (rewardType)
        {
            case DHWorldEventRewardType.Money:
                resourceType = ResourceType.Money;
                return true;
            case DHWorldEventRewardType.Crystal:
                resourceType = ResourceType.Crystal;
                return true;
            case DHWorldEventRewardType.Medal:
                resourceType = ResourceType.Chip;
                return true;
            case DHWorldEventRewardType.Supply:
                resourceType = ResourceType.Supply;
                return true;
            default:
                resourceType = default;
                return false;
        }
    }
}
